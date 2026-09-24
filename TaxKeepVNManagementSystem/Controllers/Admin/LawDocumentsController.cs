using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaxKeepVN.Application.DTOs.Law;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.Law.Document;

namespace TaxKeepVNManagementSystem.Controllers.Admin
{
    [ApiController]
    [Route("api/v1/admin/law/documents")]
    [Authorize(Roles = "TaxAdmin,Admin,taxadmin,admin")]
    public class LawDocumentsController : ControllerBase
    {
        private readonly ILawDocumentService _documentService;

        public LawDocumentsController(ILawDocumentService documentService)
        {
            _documentService = documentService;
        }

        private Guid GetAdminId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("userId")?.Value;
            return Guid.TryParse(claim, out var guid) ? guid : Guid.Empty;
        }

        /// <summary>
        /// Tải lên văn bản pháp luật PDF để bóc tách AI
        /// </summary>
        [HttpPost]
        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(ApiResponse<UploadDocumentResponseDto>), StatusCodes.Status202Accepted)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UploadDocument(
            [FromForm] IFormFile file,
            [FromForm] string? sourceUrl,
            [FromForm] string? documentNumberHint,
            CancellationToken ct = default)
        {
            string baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
            var result = await _documentService.UploadDocumentAsync(file, sourceUrl, documentNumberHint, GetAdminId(), baseUrl, ct);
            return StatusCode(StatusCodes.Status202Accepted, ApiResponse<UploadDocumentResponseDto>.Ok(result, "Tải lên văn bản thành công, tiến trình bóc tách AI đã được kích hoạt."));
        }

        /// <summary>
        /// Danh sách văn bản pháp luật
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<List<LegalDocumentDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetDocuments(
            [FromQuery] string? status,
            [FromQuery] string? q,
            [FromQuery] int page = 1,
            [FromQuery] int size = 20,
            CancellationToken ct = default)
        {
            var result = await _documentService.GetDocumentsAsync(status, q, page, size, ct);
            return Ok(ApiResponse<List<LegalDocumentDto>>.Ok(result, "Lấy danh sách văn bản thành công."));
        }

        /// <summary>
        /// Chi tiết một văn bản pháp luật kèm các quan hệ
        /// </summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<LegalDocumentDetailDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetDocumentDetail([FromRoute] Guid id, CancellationToken ct = default)
        {
            var result = await _documentService.GetDocumentDetailAsync(id, ct);
            return Ok(ApiResponse<LegalDocumentDetailDto>.Ok(result, "Lấy chi tiết văn bản thành công."));
        }

        /// <summary>
        /// Cập nhật thông tin văn bản pháp luật
        /// </summary>
        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<LegalDocumentDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdateDocument(
            [FromRoute] Guid id,
            [FromBody] UpdateLegalDocumentInput input,
            CancellationToken ct = default)
        {
            var result = await _documentService.UpdateDocumentAsync(id, input, ct);
            return Ok(ApiResponse<LegalDocumentDto>.Ok(result, "Cập nhật văn bản thành công."));
        }
    }
}
