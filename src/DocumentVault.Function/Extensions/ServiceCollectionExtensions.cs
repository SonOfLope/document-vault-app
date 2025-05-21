using Azure.Storage;
using Azure.Storage.Blobs;
using DocumentVault.Function.Configuration;
using DocumentVault.Function.Services;
using Microsoft.Azure.Cosmos;
using System.Security.Cryptography.X509Certificates;

namespace DocumentVault.Function.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddFunctionServices(this IServiceCollection services, IConfiguration config)
        {
            // Register CosmosDB client
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

            // Register Blob Storage client
            services.AddSingleton(provider =>
            {
                var config = provider.GetRequiredService<IConfiguration>();
                var connectionString = config["BlobStorage:ConnectionString"];
                return new BlobServiceClient(connectionString);
            });

            // Register StorageSharedKeyCredential
            services.AddSingleton(provider =>
            {
                var connStr = config["BlobStorage:ConnectionString"];
                var accountName = ConfigurationHelpers.GetConnectionStringValue(connStr, "AccountName");
                var accountKey = ConfigurationHelpers.GetConnectionStringValue(connStr, "AccountKey");

                return new StorageSharedKeyCredential(accountName, accountKey);
            });
            
            // Add CosmosDB initializer
            services.AddHostedService<CosmosDbInitializer>();
            
            return services;
        }
    }
}