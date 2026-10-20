using System.Collections.Generic;
using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Application.DTOs.SearchConfigs
{
    /// <summary>
    /// Bộ tiêu chí tìm kiếm & sắp xếp chuyên gia do Admin cấu hình
    /// </summary>
    public class ExpertSearchCriteriaConfigDto
    {
        /// <summary>
        /// Tiêu chí sắp xếp mặc định khi người dùng vào trang (VD: Recommended, RatingDesc, CompletedSessionsDesc...)
        /// </summary>
        public ExpertSortBy DefaultSortBy { get; set; } = ExpertSortBy.Recommended;

        /// <summary>
        /// Bắt buộc chuyên gia phải có ít nhất 1 chứng chỉ hành nghề đã được xác thực (Verified) mới được hiển thị
        /// </summary>
        public bool RequireVerifiedCertificate { get; set; } = false;

        /// <summary>
        /// Lĩnh vực chuyên môn ưu tiên đẩy lên đầu trong mùa vụ (VD: ID mảng PIT/Thuế TNCN)
        /// </summary>
        public List<int> FeaturedSpecializationIds { get; set; } = new();

        /// <summary>
        /// Cấu hình các trường dữ liệu được phép quét từ khóa tìm kiếm
        /// </summary>
        public SearchableFieldsConfig SearchableFields { get; set; } = new();

        /// <summary>
        /// Cấu hình trọng số tính điểm thuật toán đề xuất (Recommended)
        /// </summary>
        public RankingWeightsConfig RankingWeights { get; set; } = new();

        /// <summary>
        /// Danh sách các tiêu chí sắp xếp được phép hiển thị trên dropdown cho người dùng chọn
        /// </summary>
        public List<ExpertSortBy> AllowedSortOptions { get; set; } = new()
        {
            ExpertSortBy.Recommended,
            ExpertSortBy.RatingDesc,
            ExpertSortBy.CompletedSessionsDesc,
            ExpertSortBy.FeeAsc,
            ExpertSortBy.FeeDesc,
            ExpertSortBy.ExperienceDesc
        };
    }

    public class SearchableFieldsConfig
    {
        public bool SearchInFullName { get; set; } = true;         // Họ tên
        public bool SearchInJobTitle { get; set; } = true;         // Chức danh
        public bool SearchInCompanyName { get; set; } = true;      // Công ty
        public bool SearchInBio { get; set; } = true;              // Tiểu sử
        public bool SearchInSpecializations { get; set; } = true;  // Tên lĩnh vực chuyên môn
        public bool SearchInCertificates { get; set; } = true;     // Tên chứng chỉ hành nghề
    }

    public class RankingWeightsConfig
    {
        public double RatingWeight { get; set; } = 0.35;                   // 35% từ điểm đánh giá sao
        public double CompletedConsultationsWeight { get; set; } = 0.25;   // 25% từ số ca tư vấn hoàn thành
        public double ReviewsCountWeight { get; set; } = 0.15;             // 15% từ tổng số lượt đánh giá
        public double SlotAvailabilityWeight { get; set; } = 0.15;         // 15% từ lịch rảnh sớm
        public double VerifiedCertificateWeight { get; set; } = 0.10;      // 10% từ số chứng chỉ xác thực
    }
}
