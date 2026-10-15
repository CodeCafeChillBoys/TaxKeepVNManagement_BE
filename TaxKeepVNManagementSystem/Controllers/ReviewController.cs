using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.DTOs.Reviews;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVNManagementSystem.Controllers
{
    /// <summary>
    /// Controller Đánh giá và Phản hồi sau phiên tư vấn (Đặc tả: Đánh giá chất lượng và gửi phản hồi)
    /// </summary>
    [ApiController]
    public class ReviewController : ControllerBase
    {
        private readonly IReviewService _reviewService;

        public ReviewController(IReviewService reviewService)
        {
            _reviewService = reviewService;
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst("userId")?.Value 
                     ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }

        // ════════════════════════════════════════════════════════════════════════
        //  KHÁCH HÀNG (USER)
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Kiểm tra tính hợp lệ và thời hạn đánh giá của một ca tư vấn (Đặc tả 4 - Điều kiện trước)
        /// Trả về trạng thái canReview, đã đánh giá hay chưa, thời hạn deadline và số giây còn lại.
        /// GET /api/v1/bookings/{bookingId}/review-eligibility
        /// </summary>
        [Authorize]
        [HttpGet("api/v1/bookings/{bookingId:guid}/review-eligibility")]
        [ProducesResponseType(typeof(ApiResponse<ReviewEligibilityResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CheckReviewEligibility([FromRoute] Guid bookingId)
        {
            var userId = GetCurrentUserId();
            var result = await _reviewService.CheckReviewEligibilityAsync(userId, bookingId);
            return Ok(ApiResponse<ReviewEligibilityResponse>.Ok(result, "Kiểm tra điều kiện đánh giá thành công."));
        }

        /// <summary>
        /// Khách hàng gửi đánh giá và nhận xét sau khi phiên tư vấn kết thúc (Đặc tả 5 - Luồng chính)
        /// Chấm điểm tổng quan 1–5 sao, điểm tiêu chí chi tiết (tùy chọn), nhận xét, tùy chọn ẩn danh.
        /// Hệ thống tự động cập nhật trung bình rating chuyên gia và kích hoạt cảnh báo nếu đánh giá tiêu cực.
        /// POST /api/v1/bookings/{bookingId}/reviews
        /// </summary>
        [Authorize]
        [HttpPost("api/v1/bookings/{bookingId:guid}/reviews")]
        [ProducesResponseType(typeof(ApiResponse<ReviewDetailResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SubmitReview(
            [FromRoute] Guid bookingId,
            [FromBody] CreateReviewRequest request)
        {
            var userId = GetCurrentUserId();
            var result = await _reviewService.SubmitReviewAsync(userId, bookingId, request);
            return Ok(ApiResponse<ReviewDetailResponse>.Ok(result, "Gửi đánh giá và nhận xét thành công. Cảm ơn bạn đã phản hồi!"));
        }

        // ════════════════════════════════════════════════════════════════════════
        //  CHUYÊN GIA (EXPERT)
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Chuyên gia xem danh sách các đánh giá nhận được từ khách hàng (kèm cả review bị flag)
        /// GET /api/v1/expert/reviews?page=1&size=10
        /// </summary>
        [Authorize]
        [HttpGet("api/v1/expert/reviews")]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<ReviewDetailResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetMyReceivedReviews(
            [FromQuery] int page = 1,
            [FromQuery] int size = 10)
        {
            var expertUserId = GetCurrentUserId();
            var result = await _reviewService.GetMyReceivedReviewsAsync(expertUserId, page, size);
            return Ok(ApiResponse<PagedResult<ReviewDetailResponse>>.Ok(result, "Lấy danh sách đánh giá thành công."));
        }

        /// <summary>
        /// Chuyên gia gửi phản hồi công khai (Reply) cho một đánh giá của khách hàng (Đặc tả 5 - Luồng chính)
        /// Mỗi đánh giá chuyên gia chỉ được phản hồi tối đa 1 lần.
        /// POST /api/v1/expert/reviews/{reviewId}/reply
        /// </summary>
        [Authorize]
        [HttpPost("api/v1/expert/reviews/{reviewId:guid}/reply")]
        [ProducesResponseType(typeof(ApiResponse<ReviewDetailResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ReplyToReview(
            [FromRoute] Guid reviewId,
            [FromBody] ExpertReplyRequest request)
        {
            var expertUserId = GetCurrentUserId();
            var result = await _reviewService.ReplyToReviewAsync(expertUserId, reviewId, request);
            return Ok(ApiResponse<ReviewDetailResponse>.Ok(result, "Đã gửi phản hồi đánh giá thành công."));
        }

        // ════════════════════════════════════════════════════════════════════════
        //  CÔNG KHAI (PUBLIC)
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Lấy danh sách đánh giá công khai của một chuyên gia (chỉ các review đã duyệt/xuất bản)
        /// GET /api/v1/reviews/experts/{expertProfileId}?page=1&size=10
        /// </summary>
        [HttpGet("api/v1/reviews/experts/{expertProfileId:guid}")]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<ReviewListItemResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetExpertPublicReviews(
            [FromRoute] Guid expertProfileId,
            [FromQuery] int page = 1,
            [FromQuery] int size = 10)
        {
            var result = await _reviewService.GetExpertPublicReviewsAsync(expertProfileId, page, size);
            return Ok(ApiResponse<PagedResult<ReviewListItemResponse>>.Ok(result, "Lấy danh sách đánh giá chuyên gia thành công."));
        }
    }
}
