using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVNManagementSystem.Controllers
{
    [ApiController]
    [Route("api/v1/dependents")]
    // [Authorize(Roles = "TAXPAYER")] // Comment out auth for easier testing if no JWT logic is implemented yet
    public class DependentDocumentController : ControllerBase
    {
        private readonly IDependentDocumentService _documentService;

        public DependentDocumentController(IDependentDocumentService documentService)
        {
            _documentService = documentService;
        }

        [HttpPost("{dependentId}/documents")]
        public async Task<IActionResult> UploadDocument(
            [FromRoute] Guid dependentId, 
            [FromForm] string docType, 
            IFormFile file)
        {
            // Dummy user ID for now since JWT is not fully setup
            // var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userIdStr = "c1234567-89ab-cdef-0123-456789abcdef"; // Mock UUID
            _ = Guid.TryParse(userIdStr, out Guid userId);

            var document = await _documentService.UploadDocumentAsync(userId, dependentId, docType, file);

            var responseData = new
            {
                docId = document.Id,
                dependentId = document.DependentId,
                docType = document.DocType.ToString(),
                fileUrl = document.FileUrl,
                fileMimeType = document.FileMimeType,
                isReadable = document.IsReadable,
                uploadedAt = document.UploadedAt
            };

            return StatusCode(StatusCodes.Status201Created, ApiResponse<object>.Ok(responseData, "Tải lên và lưu trữ chứng từ gốc thành công."));
        }
    }
}
