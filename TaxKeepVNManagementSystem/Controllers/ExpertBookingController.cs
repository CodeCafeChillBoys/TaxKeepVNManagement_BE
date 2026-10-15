using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaxKeepVN.Application.DTOs.Bookings;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Enums;

namespace TaxKeepVNManagementSystem.Controllers
{
    /// <summary>
    /// Controller Quản lý và Xét duyệt ca tư vấn dành cho Chuyên gia (Đặc tả 3)
    /// </summary>
    [ApiController]
    [Route("api/v1/expert/bookings")]
    [Authorize]
    [Produces("application/json")]
    public class ExpertBookingController : ControllerBase
    {
        private readonly IBookingService _bookingService;
        private readonly IReviewService _reviewService;

        public ExpertBookingController(
            IBookingService bookingService,
            IReviewService reviewService)
        {
            _bookingService = bookingService;
            _reviewService = reviewService;
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst("userId")?.Value
                     ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }

        /// <summary>
        /// Lấy danh sách các ca tư vấn đang chờ chuyên gia phê duyệt tiếp nhận (SLA 2 giờ) (Đặc tả 3)
        /// Kèm đồng hồ đếm ngược thời gian còn lại của hạn 2 giờ, chủ đề và số lượng tài liệu đính kèm.
        /// GET /api/v1/expert/bookings/pending?page=1&amp;size=10
        /// </summary>
        [HttpGet("pending")]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<BookingListItemResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetPendingBookings(
            [FromQuery] int page = 1,
            [FromQuery] int size = 10)
        {
            var expertUserId = GetCurrentUserId();
            var result = await _bookingService.GetExpertPendingBookingsAsync(expertUserId, page, size);
            return Ok(ApiResponse<PagedResult<BookingListItemResponse>>.Ok(result, "Lấy danh sách ca tư vấn chờ duyệt thành công."));
        }

        /// <summary>
        /// Lấy toàn bộ danh sách lịch hẹn của chuyên gia (Đặc tả 3)
        /// Hỗ trợ lọc theo trạng thái (AWAITING_EXPERT_APPROVAL, CONFIRMED, COMPLETED, v.v.).
        /// GET /api/v1/expert/bookings?status=CONFIRMED&amp;page=1&amp;size=10
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<BookingListItemResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetExpertBookings(
            [FromQuery] BookingStatus? status = null,
            [FromQuery] int page = 1,
            [FromQuery] int size = 10)
        {
            var expertUserId = GetCurrentUserId();
            var result = await _bookingService.GetExpertBookingsAsync(expertUserId, status, page, size);
            return Ok(ApiResponse<PagedResult<BookingListItemResponse>>.Ok(result, "Lấy danh sách ca tư vấn của chuyên gia thành công."));
        }

        /// <summary>
        /// Chuyên gia đồng ý tiếp nhận ca tư vấn trong vòng 2 giờ (Đặc tả 3)
        /// Chuyển trạng thái sang CONFIRMED và chính thức chốt lịch hẹn.
        /// POST /api/v1/expert/bookings/{id}/accept
        /// </summary>
        [HttpPost("{id:guid}/accept")]
        [ProducesResponseType(typeof(ApiResponse<BookingDetailResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AcceptBooking([FromRoute] Guid id)
        {
            var expertUserId = GetCurrentUserId();
            var result = await _bookingService.AcceptBookingAsync(expertUserId, id);
            return Ok(ApiResponse<BookingDetailResponse>.Ok(result, "Bạn đã đồng ý tiếp nhận ca tư vấn thành công. Lịch hẹn chính thức được xác nhận."));
        }

        /// <summary>
        /// Chuyên gia từ chối tiếp nhận ca tư vấn trong vòng 2 giờ (Đặc tả 3)
        /// Bắt buộc nhập lý do từ chối. Hệ thống tự động kích hoạt hoàn tiền 100% cho khách hàng và giải phóng slot.
        /// POST /api/v1/expert/bookings/{id}/reject
        /// </summary>
        [HttpPost("{id:guid}/reject")]
        [ProducesResponseType(typeof(ApiResponse<BookingDetailResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RejectBooking([FromRoute] Guid id, [FromBody] RejectBookingRequest request)
        {
            var expertUserId = GetCurrentUserId();
            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "EXPERT";
            var fullName = User.FindFirst("fullName")?.Value;
            // Bạn có thể chọn 1 trong các cách lưu:
            // var cancelledBy = role.ToUpperInvariant(); // Lưu: "EXPERT"
            var cancelledBy = !string.IsNullOrEmpty(fullName) ? $"{role.ToUpperInvariant()}: {fullName}" : role.ToUpperInvariant();
            var result = await _bookingService.RejectBookingAsync(expertUserId, id, request, cancelledBy);
            return Ok(ApiResponse<BookingDetailResponse>.Ok(result, "Bạn đã từ chối ca tư vấn..."));
        }

        /// <summary>
        /// Chuyên gia bấm nút "Kết thúc phiên" sau khi phiên tư vấn hoàn tất
        /// Chuyển trạng thái sang COMPLETED, ghi nhận CompletedAt, tính ReviewDeadline và kích hoạt thông báo mời khách hàng đánh giá.
        /// POST /api/v1/expert/bookings/{id}/complete
        /// </summary>
        [HttpPost("{id:guid}/complete")]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CompleteBooking([FromRoute] Guid id)
        {
            var expertUserId = GetCurrentUserId();
            await _reviewService.CompleteSessionAsync(expertUserId, id);
            return Ok(ApiResponse<string>.Ok("Phiên tư vấn đã được đánh dấu hoàn thành thành công.", "Hoàn thành phiên tư vấn thành công."));
        }
    }
}
