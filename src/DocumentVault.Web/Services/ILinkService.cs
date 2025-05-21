using DocumentVault.Web.Models;

namespace DocumentVault.Web.Services
{
    public interface ILinkService
    {
        Task<DocumentLink> GenerateLinkAsync(string documentId, int expiryHours);
        Task<DocumentLink?> GetLinkAsync(string linkId);
    }
}