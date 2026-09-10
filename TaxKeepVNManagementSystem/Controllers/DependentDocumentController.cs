using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVNManagementSystem.Controllers
{
    [ApiController]
    [Route("api/v1/dependents")]
    // [Authorize(Roles = "TAXPAYER")]
    public partial class DependentDocumentController : ControllerBase
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
            var userIdStr = "c1234567-89ab-cdef-0123-456789abcdef"; // Mock UUID
            _ = Guid.TryParse(userIdStr, out Guid userId);

            var result = await _documentService.UploadDocumentAsync(userId, dependentId, docType, file);
            var document = result.Document;

            var responseData = new
            {
                docId = document.Id,
                dependentId = document.DependentId,
                docType = document.DocType.ToString(),
                fileUrl = document.FileUrl,
                fileMimeType = document.FileMimeType,
                isReadable = document.IsReadable,
                uploadedAt = document.UploadedAt,
                isProfileComplete = result.IsProfileComplete,
                missingDocuments = result.MissingDocuments
            };

            return StatusCode(StatusCodes.Status201Created, ApiResponse<object>.Ok(responseData, "Tải lên và lưu trữ chứng từ gốc thành công."));
        }
    }
}
