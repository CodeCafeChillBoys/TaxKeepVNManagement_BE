using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaxKeepVN.Application.DTOs.Consultations;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVNManagementSystem.Controllers
{
    /// <summary>
    /// Controller điều phối Phiên tư vấn trực tuyến 1-1 (Live Consultation Execution) (Đặc tả 5)
    /// Hỗ trợ: Điểm danh vào phòng Jitsi Meet, Xem chung hồ sơ thuế nội bộ, Cập nhật biên bản kết luận và Xử lý vắng mặt No-Show.
    /// </summary>
    [ApiController]
    [Route("api/v1/consultations")]
    [Authorize]
    [Tags("Consultation Live - Thực Hiện Phiên Tư Vấn Trực Tuyến 1-1")]
    [Produces("application/json")]
    public class ConsultationController : ControllerBase
    {
        private readonly IConsultationService _consultationService;

        public ConsultationController(IConsultationService consultationService)
        {
            _consultationService = consultationService;
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst("userId")?.Value 
                     ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }

        private string GetCurrentUserRole()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value 
                ?? User.FindFirst("role")?.Value 
                ?? "taxpayer";
        }

        /// <summary>
        /// Điểm danh và lấy link phòng họp trực tuyến Jitsi Meet (Đặc tả 5)
        /// Phòng chỉ mở trước giờ hẹn tối đa 10 phút (cấu hình trong system_configs: SESSION_OPEN_BEFORE_MINUTES).
        /// Khi cả 2 bên cùng vào phòng, hệ thống tự động chuyển trạng thái lịch hẹn sang IN_PROGRESS.
        /// POST /api/v1/consultations/{bookingId}/join
        /// </summary>
        [HttpPost("{bookingId:guid}/join", Name = "JoinConsultationSession")]
        [EndpointSummary("Điểm danh và lấy đường dẫn phòng họp trực tuyến Jitsi Meet")]
        [ProducesResponseType(typeof(ApiResponse<JoinConsultationResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> JoinSession([FromRoute] Guid bookingId)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();
            var result = await _consultationService.JoinSessionAsync(userId, role, bookingId);
            return Ok(ApiResponse<JoinConsultationResponse>.Ok(result, result.Message));
        }

        /// <summary>
        /// Xem chi tiết trạng thái phiên tư vấn hiện tại (Đặc tả 5)
        /// Trả về: thời gian check-in của 2 bên, trạng thái phòng, thời lượng đã diễn ra và số phút còn lại.
        /// GET /api/v1/consultations/{bookingId}/session
        /// </summary>
        [HttpGet("{bookingId:guid}/session", Name = "GetConsultationSession")]
        [EndpointSummary("Xem trạng thái chi tiết của phòng tư vấn trực tuyến")]
        [ProducesResponseType(typeof(ApiResponse<ConsultationSessionResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetSession([FromRoute] Guid bookingId)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();
            var result = await _consultationService.GetSessionAsync(userId, role, bookingId);
            return Ok(ApiResponse<ConsultationSessionResponse>.Ok(result, "Lấy thông tin phiên tư vấn thành công."));
        }

        /// <summary>
        /// Xem toàn bộ hồ sơ thuế nội bộ được chia sẻ (Read-only) trong phòng tư vấn (Đặc tả 3 & 5)
        /// Bao gồm: Bảng thu nhập, Bảng lương chi tiết, Người phụ thuộc (TT87), Chứng từ Y tế & Giáo dục, và Bản tính nháp quyết toán.
        /// Quyền xem chỉ mở khi lịch hẹn ở trạng thái CONFIRMED, IN_PROGRESS hoặc COMPLETED.
        /// GET /api/v1/consultations/{bookingId}/shared-dossier
        /// </summary>
        [HttpGet("{bookingId:guid}/shared-dossier", Name = "GetSharedTaxDossier")]
        [EndpointSummary("Xem toàn bộ hồ sơ thuế được chia sẻ của khách hàng (Read-only)")]
        [ProducesResponseType(typeof(ApiResponse<SharedTaxDossierResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetSharedTaxDossier([FromRoute] Guid bookingId)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();
            var result = await _consultationService.GetSharedTaxDossierAsync(userId, role, bookingId);
            return Ok(ApiResponse<SharedTaxDossierResponse>.Ok(result, "Lấy bộ hồ sơ thuế được chia sẻ thành công."));
        }

        /// <summary>
        /// Chuyên gia cập nhật bản nháp/ghi chú kết luận tư vấn (Đặc tả 5)
        /// PUT /api/v1/consultations/{bookingId}/notes
        /// </summary>
        [HttpPut("{bookingId:guid}/notes", Name = "UpdateConsultationNotes")]
        [EndpointSummary("Chuyên gia cập nhật biên bản ghi chú tư vấn trong phòng họp")]
        [ProducesResponseType(typeof(ApiResponse<ConsultationSessionResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateNotes(
            [FromRoute] Guid bookingId, 
            [FromBody] UpdateConsultationNotesRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));
            }

            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();
            var result = await _consultationService.UpdateNotesAsync(userId, role, bookingId, request);
            return Ok(ApiResponse<ConsultationSessionResponse>.Ok(result, "Cập nhật ghi chú tư vấn thành công."));
        }

        /// <summary>
        /// Hoàn thành phiên tư vấn (Đặc tả 5)
        /// Bắt buộc Chuyên gia phải nhập biên bản tóm tắt giải pháp tư vấn theo quy định BR-5.3.
        /// Hệ thống đóng phòng họp, chuyển trạng thái Booking sang COMPLETED và Escrow sang CoolingDown (24h).
        /// POST /api/v1/consultations/{bookingId}/complete
        /// </summary>
        [HttpPost("{bookingId:guid}/complete", Name = "CompleteConsultationSession")]
        [EndpointSummary("Chuyên gia chốt biên bản kết luận và kết thúc phiên tư vấn")]
        [ProducesResponseType(typeof(ApiResponse<ConsultationSessionResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CompleteSession(
            [FromRoute] Guid bookingId, 
            [FromBody] CompleteConsultationRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));
            }

            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();
            var result = await _consultationService.CompleteSessionAsync(userId, role, bookingId, request);
            return Ok(ApiResponse<ConsultationSessionResponse>.Ok(result, "Phiên tư vấn đã được hoàn tất thành công. Bắt đầu thời gian Cool-down ký quỹ."));
        }

        /// <summary>
        /// Báo cáo đối phương vắng mặt không phép (No-Show quá 15 phút) (Đặc tả 5)
        /// - Nếu Chuyên gia vắng mặt: Hệ thống tự động hủy ca và hoàn tiền 100% cho khách hàng.
        /// - Nếu Khách hàng vắng mặt: Hệ thống đóng ca và bảo vệ thù lao làm việc cho chuyên gia.
        /// POST /api/v1/consultations/{bookingId}/report-no-show
        /// </summary>
        [HttpPost("{bookingId:guid}/report-no-show", Name = "ReportConsultationNoShow")]
        [EndpointSummary("Báo cáo đối phương vắng mặt không phép (No-Show quá 15 phút)")]
        [ProducesResponseType(typeof(ApiResponse<ConsultationSessionResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ReportNoShow(
            [FromRoute] Guid bookingId, 
            [FromBody] ReportNoShowRequest? request = null)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();
            var result = await _consultationService.ReportNoShowAsync(userId, role, bookingId, request ?? new ReportNoShowRequest());
            return Ok(ApiResponse<ConsultationSessionResponse>.Ok(result, "Xử lý báo cáo vắng mặt thành công."));
        }
    }
}
