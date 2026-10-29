using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Disputes;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVNManagementSystem.Controllers
{
    /// <summary>
    /// Controller Khiếu nại dịch vụ & Xử lý tranh chấp ca tư vấn (Đặc tả 7)
    /// </summary>
    [ApiController]
    [Authorize]
    [Produces("application/json")]
    public class DisputeController : ControllerBase
    {
        private readonly IDisbursementDisputeService _service;

        public DisputeController(IDisbursementDisputeService service)
        {
            _service = service;
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

        // ════════════════════════════════════════════════════════════════════════
        //  1. KHÁCH HÀNG: XÁC NHẬN HÀI LÒNG SỚM / KIỂM TRA ĐIỀU KIỆN KHIẾU NẠI
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Khách hàng chủ động bấm "Xác nhận hài lòng" sớm trong thời gian 24 giờ Cool-down (Đặc tả 7 - Mục 5.2)
        /// Hệ thống giải ngân ngay lập tức tiền thù lao cho Chuyên gia mà không cần chờ hết 24 giờ.
        /// POST /api/v1/bookings/{bookingId}/confirm-satisfaction
        /// </summary>
        [HttpPost("api/v1/bookings/{bookingId:guid}/confirm-satisfaction")]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ConfirmSatisfaction([FromRoute] Guid bookingId)
        {
            var userId = GetCurrentUserId();
            await _service.ConfirmSatisfactionAsync(userId, bookingId);
            return Ok(ApiResponse<string>.Ok("Bạn đã xác nhận hài lòng. Thù lao đã được chuyển vào ví chuyên gia thành công.", "Xác nhận hài lòng thành công."));
        }

        /// <summary>
        /// Kiểm tra tính hợp lệ và thời hạn khiếu nại (Đặc tả 7 - Mục 6.B Bước 1)
        /// Trả về canDispute, deadline và số giây đếm ngược còn lại để FE hiển thị nút Khiếu nại.
        /// GET /api/v1/bookings/{bookingId}/dispute-eligibility
        /// </summary>
        [HttpGet("api/v1/bookings/{bookingId:guid}/dispute-eligibility")]
        [ProducesResponseType(typeof(ApiResponse<DisputeEligibilityResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> CheckDisputeEligibility([FromRoute] Guid bookingId)
        {
            var userId = GetCurrentUserId();
            var result = await _service.CheckDisputeEligibilityAsync(userId, bookingId);
            return Ok(ApiResponse<DisputeEligibilityResponse>.Ok(result, "Kiểm tra điều kiện khiếu nại thành công."));
        }

        /// <summary>
        /// Khách hàng gửi khiếu nại chất lượng phiên tư vấn (Đặc tả 7 - Mục 6.B)
        /// Lập tức đóng băng tiền Escrow (Freeze Escrow) và chuyển Booking sang UNDER_DISPUTE.
        /// POST /api/v1/bookings/{bookingId}/disputes
        /// </summary>
        [HttpPost("api/v1/bookings/{bookingId:guid}/disputes")]
        [ProducesResponseType(typeof(ApiResponse<DisputeDetailResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SubmitDispute(
            [FromRoute] Guid bookingId,
            [FromBody] CreateDisputeRequest request)
        {
            var userId = GetCurrentUserId();
            var result = await _service.SubmitDisputeAsync(userId, bookingId, request);
            return Ok(ApiResponse<DisputeDetailResponse>.Ok(result, "Đã gửi khiếu nại thành công. Khoản tiền tạm giữ đã được đóng băng để giải quyết tranh chấp."));
        }

        /// <summary>
        /// Xem thông tin khiếu nại của một ca Booking
        /// GET /api/v1/bookings/{bookingId}/dispute
        /// </summary>
        [HttpGet("api/v1/bookings/{bookingId:guid}/dispute")]
        [ProducesResponseType(typeof(ApiResponse<DisputeDetailResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetDisputeByBookingId([FromRoute] Guid bookingId)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();
            var result = await _service.GetDisputeByBookingIdAsync(userId, role, bookingId);
            return Ok(ApiResponse<DisputeDetailResponse?>.Ok(result, "Lấy thông tin khiếu nại thành công."));
        }

        // ════════════════════════════════════════════════════════════════════════
        //  2. CHUYÊN GIA: GỬI BẰNG CHỨNG PHẢN HỒI (MỤC 6.B BƯỚC 3)
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Chuyên gia gửi phản hồi / giải trình khiếu nại trong vòng 24 giờ (Đặc tả 7 - Mục 6.B Bước 3)
        /// POST /api/v1/expert/disputes/{id}/respond
        /// </summary>
        [HttpPost("api/v1/expert/disputes/{id:guid}/respond")]
        [ProducesResponseType(typeof(ApiResponse<DisputeDetailResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ExpertRespondDispute(
            [FromRoute] Guid id,
            [FromBody] ExpertRespondDisputeRequest request)
        {
            var expertUserId = GetCurrentUserId();
            var result = await _service.ExpertRespondDisputeAsync(expertUserId, id, request);
            return Ok(ApiResponse<DisputeDetailResponse>.Ok(result, "Đã gửi giải trình phản biện khiếu nại thành công. Hồ sơ đã được chuyển tới Admin để phán quyết."));
        }

        // ════════════════════════════════════════════════════════════════════════
        //  3. CHUNG: XEM CHI TIẾT & DANH SÁCH VỤ VIỆC TRANH CHẤP
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Xem chi tiết một vụ việc tranh chấp theo ID
        /// GET /api/v1/disputes/{id}
        /// </summary>
        [HttpGet("api/v1/disputes/{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<DisputeDetailResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetDisputeById([FromRoute] Guid id)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();
            var result = await _service.GetDisputeByIdAsync(userId, role, id);
            return Ok(ApiResponse<DisputeDetailResponse>.Ok(result, "Lấy thông tin chi tiết khiếu nại thành công."));
        }

        /// <summary>
        /// Lấy danh sách các vụ việc khiếu nại tranh chấp (Admin xem tất cả; Khách hàng/Chuyên gia chỉ xem ca của mình)
        /// GET /api/v1/disputes?status=PENDING_EXPERT_RESPONSE&page=1&size=10
        /// </summary>
        [HttpGet("api/v1/disputes")]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<DisputeListItemResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetDisputes(
            [FromQuery] string? status = null,
            [FromQuery] int page = 1,
            [FromQuery] int size = 10)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();
            var result = await _service.GetDisputesAsync(userId, role, status, page, size);
            return Ok(ApiResponse<PagedResult<DisputeListItemResponse>>.Ok(result, "Lấy danh sách khiếu nại thành công."));
        }

        // ════════════════════════════════════════════════════════════════════════
        //  4. ADMIN: PHÁN QUYẾT TRANH CHẤP (MỤC 6.B BƯỚC 5)
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Quản trị viên đưa ra phán quyết cuối cùng giải quyết tranh chấp (Đặc tả 7 - Mục 6.B Bước 5)
        /// Hỗ trợ 3 hướng phán quyết: FULL_REFUND (Lỗi chuyên gia - hoàn 100%), SETTLE_EXPERT (Bác khiếu nại - giải ngân đủ), PARTIAL (Lỗi một phần - chia theo %).
        /// POST /api/v1/admin/disputes/{id}/resolve
        /// </summary>
        [HttpPost("api/v1/admin/disputes/{id:guid}/resolve")]
        [ProducesResponseType(typeof(ApiResponse<DisputeDetailResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AdminResolveDispute(
            [FromRoute] Guid id,
            [FromBody] AdminResolveDisputeRequest request)
        {
            var role = GetCurrentUserRole();
            if (!string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase))
            {
                throw new ForbiddenException("FORBIDDEN", "Chỉ quản trị viên hệ thống (Admin) mới có quyền đưa ra phán quyết tranh chấp.");
            }

            var adminId = GetCurrentUserId();
            var result = await _service.AdminResolveDisputeAsync(adminId, id, request);
            return Ok(ApiResponse<DisputeDetailResponse>.Ok(result, "Phán quyết tranh chấp và thực thi giao dịch tài chính hoàn tất thành công."));
        }
    }
}
