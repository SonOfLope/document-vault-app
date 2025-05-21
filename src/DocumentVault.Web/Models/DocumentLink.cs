using Newtonsoft.Json;

namespace DocumentVault.Web.Models
{
    public class DocumentLink
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;
        
        public string DocumentId { get; set; } = string.Empty;
        
        public string Url { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; }
        
        public DateTime ExpiresAt { get; set; }
    }
}