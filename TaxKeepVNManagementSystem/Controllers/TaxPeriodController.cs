using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaxKeepVN.Application.DTOs.Documents;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.DTOs.TaxPeriods;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVNManagementSystem.Controllers
{
    [ApiController]
    [Route("api/v1/tax-periods")]
    [Authorize]
    public class TaxPeriodController : ControllerBase
    {
        private readonly ITaxPeriodService _service;
        public TaxPeriodController(ITaxPeriodService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<IActionResult> InitializePeriod([FromBody] InitTaxPeriodRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));
            }

            var result = await _service.InitOrGetPeriodAsync(request.UserId!.Value, request.TaxYear!.Value);
            return Ok(ApiResponse<TaxPeriodResponseDto>.Ok(result, "Tax year period initialized successfully."));
        }

        /// <summary>
        /// Tải lên danh sách hóa đơn / chứng từ cho một kỳ tính thuế (Batch Upload Tax Documents).
        /// Sau khi lưu trữ và tạo bản ghi ở trạng thái UPLOADED, hệ thống sẽ kích hoạt RabbitMQ để AI xử lý ngầm.
        /// POST /api/v1/tax-periods/{periodId}/documents/upload
        /// </summary>
        [HttpPost("{periodId:guid}/documents/upload")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> BatchUploadDocuments(
            [FromRoute] Guid periodId,
            [FromForm] List<IFormFile> files)
        {
            var userId = GetUserIdFromToken();
            var result = await _service.BatchUploadDocumentsAsync(userId, periodId, files);
            return Ok(ApiResponse<BatchUploadDocumentsResponseDto>.Ok(result, "Documents uploaded successfully."));
        }

        /// <summary>
        /// Xác nhận và lưu trữ chính thức dữ liệu chứng từ sau khi người dùng review kết quả từ AI.
        /// Chuyển trạng thái chứng từ sang CONFIRMED.
        /// PUT /api/v1/tax-periods/{periodId}/documents/{documentId}/confirm
        /// </summary>
        [HttpPut("{periodId:guid}/documents/{documentId:guid}/confirm")]
        public async Task<IActionResult> ConfirmDocumentReview(
            [FromRoute] Guid periodId,
            [FromRoute] Guid documentId,
            [FromBody] ConfirmDocumentReviewRequestDto dto)
        {
            var userId = GetUserIdFromToken();
            var result = await _service.ConfirmDocumentReviewAsync(userId, periodId, documentId, dto);
            return Ok(ApiResponse<DocumentReviewResponseDto>.Ok(result, "Xác nhận và lưu trữ dữ liệu chứng từ thành công."));
        }

        private Guid GetUserIdFromToken()
        {
            var userIdClaim = User.FindFirst("userId")?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                throw new UnauthorizedException("INVALID_TOKEN",
                    "Không thể xác định danh tính người dùng từ token. Vui lòng đăng nhập lại.");
            }
            return userId;
        }
    }
}