using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Scripts;

namespace DocumentVault.Function.Services
{
    public class CosmosDbInitializer : IHostedService
    {
        private readonly CosmosClient _cosmosClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CosmosDbInitializer> _logger;

        public CosmosDbInitializer(
            CosmosClient cosmosClient,
            IConfiguration configuration,
            ILoggerFactory loggerFactory)
        {
            _cosmosClient = cosmosClient;
            _configuration = configuration;
            _logger = loggerFactory.CreateLogger<CosmosDbInitializer>();
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Initializing CosmosDB database and containers");
            
            try
            {
                var databaseName = _configuration["CosmosDb:DatabaseName"];
                var documentsContainerName = _configuration["CosmosDb:DocumentsContainerName"];
                var linksContainerName = _configuration["CosmosDb:LinksContainerName"];
                
                // Create database if it doesn't exist
                var databaseResponse = await _cosmosClient.CreateDatabaseIfNotExistsAsync(databaseName, cancellationToken: cancellationToken);
                _logger.LogInformation($"Database {databaseName} initialization completed with status: {databaseResponse.StatusCode}");
                
                // Create Documents container if it doesn't exist
                var database = _cosmosClient.GetDatabase(databaseName);
                var documentsContainerProperties = new ContainerProperties(documentsContainerName, "/id");
                var documentsContainerResponse = await database.CreateContainerIfNotExistsAsync(
                    documentsContainerProperties, 
                    cancellationToken: cancellationToken);
                _logger.LogInformation($"Container {documentsContainerName} initialization completed with status: {documentsContainerResponse.StatusCode}");
                
                // Create Links container if it doesn't exist
                var linksContainerProperties = new ContainerProperties(linksContainerName, "/id");
                var linksContainerResponse = await database.CreateContainerIfNotExistsAsync(
                    linksContainerProperties, 
                    cancellationToken: cancellationToken);
                _logger.LogInformation($"Container {linksContainerName} initialization completed with status: {linksContainerResponse.StatusCode}");
                
                // Create or update stored procedures
                var linksContainer = database.GetContainer(linksContainerName);
                await CreateOrUpdateStoredProceduresAsync(linksContainer, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing CosmosDB");
                throw;
            }
        }

        private async Task CreateOrUpdateStoredProceduresAsync(Container container, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Creating or updating stored procedures");
            
            // Define the stored procedure for deleting expired links
            const string deleteExpiredLinksProcId = "spDeleteExpiredLinks";
            string deleteExpiredLinksSpBody = @"function deleteExpiredLinks() {
    var context = getContext();
    var collection = context.getCollection();
    var response = context.getResponse();
    var collectionLink = collection.getSelfLink();
    
    // Query to find expired links
    var now = new Date().toISOString();
    var query = ""SELECT * FROM c WHERE c.ExpiresAt < GetCurrentDateTime()"";
    
    // Set up the response body
    var responseBody = {
        deleted: 0,
        continuation: true
    };
    
    // Query for documents
    var accepted = collection.queryDocuments(
        collectionLink,
        query,
        {}, 
        function (err, documents, options) {
            if (err) {
                throw new Error('Error querying for expired links: ' + err.message);
            }
            
            if (documents.length === 0) {
                responseBody.continuation = false;
                response.setBody(responseBody);
                return;
            }
            
            // Process documents in batches
            tryDelete(documents, 0);
        }
    );
    
    if (!accepted) {
        throw new Error('The query was not accepted by the server.');
    }
    
    function tryDelete(documents, index) {
        if (index >= documents.length) {
            response.setBody(responseBody);
            return;
        }
        
        var document = documents[index];
        var isAccepted = collection.deleteDocument(
            document._self,
            {},
            function (err) {
                if (err) {
                    throw new Error('Error deleting document: ' + err.message);
                }
                
                responseBody.deleted++;
                tryDelete(documents, index + 1);
            }
        );
        
        if (!isAccepted) {
            responseBody.continuation = true;
            response.setBody(responseBody);
        }
    }
}";

            try
            {
                // Try to replace the stored procedure if it exists
                await container.Scripts.ReplaceStoredProcedureAsync(
                    new StoredProcedureProperties
                    {
                        Id = deleteExpiredLinksProcId,
                        Body = deleteExpiredLinksSpBody
                    }, 
                    cancellationToken: cancellationToken);
                
                _logger.LogInformation($"Stored procedure {deleteExpiredLinksProcId} updated successfully");
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Create the stored procedure if it doesn't exist
                await container.Scripts.CreateStoredProcedureAsync(
                    new StoredProcedureProperties
                    {
                        Id = deleteExpiredLinksProcId,
                        Body = deleteExpiredLinksSpBody
                    }, 
                    cancellationToken: cancellationToken);
                
                _logger.LogInformation($"Stored procedure {deleteExpiredLinksProcId} created successfully");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}