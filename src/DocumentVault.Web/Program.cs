using DocumentVault.Web.Services;
using Microsoft.Azure.Cosmos;
using Azure.Storage.Blobs;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddControllersWithViews();
builder.Services.AddApplicationInsightsTelemetry();

builder.Services.AddSingleton(s =>
{
    var endpoint = builder.Configuration["CosmosDb:Endpoint"];
    var key = builder.Configuration["CosmosDb:Key"];
    
    // Configure SSL validation for CosmosDB emulator
    bool disableSslValidation = Environment.GetEnvironmentVariable("COSMOSDB_DISABLE_SSL_VALIDATION") == "true";
    
    if (disableSslValidation)
    {
        // Disable SSL certificate validation for development
        var connectionPolicy = new CosmosClientOptions
        {
            ConnectionMode = ConnectionMode.Gateway,
            HttpClientFactory = () =>
            {
                HttpMessageHandler httpMessageHandler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (_, _, _, _) => true
                };
                return new HttpClient(httpMessageHandler);
            }
        };
        return new CosmosClient(endpoint, key, connectionPolicy);
    }
    
    // for development, we use the emulator certificate
    string? certPath = Environment.GetEnvironmentVariable("AZURE_COSMOS_EMULATOR_CERTIFICATE_PATH");
    if (!string.IsNullOrEmpty(certPath) && File.Exists(certPath))
    {
    var expectedCert = X509CertificateLoader.LoadCertificateFromFile(certPath);
    var connectionPolicy = new CosmosClientOptions
    {
        ConnectionMode = ConnectionMode.Gateway,
        HttpClientFactory = () =>
        {
            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = (request, cert, chain, errors) =>
            {
                return cert != null && cert.Thumbprint == expectedCert.Thumbprint;
            };
            return new HttpClient(handler);
        }
    };
        return new CosmosClient(endpoint, key, connectionPolicy);
    }
    
    return new CosmosClient(endpoint, key);
});

builder.Services.AddSingleton(s =>
{
    // For local development we're using Azurite via HTTP
    string connectionString = string.Empty;
    bool isDevelopment = builder.Environment.IsDevelopment();
    
    if (isDevelopment && !string.IsNullOrEmpty(builder.Configuration["BlobStorage:ConnectionString"]))
    {
        connectionString = builder.Configuration["BlobStorage:ConnectionString"] ?? string.Empty;
    }
    else
    {
        connectionString = $"DefaultEndpointsProtocol=https;AccountName={builder.Configuration["BlobStorage:AccountName"]};AccountKey={builder.Configuration["BlobStorage:Key"]};EndpointSuffix=core.windows.net";
    }
    
    return new BlobServiceClient(connectionString);
});

builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<ILinkService, LinkService>();
builder.Services.AddHttpClient();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

app.Run();