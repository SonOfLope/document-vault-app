using Newtonsoft.Json;

namespace DocumentVault.Web.Models
{
    public class Document
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        public string FileName { get; set; } = string.Empty;
        
        public string ContentType { get; set; } = string.Empty;
        
        public long FileSize { get; set; }
        
        public string BlobPath { get; set; } = string.Empty;
        
        public string[] Tags { get; set; } = Array.Empty<string>();
        
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        
        public string? UploadedBy { get; set; }
    }
}