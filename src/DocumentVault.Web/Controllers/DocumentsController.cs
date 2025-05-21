using DocumentVault.Web.Models;
using DocumentVault.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DocumentVault.Web.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DocumentsController : ControllerBase
    {
        private readonly IDocumentService _documentService;
        private readonly ILinkService _linkService;
        private readonly ILogger<DocumentsController> _logger;

        public DocumentsController(
            IDocumentService documentService,
            ILinkService linkService,
            ILogger<DocumentsController> logger)
        {
            _documentService = documentService;
            _linkService = linkService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Document>>> GetDocuments()
        {
            try
            {
                var documents = await _documentService.GetDocumentsAsync();
                return Ok(documents);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting documents");
                return StatusCode(500, "An error occurred while retrieving documents.");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Document>> GetDocument(string id)
        {
            try
            {
                var document = await _documentService.GetDocumentAsync(id);

                if (document == null)
                {
                    return NotFound();
                }

                return Ok(document);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting document {id}");
                return StatusCode(500, "An error occurred while retrieving the document.");
            }
        }

        [HttpPost]
        public async Task<ActionResult<Document>> UploadDocument([FromForm] IFormFile file, [FromForm] string tags)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file was uploaded.");
            }

            try
            {
                var tagArray = string.IsNullOrEmpty(tags) 
                    ? Array.Empty<string>() 
                    : tags.Split(',').Select(t => t.Trim()).ToArray();
                
                var document = await _documentService.UploadDocumentAsync(file, tagArray);
                return CreatedAtAction(nameof(GetDocument), new { id = document.Id }, document);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading document");
                return StatusCode(500, "An error occurred while uploading the document.");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDocument(string id)
        {
            try
            {
                await _documentService.DeleteDocumentAsync(id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting document {id}");
                return StatusCode(500, "An error occurred while deleting the document.");
            }
        }

        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<Document>>> SearchDocuments([FromQuery] string tags)
        {
            if (string.IsNullOrEmpty(tags))
            {
                return BadRequest("No tags provided for search.");
            }

            try
            {
                var tagArray = tags.Split(',').Select(t => t.Trim()).ToArray();
                var documents = await _documentService.SearchDocumentsByTagsAsync(tagArray);
                return Ok(documents);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching documents");
                return StatusCode(500, "An error occurred while searching for documents.");
            }
        }

        [HttpPost("{id}/link")]
        public async Task<ActionResult<DocumentLink>> CreateLink(string id, [FromBody] CreateLinkRequest request)
        {
            if (request == null || request.ExpiryHours <= 0)
            {
                return BadRequest("Invalid expiry time.");
            }

            try
            {
                var document = await _documentService.GetDocumentAsync(id);
                if (document == null)
                {
                    return NotFound("Document not found.");
                }

                var link = await _linkService.GenerateLinkAsync(id, request.ExpiryHours);
                return Ok(link);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating link for document {id}");
                return StatusCode(500, "An error occurred while creating the link.");
            }
        }
    }

    public class CreateLinkRequest
    {
        public int ExpiryHours { get; set; }
    }
}