using DocumentVault.Web.Models;
using System.Text;
using System.Text.Json;

namespace DocumentVault.Web.Services
{
    public class LinkService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<LinkService> logger) : ILinkService
    {
        private readonly HttpClient _httpClient = httpClient;
        private readonly IConfiguration _configuration = configuration;
        private readonly ILogger<LinkService> _logger = logger;

        public async Task<DocumentLink> GenerateLinkAsync(string documentId, int expiryHours)
        {
            var key = _configuration["FunctionApp:Key"];
            var functionHost = _configuration["FunctionApp:HostName"];
            
            var url = $"{functionHost}/api/documents/{documentId}/link";
            
            if (!string.IsNullOrEmpty(key))
            {
                url += $"?code={key}";
            }

            var request = new
            {
                ExpiryHours = expiryHours
            };

            var content = new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(url, content);
            
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var link = JsonSerializer.Deserialize<DocumentLink>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new Exception("Failed to deserialize link response");
            return link;
        }

        public async Task<DocumentLink?> GetLinkAsync(string linkId)
        {
            try
            {
                var key = _configuration["FunctionApp:Key"];
                var functionHost = _configuration["FunctionApp:HostName"];
                
                _logger.LogInformation($"Calling function at host: {functionHost}");
                var url = $"{functionHost}/api/links/{linkId}";
                
                // Add code as query param if it exists
                if (!string.IsNullOrEmpty(key))
                {
                    url += $"?code={key}";
                }

                var response = await _httpClient.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return null;
                    }
                    
                    throw new Exception($"Error getting link: {response.StatusCode}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var link = JsonSerializer.Deserialize<DocumentLink>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return link;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting link {linkId}");
                throw;
            }
        }
    }
}