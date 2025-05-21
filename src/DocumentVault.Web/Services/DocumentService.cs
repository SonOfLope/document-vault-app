using DocumentVault.Web.Models;
using Microsoft.Azure.Cosmos;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Net;

namespace DocumentVault.Web.Services
{
    public class DocumentService : IDocumentService
    {
        private readonly CosmosClient _cosmosClient;
        private readonly BlobServiceClient _blobServiceClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DocumentService> _logger;
        private readonly Container _container;
        private readonly BlobContainerClient _blobContainer;

        public DocumentService(
            CosmosClient cosmosClient,
            BlobServiceClient blobServiceClient,
            IConfiguration configuration,
            ILogger<DocumentService> logger)
        {
            _cosmosClient = cosmosClient;
            _blobServiceClient = blobServiceClient;
            _configuration = configuration;
            _logger = logger;

            var databaseName = _configuration["CosmosDb:DatabaseName"];
            var containerName = _configuration["CosmosDb:DocumentsContainerName"];
            
            // Ensure database and container exist
            if (string.IsNullOrEmpty(databaseName) || string.IsNullOrEmpty(containerName))
            {
                throw new ArgumentException("Database name or container name is not configured.");
            }
            
            EnsureDatabaseAndContainerExistAsync(databaseName, containerName).GetAwaiter().GetResult();
            _container = _cosmosClient.GetContainer(databaseName, containerName);

            var blobContainerName = _configuration["BlobStorage:ContainerName"];
            _blobContainer = _blobServiceClient.GetBlobContainerClient(blobContainerName);
            
            // Create blob container if it doesn't exist
            try
            {
                _blobContainer.CreateIfNotExists();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating blob container {blobContainerName}");
            }
        }
        
        private async Task EnsureDatabaseAndContainerExistAsync(string databaseName, string containerName)
        {
            try
            {
                _logger.LogInformation($"Ensuring database {databaseName} exists");
                var database = await _cosmosClient.CreateDatabaseIfNotExistsAsync(databaseName);
                
                _logger.LogInformation($"Ensuring container {containerName} exists in database {databaseName}");
                await database.Database.CreateContainerIfNotExistsAsync(
                    id: containerName,
                    partitionKeyPath: "/id",
                    throughput: 400);
                
                _logger.LogInformation($"Database {databaseName} and container {containerName} are ready");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating database {databaseName} or container {containerName}");
                throw;
            }
        }

        public async Task<IEnumerable<Document>> GetDocumentsAsync()
        {
            var query = new QueryDefinition("SELECT * FROM c ORDER BY c.UploadedAt DESC");
            var results = new List<Document>();

            using var iterator = _container.GetItemQueryIterator<Document>(query);
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }

            return results;
        }

        public async Task<Document?> GetDocumentAsync(string id)
        {
            try
            {
                var response = await _container.ReadItemAsync<Document>(id, new PartitionKey(id));
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning($"Document {id} not found");
                return null;
            }
        }

        public async Task<Document> UploadDocumentAsync(IFormFile file, string[] tags)
        {
            // Ensure the blob container exists
            await _blobContainer.CreateIfNotExistsAsync();

            // Upload file to blob storage
            var blobName = $"{Guid.NewGuid()}/{file.FileName}";
            var blobClient = _blobContainer.GetBlobClient(blobName);
            
            using var stream = file.OpenReadStream();
            await blobClient.UploadAsync(stream, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = file.ContentType
                }
            });

            // Create document metadata in Cosmos DB
            var document = new Document
            {
                FileName = file.FileName,
                ContentType = file.ContentType,
                FileSize = file.Length,
                BlobPath = blobName,
                Tags = tags,
                UploadedAt = DateTime.UtcNow
            };

            await _container.CreateItemAsync(document, new PartitionKey(document.Id));
            return document;
        }

        public async Task DeleteDocumentAsync(string id)
        {
            // Get document first to get the blob path
            var document = await GetDocumentAsync(id);
            if (document == null)
            {
                _logger.LogWarning($"Document {id} not found for deletion");
                return;
            }

            // Delete from Cosmos DB
            await _container.DeleteItemAsync<Document>(id, new PartitionKey(id));

            // Delete from Blob Storage
            var blobClient = _blobContainer.GetBlobClient(document.BlobPath);
            await blobClient.DeleteIfExistsAsync();

            _logger.LogInformation($"Document {id} deleted successfully");
        }

        public async Task<IEnumerable<Document>> SearchDocumentsByTagsAsync(string[] tags)
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE ARRAY_CONTAINS(c.Tags, @tag)").WithParameter("@tag", tags[0]);
            
            if (tags.Length > 1)
            {
                var queryText = "SELECT * FROM c WHERE ";
                var conditions = new List<string>();

                for (int i = 0; i < tags.Length; i++)
                {
                    conditions.Add($"ARRAY_CONTAINS(c.Tags, @tag{i})");
                }

                queryText += string.Join(" OR ", conditions);
                query = new QueryDefinition(queryText);

                for (int i = 0; i < tags.Length; i++)
                {
                    query = query.WithParameter($"@tag{i}", tags[i]);
                }
            }

            var results = new List<Document>();
            using var iterator = _container.GetItemQueryIterator<Document>(query);
            
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }

            return results;
        }
    }
}