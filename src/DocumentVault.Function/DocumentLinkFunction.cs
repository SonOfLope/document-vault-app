using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Newtonsoft.Json;
using Microsoft.Azure.Cosmos;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Azure.Storage;
using System.Net;
using System.Text.Json;

namespace DocumentVault.Function
{
    public class DocumentLinkFunction
    {
        private readonly CosmosClient _cosmosClient;
        private readonly BlobServiceClient _blobServiceClient;
        private readonly string _databaseName;
        private readonly string _linksContainerName;
        private readonly string _documentsContainerName;
        private readonly string _blobContainerName;
        private readonly string _cdnEndpoint;
        private readonly StorageSharedKeyCredential _storageSharedKeyCredential;

        public DocumentLinkFunction(
            CosmosClient cosmosClient,
            BlobServiceClient blobServiceClient,
            IConfiguration configuration,
            StorageSharedKeyCredential storageSharedKeyCredential)

        {
            _cosmosClient = cosmosClient;
            _blobServiceClient = blobServiceClient;
            _databaseName = configuration["CosmosDb:DatabaseName"];
            _linksContainerName = configuration["CosmosDb:LinksContainerName"];
            _documentsContainerName = configuration["CosmosDb:DocumentsContainerName"];
            _blobContainerName = configuration["BlobStorage:ContainerName"];
            _cdnEndpoint = configuration["CdnEndpoint"];
            _storageSharedKeyCredential = storageSharedKeyCredential;
        }

        [Function("GenerateDocumentLink")]
        public async Task<HttpResponseData> GenerateLink(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "documents/{documentId}/link")] HttpRequestData req,
            string documentId,
            FunctionContext context)
        {
            var logger = context.GetLogger("GenerateDocumentLink");
            logger.LogInformation($"Generating link for document {documentId}");

            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var data = JsonConvert.DeserializeObject<GenerateLinkRequest>(requestBody);
                
               if (data == null || data.ExpiryHours <= 0)
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteStringAsync("Please provide a valid expiry time in hours");
                    return badRequest;
                }

                // Get document from Cosmos DB
                var container = _cosmosClient.GetContainer(_databaseName, _documentsContainerName);
                var document = await container.ReadItemAsync<DocumentMetadata>(
                    documentId, 
                    new PartitionKey(documentId)
                );

                // Generate unique link ID
                string linkId = Guid.NewGuid().ToString();
                
                // Generate SAS token for blob
                var blobContainerClient = _blobServiceClient.GetBlobContainerClient(_blobContainerName);
                var blobClient = blobContainerClient.GetBlobClient(document.Resource.BlobPath);

                var sasBuilder = new BlobSasBuilder
                {
                    BlobContainerName = _blobContainerName,
                    BlobName = document.Resource.BlobPath,
                    Resource = "b", // "b" for blob
                    ExpiresOn = DateTimeOffset.UtcNow.AddHours(data.ExpiryHours)
                };
                
                sasBuilder.SetPermissions(BlobSasPermissions.Read);
                var sasToken = sasBuilder.ToSasQueryParameters(_storageSharedKeyCredential).ToString();


                string blobUrl = blobClient.Uri.ToString();
                string cdnUrl = blobUrl.Replace(
                    $"https://{_blobServiceClient.AccountName}.blob.core.windows.net",
                    _cdnEndpoint
                );
                
                // Store link in Cosmos DB
                var linksContainer = _cosmosClient.GetContainer(_databaseName, _linksContainerName);
                var link = new DocumentLink
                {
                    Id = linkId,
                    DocumentId = documentId,
                    SasToken = sasToken,
                    ExpiresAt = DateTime.UtcNow.AddHours(data.ExpiryHours),
                    CreatedAt = DateTime.UtcNow,
                    Url = $"{cdnUrl}?{sasToken}"
                };
                
                await linksContainer.CreateItemAsync(link, new PartitionKey(link.Id));

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    linkId,
                    url = link.Url,
                    expiresAt = link.ExpiresAt
                });
                return response;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                return notFound;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error generating link: {ex.Message}");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                return error;
            }
        }

        [Function("GetDocumentLink")]
        public async Task<HttpResponseData> GetLink(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "links/{linkId}")] HttpRequestData req,
            string linkId,
            FunctionContext context)
        {
            var logger = context.GetLogger("GetDocumentLink");
            logger.LogInformation($"Getting link {linkId}");


            try
            {
                // Get link from Cosmos DB
                var container = _cosmosClient.GetContainer(_databaseName, _linksContainerName);
                var link = await container.ReadItemAsync<DocumentLink>(
                    linkId, 
                    new PartitionKey(linkId)
                );

                if (link.Resource.ExpiresAt < DateTime.UtcNow)
                {
                    var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequest.WriteStringAsync("Link has expired");
                    return badRequest;
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    url = link.Resource.Url,
                    expiresAt = link.Resource.ExpiresAt,
                    documentId = link.Resource.DocumentId
                });
                return response;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                logger.LogError($"Link {linkId} not found");
                return req.CreateResponse(HttpStatusCode.NotFound);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error getting link: {ex.Message}");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }
        
        [Function("CheckExpiredLinks")]
        public async Task CheckExpiredLinks(
            [TimerTrigger("0 0 * * * *")] TimerInfo timer,
            FunctionContext context)
        {
            var logger = context.GetLogger("CheckExpiredLinks");
            logger.LogInformation("Running expired links cleanup");

            try 
            {
                var container = _cosmosClient.GetContainer(_databaseName, _linksContainerName);
                var now = DateTime.UtcNow;
                
                // Query for expired links
                var query = new QueryDefinition(
                    "SELECT * FROM c WHERE c.ExpiresAt < @now")
                    .WithParameter("@now", now);
                
                var expiredLinks = new List<DocumentLink>();
                using (var iterator = container.GetItemQueryIterator<DocumentLink>(query))
                {
                    while (iterator.HasMoreResults)
                    {
                        var response = await iterator.ReadNextAsync();
                        expiredLinks.AddRange(response);
                    }
                }
                
                logger.LogInformation($"Found {expiredLinks.Count} expired links");

                // Delete expired links
                foreach (var link in expiredLinks)
                {
                    await container.DeleteItemAsync<DocumentLink>(
                        link.Id,
                        new PartitionKey(link.Id)
                    );
                    logger.LogInformation($"Deleted expired link {link.Id}");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error checking expired links: {ex.Message}");
            }
        }
    }

    public class GenerateLinkRequest
    {
        public int ExpiryHours { get; set; }
    }

    public class DocumentMetadata
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        
        public string FileName { get; set; }
        
        public string ContentType { get; set; }
        
        public string BlobPath { get; set; }
        
        public string[] Tags { get; set; }
        
        public DateTime UploadedAt { get; set; }
    }

    public class DocumentLink
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        
        public string DocumentId { get; set; }
        
        public string SasToken { get; set; }
        
        public string Url { get; set; }
        
        public DateTime CreatedAt { get; set; }
        
        public DateTime ExpiresAt { get; set; }
    }
}