using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.ExpertApplications;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVNManagementSystem.Controllers
{
    [ApiController]
    [Route("api/v1/expert-applications")]
    [Authorize] // Yêu cầu người dùng phải đăng nhập
    public class ExpertApplicationController : ControllerBase
    {
        private readonly IExpertApplicationService _service;

        public ExpertApplicationController(IExpertApplicationService service)
        {
            _service = service;
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst("userId")?.Value 
                     ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }

        /// <summary>
        /// Xem chi tiết hồ sơ đăng ký hiện tại của tôi
        /// GET /api/v1/expert-applications/my-application
        /// </summary>
        [HttpGet("my-application")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMyApplication()
        {
            var data = await _service.GetMyApplicationAsync(GetCurrentUserId());
            if (data == null)
            {
                return NotFound(ApiResponse<object>.Fail("NOT_FOUND", "Bạn chưa có hồ sơ đăng ký nào."));
            }

            return Ok(ApiResponse<ExpertApplicationDetailResponse>.Ok(data, "Lấy thông tin hồ sơ thành công."));
        }

        /// <summary>
        /// Tạo mới hoặc lưu cập nhật bản nháp hồ sơ chuyên gia
        /// POST /api/v1/expert-applications/draft
        /// </summary>
        [HttpPost("draft")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> SaveDraft([FromBody] SaveExpertApplicationDraftRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));
            }

            var data = await _service.SaveDraftAsync(GetCurrentUserId(), request);
            return Ok(ApiResponse<ExpertApplicationDetailResponse>.Ok(data, "Lưu bản nháp hồ sơ thành công."));
        }

        /// <summary>
        /// Tải lên và đính kèm chứng chỉ vào hồ sơ (Multipart/form-data)
        /// POST /api/v1/expert-applications/certificates
        /// </summary>
        [HttpPost("certificates")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UploadCertificate([FromForm] UploadCertificateRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));
            }

            var data = await _service.UploadCertificateAsync(GetCurrentUserId(), request);
            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<CertificateResponseDto>.Ok(data, "Tải lên chứng chỉ thành công."));
        }

        /// <summary>
        /// Xóa chứng chỉ khỏi hồ sơ bản nháp
        /// DELETE /api/v1/expert-applications/certificates/{certificateId}
        /// </summary>
        [HttpDelete("certificates/{certificateId:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> DeleteCertificate([FromRoute] Guid certificateId)
        {
            await _service.DeleteCertificateAsync(GetCurrentUserId(), certificateId);
            return Ok(ApiResponse<bool>.Ok(true, "Xóa chứng chỉ thành công."));
        }

        /// <summary>
        /// Nộp hồ sơ chính thức (Draft -> PendingReview)
        /// POST /api/v1/expert-applications/submit
        /// </summary>
        [HttpPost("submit")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Submit()
        {
            var data = await _service.SubmitApplicationAsync(GetCurrentUserId());
            return Ok(ApiResponse<ExpertApplicationDetailResponse>.Ok(data, "Nộp hồ sơ thành công. Hồ sơ của bạn đang chờ xét duyệt."));
        }
    }
}
