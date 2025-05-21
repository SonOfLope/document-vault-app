using System.Security.Cryptography.X509Certificates;
using Azure.Storage;
using Microsoft.Azure.Cosmos;
using Azure.Storage.Blobs;

namespace DocumentVault.Function
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = new HostBuilder()
                .ConfigureFunctionsWorkerDefaults()
                .ConfigureAppConfiguration(config =>
                {
                    config.AddEnvironmentVariables();
                })
                .ConfigureServices((context, services) =>
                {
                    var config = context.Configuration;

                    services.AddSingleton(_ =>
                    {
                        var endpoint = config["CosmosDb:Endpoint"];
                        var key = config["CosmosDb:Key"];

                        bool disableSslValidation = Environment.GetEnvironmentVariable("COSMOSDB_DISABLE_SSL_VALIDATION") == "true";

                        if (disableSslValidation)
                        {
                            var options = new CosmosClientOptions
                            {
                                ConnectionMode = ConnectionMode.Gateway,
                                HttpClientFactory = () =>
                                {
                                    var handler = new HttpClientHandler
                                    {
                                        ServerCertificateCustomValidationCallback = (_, _, _, _) => true
                                    };
                                    return new HttpClient(handler);
                                }
                            };
                            return new CosmosClient(endpoint, key, options);
                        }

                        string certPath = Environment.GetEnvironmentVariable("AZURE_COSMOS_EMULATOR_CERTIFICATE_PATH");
                        if (!string.IsNullOrEmpty(certPath) && File.Exists(certPath))
                        {
                            var expectedCert = new X509Certificate2(certPath);
                            var options = new CosmosClientOptions
                            {
                                ConnectionMode = ConnectionMode.Gateway,
                                HttpClientFactory = () =>
                                {
                                    var handler = new HttpClientHandler();
                                    handler.ServerCertificateCustomValidationCallback = (_, cert, _, _) =>
                                    {
                                        return cert?.Thumbprint == expectedCert.Thumbprint;
                                    };
                                    return new HttpClient(handler);
                                }
                            };
                            return new CosmosClient(endpoint, key, options);
                        }

                        return new CosmosClient(endpoint, key);
                    });

                    services.AddSingleton(provider =>
                    {
                        var config = provider.GetRequiredService<IConfiguration>();
                        var connectionString = config["BlobStorage:ConnectionString"];
                        return new BlobServiceClient(connectionString);
                    });

                    services.AddSingleton(provider =>
                    {
                        var connStr = config["BlobStorage:ConnectionString"];
                        var accountName = GetConnectionStringValue(connStr, "AccountName");
                        var accountKey = GetConnectionStringValue(connStr, "AccountKey");

                        return new StorageSharedKeyCredential(accountName, accountKey);
                    });
                    
                    // Add a hosted service to initialize CosmosDB containers
                    services.AddHostedService<CosmosDbInitializer>();
                })
                .Build();

            host.Run();
        }

        // Helper method for storage connection string
        private static string GetConnectionStringValue(string connectionString, string key)
        {
            var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var kvp = part.Split('=', 2);
                if (kvp.Length == 2 && kvp[0].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp[1].Trim();
                }
            }

            throw new InvalidOperationException($"Key '{key}' not found in the connection string.");
        }
    }

    // CosmosDB initializer service
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing CosmosDB");
                throw;
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
