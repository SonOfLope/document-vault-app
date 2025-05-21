using DocumentVault.Web.Models;

namespace DocumentVault.Web.Services
{
    public interface IDocumentService
    {
        Task<IEnumerable<Document>> GetDocumentsAsync();
        Task<Document?> GetDocumentAsync(string id);
        Task<Document> UploadDocumentAsync(IFormFile file, string[] tags);
        Task DeleteDocumentAsync(string id);
        Task<IEnumerable<Document>> SearchDocumentsByTagsAsync(string[] tags);
    }
}