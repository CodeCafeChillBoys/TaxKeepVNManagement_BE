using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Experts;

namespace TaxKeepVN.Application.Service.Interfaces
{
    /// <summary>
    /// Service xử lý Tìm kiếm, lọc và xem chi tiết thông tin chuyên gia (Đặc tả 2)
    /// </summary>
    public interface IExpertSearchService
    {
        /// <summary>
        /// Tìm kiếm và lọc danh sách chuyên gia với đầy đủ tiêu chí: Từ khóa, Lĩnh vực, Mức phí, Rating, Khung giờ rảnh
        /// </summary>
        Task<ExpertSearchResultDto> SearchExpertsAsync(ExpertSearchQueryParameters query);

        /// <summary>
        /// Lấy danh sách chuyên gia nổi bật / được đề xuất trang chủ
        /// </summary>
        Task<List<ExpertListItemResponse>> GetFeaturedExpertsAsync(int count = 6);

        /// <summary>
        /// Xem chi tiết hồ sơ chuyên gia (đã che mờ thông tin riêng tư, kiểm tra trạng thái hoạt động)
        /// </summary>
        Task<ExpertDetailResponse> GetExpertDetailByIdAsync(Guid expertProfileId);

        /// <summary>
        /// Lấy danh sách đánh giá, nhận xét của khách hàng về chuyên gia (có phân trang và lọc theo sao)
        /// </summary>
        Task<PagedResult<ExpertPublicReviewDto>> GetExpertReviewsAsync(Guid expertProfileId, int page = 1, int size = 10, int? rating = null);

        /// <summary>
        /// Lấy danh sách khung giờ rảnh sắp tới của chuyên gia để chuẩn bị đặt lịch
        /// </summary>
        Task<List<ExpertAvailableSlotDto>> GetExpertUpcomingSlotsAsync(Guid expertProfileId, DateOnly? fromDate = null, int days = 14);
    }
}
