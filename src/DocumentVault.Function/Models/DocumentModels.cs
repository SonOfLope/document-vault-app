using Newtonsoft.Json;

namespace DocumentVault.Function.Models
{
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
        
        [JsonProperty("DocumentType")]
        public string DocumentType { get; set; } = "link";
    }
}