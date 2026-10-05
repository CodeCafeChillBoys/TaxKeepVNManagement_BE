using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.ExpertApplications;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVNManagementSystem.Controllers
{
    [ApiController]
    [Route("api/v1/admin/expert-applications")]
    public class AdminExpertApplicationController : ControllerBase
    {
        private readonly IExpertApplicationService _service;

        public AdminExpertApplicationController(IExpertApplicationService service)
        {
            _service = service;
        }

        private Guid GetAdminUserId()
        {
            var claim = User.FindFirst("userId")?.Value 
                     ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }

        /// <summary>
        /// Lấy danh sách hồ sơ đăng ký chuyên gia (phân trang, lọc theo status, tìm kiếm theo tên/mã hồ sơ)
        /// GET /api/v1/admin/expert-applications?status=PendingReview&page=1&size=10&search=Chuyen
        /// </summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetApplications([FromQuery] AdminExpertApplicationQueryParameters query)
        {
            var data = await _service.GetApplicationsAsync(query);
            return Ok(ApiResponse<PagedResult<ExpertApplicationListItemResponse>>.Ok(data, "Lấy danh sách hồ sơ chuyên gia thành công."));
        }

        /// <summary>
        /// Xem toàn bộ thông tin chi tiết hồ sơ đăng ký của ứng viên
        /// GET /api/v1/admin/expert-applications/{id}
        /// </summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetApplicationById([FromRoute] Guid id)
        {
            var data = await _service.GetApplicationByIdAsync(id);
            return Ok(ApiResponse<ExpertApplicationDetailResponse>.Ok(data, "Lấy chi tiết hồ sơ chuyên gia thành công."));
        }

        /// <summary>
        /// Thẩm định một chứng chỉ cụ thể trong hồ sơ (PendingVerification -> Verified / Rejected / Expired)
        /// PUT /api/v1/admin/expert-applications/{id}/certificates/{certificateId}/verify
        /// </summary>
        [HttpPut("{id:guid}/certificates/{certificateId:guid}/verify")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> VerifyCertificate(
            [FromRoute] Guid id,
            [FromRoute] Guid certificateId,
            [FromBody] VerifyCertificateRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));
            }

            var data = await _service.VerifyCertificateAsync(GetAdminUserId(), id, certificateId, request);
            return Ok(ApiResponse<CertificateResponseDto>.Ok(data, "Thẩm định chứng chỉ thành công."));
        }

        /// <summary>
        /// Phê duyệt hồ sơ chuyên gia (Approved -> Nâng quyền tài khoản sang 'expert', tạo ExpertProfile)
        /// POST /api/v1/admin/expert-applications/{id}/approve
        /// </summary>
        [HttpPost("{id:guid}/approve")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> ApproveApplication(
            [FromRoute] Guid id,
            [FromBody] ApproveExpertApplicationRequest? request)
        {
            var data = await _service.ApproveApplicationAsync(GetAdminUserId(), id, request);
            return Ok(ApiResponse<ExpertApplicationDetailResponse>.Ok(data, "Phê duyệt hồ sơ chuyên gia thành công."));
        }

        /// <summary>
        /// Từ chối hồ sơ chuyên gia kèm lý do (Rejected - BR-09)
        /// POST /api/v1/admin/expert-applications/{id}/reject
        /// </summary>
        [HttpPost("{id:guid}/reject")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> RejectApplication(
            [FromRoute] Guid id,
            [FromBody] RejectExpertApplicationRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));
            }

            var data = await _service.RejectApplicationAsync(GetAdminUserId(), id, request);
            return Ok(ApiResponse<ExpertApplicationDetailResponse>.Ok(data, "Đã từ chối hồ sơ chuyên gia."));
        }

        /// <summary>
        /// Yêu cầu ứng viên bổ sung thông tin/giấy tờ kèm nội dung chi tiết (NeedSupplement - BR-10)
        /// POST /api/v1/admin/expert-applications/{id}/request-supplement
        /// </summary>
        [HttpPost("{id:guid}/request-supplement")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> RequestSupplement(
            [FromRoute] Guid id,
            [FromBody] RequestSupplementRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));
            }

            var data = await _service.RequestSupplementAsync(GetAdminUserId(), id, request);
            return Ok(ApiResponse<ExpertApplicationDetailResponse>.Ok(data, "Đã gửi yêu cầu bổ sung hồ sơ tới ứng viên."));
        }
    }
}
