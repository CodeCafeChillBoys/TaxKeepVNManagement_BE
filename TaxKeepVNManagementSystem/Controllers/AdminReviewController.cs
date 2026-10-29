using System;
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
    /// Controller Kiểm duyệt đánh giá dành cho Quản trị viên / CSKH (Đặc tả: Review tiêu cực & moderation)
    /// </summary>
    [ApiController]
    [Route("api/v1/admin/reviews")]
    [Authorize]
    [Produces("application/json")]
    public class AdminReviewController : ControllerBase
    {
        private readonly IReviewService _reviewService;

        public AdminReviewController(IReviewService reviewService)
        {
            _reviewService = reviewService;
        }

        /// <summary>
        /// Lấy danh sách đánh giá bị gắn cờ tiêu cực (rating thấp dưới ngưỡng cấu hình) để CSKH/Admin xem xét
        /// GET /api/v1/admin/reviews/flagged?page=1&size=20
        /// </summary>
        [HttpGet("flagged")]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<ReviewDetailResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetFlaggedReviews(
            [FromQuery] int page = 1,
            [FromQuery] int size = 20)
        {
            var result = await _reviewService.GetFlaggedReviewsAsync(page, size);
            return Ok(ApiResponse<PagedResult<ReviewDetailResponse>>.Ok(result, "Lấy danh sách đánh giá cần kiểm duyệt thành công."));
        }

        /// <summary>
        /// Admin ẩn hoặc hiện một đánh giá vi phạm tiêu chuẩn cộng đồng
        /// Tự động tính toán lại điểm rating trung bình của chuyên gia sau khi ẩn/hiện.
        /// PATCH /api/v1/admin/reviews/{reviewId}/publish?isPublished=false
        /// </summary>
        [HttpPatch("{reviewId:guid}/publish")]
        [ProducesResponseType(typeof(ApiResponse<ReviewDetailResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> TogglePublishReview(
            [FromRoute] Guid reviewId,
            [FromQuery] bool isPublished)
        {
            var result = await _reviewService.TogglePublishReviewAsync(reviewId, isPublished);
            var actionMsg = isPublished ? "công khai lại" : "ẩn";
            return Ok(ApiResponse<ReviewDetailResponse>.Ok(result, $"Đã {actionMsg} đánh giá thành công. Điểm số chuyên gia đã được cập nhật."));
        }
    }
}
