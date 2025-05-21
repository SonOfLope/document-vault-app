using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DocumentVault.Web.Models;
using DocumentVault.Web.Services;

namespace DocumentVault.Web.Pages
{
    public class IndexModel : PageModel
    {
        private readonly IDocumentService _documentService;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(IDocumentService documentService, ILogger<IndexModel> logger)
        {
            _documentService = documentService;
            _logger = logger;
        }

        public IEnumerable<Document> Documents { get; private set; } = Array.Empty<Document>();

        public async Task OnGetAsync()
        {
            try
            {
                Documents = await _documentService.GetDocumentsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving documents");
                Documents = Array.Empty<Document>();
            }
        }

        public async Task<IActionResult> OnPostAsync(IFormFile file, string tags)
        {
            if (file == null || file.Length == 0)
            {
                return RedirectToPage();
            }

            try
            {
                var tagArray = string.IsNullOrEmpty(tags) 
                    ? Array.Empty<string>() 
                    : tags.Split(',').Select(t => t.Trim()).ToArray();
                
                await _documentService.UploadDocumentAsync(file, tagArray);
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading document");
                return RedirectToPage();
            }
        }
    }
}