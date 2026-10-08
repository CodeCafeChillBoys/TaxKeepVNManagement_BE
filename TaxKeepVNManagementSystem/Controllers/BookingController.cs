using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaxKeepVN.Application.DTOs.Bookings;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Experts;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Enums;

namespace TaxKeepVNManagementSystem.Controllers
{
    /// <summary>
    /// Controller Đặt lịch tư vấn thuế dành cho Người dùng / Khách hàng (Đặc tả 3)
    /// </summary>
    [ApiController]
    [Route("api/v1/bookings")]
    public class BookingController : ControllerBase
    {
        private readonly IBookingService _bookingService;

        public BookingController(IBookingService bookingService)
        {
            _bookingService = bookingService;
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
        /// Lấy lịch rảnh làm việc khả dụng của chuyên gia để khách hàng đặt lịch (Đặc tả 3 - Mục 5 Luồng chính)
        /// Tự động loại bỏ các slot vi phạm quy định đặt trước tối thiểu (Minimum Lead-time 4h) và các slot đang bị người khác tạm giữ.
        /// GET /api/v1/bookings/experts/{expertProfileId}/calendar?fromDate=2026-10-09&days=14
        /// </summary>
        [HttpGet("experts/{expertProfileId:guid}/calendar")]
        [ProducesResponseType(typeof(ApiResponse<List<ExpertAvailableSlotDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetExpertBookingCalendar(
            [FromRoute] Guid expertProfileId,
            [FromQuery] DateOnly? fromDate = null,
            [FromQuery] int days = 14)
        {
            var slots = await _bookingService.GetAvailableSlotsForBookingAsync(expertProfileId, fromDate, days);
            return Ok(ApiResponse<List<ExpertAvailableSlotDto>>.Ok(slots, "Lấy lịch làm việc khả dụng của chuyên gia thành công."));
        }

        /// <summary>
        /// Xem trước tóm tắt thông tin đặt lịch & chi phí (Dry-run preview) (Đặc tả 3 - Mục 5 Luồng chính)
        /// Kiểm tra tính hợp lệ, lead-time, trùng lịch và tính toán chi phí phiên tư vấn trước khi xác nhận.
        /// POST /api/v1/bookings/preview
        /// </summary>
        [HttpPost("preview")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResponse<BookingPreviewResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> PreviewBooking([FromBody] BookingPreviewRequest request)
        {
            var userId = GetCurrentUserId();
            var result = await _bookingService.PreviewBookingAsync(userId, request);
            return Ok(ApiResponse<BookingPreviewResponse>.Ok(result, "Tải thông tin xem trước buổi hẹn thành công."));
        }

        /// <summary>
        /// Khởi tạo lịch hẹn tư vấn và tạm khóa slot 10 phút (Hold slot) (Đặc tả 3 - Mục 5 Luồng chính)
        /// Nhận multipart/form-data gồm thông tin tư vấn và tệp chứng từ đính kèm (tối đa 5 tệp, tổng dung lượng &lt;= 25MB).
        /// Slot được tạm khóa trong 10 phút chờ thanh toán. Trả về BookingId và số giây đếm ngược để chuyển tiếp sang thanh toán.
        /// POST /api/v1/bookings
        /// </summary>
        [HttpPost]
        [Authorize]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(ApiResponse<BookingDetailResponse>), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateBooking([FromForm] CreateBookingRequest request)
        {
            var userId = GetCurrentUserId();
            var result = await _bookingService.CreateBookingAsync(userId, request);
            return StatusCode(StatusCodes.Status201Created, 
                ApiResponse<BookingDetailResponse>.Ok(result, "Khởi tạo lịch hẹn thành công. Khung giờ đã được tạm giữ 10 phút chờ thanh toán."));
        }

        /// <summary>
        /// Xem thông tin chi tiết một ca tư vấn theo Booking ID (Đặc tả 3)
        /// Chỉ khách hàng tạo lịch, chuyên gia được phân công hoặc Admin mới có quyền xem.
        /// GET /api/v1/bookings/{id}
        /// </summary>
        [HttpGet("{id:guid}")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResponse<BookingDetailResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetBookingDetail([FromRoute] Guid id)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();
            var result = await _bookingService.GetBookingByIdAsync(id, userId, role);
            return Ok(ApiResponse<BookingDetailResponse>.Ok(result, "Lấy thông tin chi tiết lịch hẹn thành công."));
        }

        /// <summary>
        /// Lấy danh sách lịch hẹn của tôi (Khách hàng) (Đặc tả 3)
        /// Hỗ trợ phân trang và lọc theo trạng thái (PENDING_PAYMENT, CONFIRMED, COMPLETED, v.v.).
        /// GET /api/v1/bookings/my-bookings?status=CONFIRMED&amp;page=1&amp;size=10
        /// </summary>
        [HttpGet("my-bookings")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<BookingListItemResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyBookings(
            [FromQuery] BookingStatus? status = null,
            [FromQuery] int page = 1,
            [FromQuery] int size = 10)
        {
            var userId = GetCurrentUserId();
            var result = await _bookingService.GetMyBookingsAsync(userId, status, page, size);
            return Ok(ApiResponse<PagedResult<BookingListItemResponse>>.Ok(result, "Lấy danh sách lịch hẹn của bạn thành công."));
        }

        /// <summary>
        /// Khách hàng chủ động hủy lịch hẹn (Đặc tả 3)
        /// POST /api/v1/bookings/{id}/cancel
        /// </summary>
        [HttpPost("{id:guid}/cancel")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResponse<BookingDetailResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CancelBooking([FromRoute] Guid id, [FromBody] string? reason = null)
        {
            var userId = GetCurrentUserId();
            var result = await _bookingService.CancelBookingAsync(userId, id, reason ?? "Khách hàng chủ động hủy.");
            return Ok(ApiResponse<BookingDetailResponse>.Ok(result, "Hủy lịch hẹn thành công."));
        }

        /// <summary>
        /// Lấy đường dẫn tải an toàn cho tệp chứng từ đính kèm của buổi tư vấn
        /// GET /api/v1/bookings/{id}/attachments/{attachmentId}/download
        /// </summary>
        [HttpGet("{id:guid}/attachments/{attachmentId:guid}/download")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DownloadAttachment([FromRoute] Guid id, [FromRoute] Guid attachmentId)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();
            var downloadUrl = await _bookingService.GetAttachmentDownloadUrlAsync(id, attachmentId, userId, role);
            return Ok(ApiResponse<string>.Ok(downloadUrl, "Lấy đường dẫn tải tệp chứng từ thành công."));
        }
    }
}
