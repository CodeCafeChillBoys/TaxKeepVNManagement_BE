using Microsoft.AspNetCore.Mvc;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Experts;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVNManagementSystem.Controllers
{
    /// <summary>
    /// Controller tìm kiếm, lọc và xem thông tin chuyên gia tư vấn thuế (Đặc tả 2)
    /// </summary>
    [ApiController]
    [Route("api/v1/experts")]
    public class ExpertController : ControllerBase
    {
        private readonly IExpertSearchService _expertSearchService;

        public ExpertController(IExpertSearchService expertSearchService)
        {
            _expertSearchService = expertSearchService;
        }

        /// <summary>
        /// Tìm kiếm, lọc và phân trang danh sách chuyên gia (Đặc tả 2 - Mục 5 Luồng chính)
        /// Hỗ trợ lọc đa tiêu chí: Từ khóa, Lĩnh vực chuyên môn, Mức phí, Rating, Khung thời gian rảnh.
        /// GET /api/v1/experts?keyword=Thue&specializationIds=1&minRating=4.0&timeFilter=Today&sortBy=Recommended&page=1&size=10
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<ExpertSearchResultDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> SearchExperts([FromQuery] ExpertSearchQueryParameters query)
        {
            var result = await _expertSearchService.SearchExpertsAsync(query);
            return Ok(ApiResponse<ExpertSearchResultDto>.Ok(result, "Tìm kiếm và lấy danh sách chuyên gia thành công."));
        }

        /// <summary>
        /// Lấy danh sách chuyên gia nổi bật / đề xuất trang chủ (Đặc tả 2 - Mục 5 Bước 2)
        /// Ưu tiên theo thuật toán ranking: Rating cao, nhiều ca hoàn tất và có slot trống sẵn sàng.
        /// GET /api/v1/experts/featured?count=6
        /// </summary>
        [HttpGet("featured")]
        [ProducesResponseType(typeof(ApiResponse<List<ExpertListItemResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetFeaturedExperts([FromQuery] int count = 6)
        {
            var result = await _expertSearchService.GetFeaturedExpertsAsync(count);
            return Ok(ApiResponse<List<ExpertListItemResponse>>.Ok(result, "Lấy danh sách chuyên gia nổi bật thành công."));
        }

        /// <summary>
        /// Xem chi tiết thông tin công khai của chuyên gia (Đặc tả 2 - Mục 5 Luồng chính)
        /// Đã che mờ các thông tin nhạy cảm: CCCD, SĐT, Email cá nhân (BR-03).
        /// GET /api/v1/experts/{id}
        /// </summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<ExpertDetailResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetExpertDetailById([FromRoute] Guid id)
        {
            var result = await _expertSearchService.GetExpertDetailByIdAsync(id);
            return Ok(ApiResponse<ExpertDetailResponse>.Ok(result, "Lấy thông tin chi tiết chuyên gia thành công."));
        }

        /// <summary>
        /// Lấy danh sách đánh giá, nhận xét của khách hàng về chuyên gia (Đặc tả 2 - Mục 5)
        /// Hỗ trợ phân trang và lọc theo số sao đánh giá (1 - 5 sao).
        /// GET /api/v1/experts/{id}/reviews?page=1&size=10&rating=5
        /// </summary>
        [HttpGet("{id:guid}/reviews")]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<ExpertPublicReviewDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetExpertReviews(
            [FromRoute] Guid id,
            [FromQuery] int page = 1,
            [FromQuery] int size = 10,
            [FromQuery] int? rating = null)
        {
            var result = await _expertSearchService.GetExpertReviewsAsync(id, page, size, rating);
            return Ok(ApiResponse<PagedResult<ExpertPublicReviewDto>>.Ok(result, "Lấy danh sách đánh giá của chuyên gia thành công."));
        }

        /// <summary>
        /// Lấy danh sách khung giờ rảnh sắp tới của chuyên gia để khách hàng đặt lịch (Đặc tả 2 - Mục 5)
        /// Hỗ trợ lọc từ ngày cụ thể (fromDate) và số ngày tới (days, mặc định 14 ngày, tối đa 30 ngày).
        /// GET /api/v1/experts/{id}/upcoming-slots?fromDate=2026-10-05&days=14
        /// </summary>
        [HttpGet("{id:guid}/upcoming-slots")]
        [ProducesResponseType(typeof(ApiResponse<List<ExpertAvailableSlotDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetExpertUpcomingSlots(
            [FromRoute] Guid id,
            [FromQuery] DateOnly? fromDate = null,
            [FromQuery] int days = 14)
        {
            var result = await _expertSearchService.GetExpertUpcomingSlotsAsync(id, fromDate, days);
            return Ok(ApiResponse<List<ExpertAvailableSlotDto>>.Ok(result, "Lấy danh sách lịch rảnh sắp tới của chuyên gia thành công."));
        }
    }
}
