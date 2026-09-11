using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Documents;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVNManagementSystem.Controllers
{
    [ApiController]
    [Route("api/v1/dependents")]
    // [Authorize(Roles = "TAXPAYER")]
    public class DependentDocumentController : ControllerBase
    {
        private readonly IDependentDocumentService _documentService;

        public DependentDocumentController(IDependentDocumentService documentService)
        {
            _documentService = documentService;
        }

        /// <summary>
        /// Upload giấy tờ chứng minh người phụ thuộc.
        /// POST /api/v1/dependents/{dependentId}/documents
        /// </summary>
        [HttpPost("{dependentId:guid}/documents", Name = "UploadDependentDocument")]
        public async Task<IActionResult> UploadDocument(
            [FromRoute] Guid dependentId,
            [FromForm] UploadDocumentRequestDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));

            var userId = GetUserIdFromToken();

            var result = await _documentService.UploadDocumentAsync(userId, dependentId, dto.DocType, dto.File);
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

            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<object>.Ok(responseData, "Tải lên và lưu trữ chứng từ gốc thành công."));
        }

        private Guid GetUserIdFromToken()
        {
            var userIdClaim = User.FindFirst("userId")?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                if (Guid.TryParse("c1234567-89ab-cdef-0123-456789abcdef", out var mockId))
                    return mockId;

                throw new TaxKeepVN.Application.Exceptions.UnauthorizedException("INVALID_TOKEN",
                    "Không thể xác định danh tính người dùng từ token.");
            }
            return userId;
        }
    }
}
