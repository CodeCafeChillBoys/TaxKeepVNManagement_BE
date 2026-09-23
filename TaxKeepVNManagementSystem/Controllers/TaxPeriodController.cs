using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaxKeepVN.Application.Constants;
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
            return Ok(ApiResponse<TaxPeriodResponseDto>.Ok(result, SuccessMessages.TaxPeriodInitialized));
        }

        /// <summary>
        /// Nộp và khóa kỳ tính thuế (chuyển trạng thái sang SUBMITTED).
        /// POST /api/v1/tax-periods/{periodId}/submit
        /// </summary>
        [HttpPost("{periodId:guid}/submit")]
        public async Task<IActionResult> SubmitTaxPeriod([FromRoute] Guid periodId)
        {
            var userId = GetUserIdFromToken();
            var result = await _service.SubmitTaxPeriodAsync(userId, periodId);
            return Ok(ApiResponse<TaxPeriodResponseDto>.Ok(result, "Nộp kỳ kê khai thuế thành công. Kỳ tính thuế đã được khóa."));
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
            return Ok(ApiResponse<BatchUploadDocumentsResponseDto>.Ok(result, SuccessMessages.DocumentsUploaded));
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
            return Ok(ApiResponse<DocumentReviewResponseDto>.Ok(result, SuccessMessages.DocumentReviewConfirmed));
        }

        /// <summary>
        /// Kích hoạt lại bóc tách OCR cho một chứng từ cụ thể (chỉ cho phép khi ở trạng thái UPLOADED).
        /// POST /api/v1/tax-periods/{periodId}/documents/{documentId}/extract
        /// </summary>
        [HttpPost("{periodId:guid}/documents/{documentId:guid}/extract")]
        public async Task<IActionResult> TriggerDocumentOcr(
            [FromRoute] Guid periodId,
            [FromRoute] Guid documentId)
        {
            var userId = GetUserIdFromToken();
            await _service.TriggerDocumentOcrAsync(userId, periodId, documentId);
            return Ok(ApiResponse<object>.Ok(null, SuccessMessages.DocumentOcrTriggered));
        }


        /// <summary>
        /// Lấy danh sách chứng từ theo kỳ tính thuế (mặc định mới nhất lên đầu).
        /// GET /api/v1/tax-periods/{periodId}/documents
        /// </summary>
        [HttpGet("{periodId:guid}/documents")]
        public async Task<IActionResult> GetDocumentsByPeriod(
            [FromRoute] Guid periodId)
        {
            var userId = GetUserIdFromToken();
            var result = await _service.GetDocumentsAsync(userId, periodId);
            return Ok(ApiResponse<List<DocumentReviewResponseDto>>.Ok(result, "Lấy danh sách chứng từ thành công."));
        }

        /// <summary>
        /// Xem thông tin chi tiết của một chứng từ cụ thể theo ID (kèm thông tin DocType và các dòng chi tiết Items).
        /// GET /api/v1/tax-periods/{periodId}/documents/{documentId}
        /// </summary>
        [HttpGet("{periodId:guid}/documents/{documentId:guid}")]
        public async Task<IActionResult> GetDocumentById(
            [FromRoute] Guid periodId,
            [FromRoute] Guid documentId)
        {
            var userId = GetUserIdFromToken();
            var result = await _service.GetDocumentByIdAsync(userId, periodId, documentId);
            return Ok(ApiResponse<DocumentReviewResponseDto>.Ok(result, "Lấy chi tiết chứng từ thành công."));
        }

        private Guid GetUserIdFromToken()
        {
            var userIdClaim = User.FindFirst("userId")?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                throw new UnauthorizedException(ErrorCodes.InvalidToken, ErrorMessages.InvalidToken);
            }
            return userId;
        }
    }
}