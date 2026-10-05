using System.Collections.Generic;
using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Application.DTOs.Experts
{
    /// <summary>
    /// Tham số tìm kiếm và bộ lọc chuyên gia (Đặc tả 2 - Mục 5)
    /// </summary>
    public class ExpertSearchQueryParameters
    {
        /// <summary>Từ khóa tìm kiếm (họ tên chuyên gia, chức danh, đơn vị công tác)</summary>
        public string? Keyword { get; set; }

        /// <summary>Lọc theo danh sách ID lĩnh vực chuyên môn (PIT, CIT, FINALIZATION...)</summary>
        public List<int>? SpecializationIds { get; set; }

        /// <summary>Lọc theo mức phí tối thiểu (VNĐ)</summary>
        public decimal? MinFee { get; set; }

        /// <summary>Lọc theo mức phí tối đa (VNĐ)</summary>
        public decimal? MaxFee { get; set; }

        /// <summary>Lọc theo số sao đánh giá tối thiểu (VD: 4.0 trở lên)</summary>
        public decimal? MinRating { get; set; }

        /// <summary>Lọc theo khung thời gian rảnh (Today, Tomorrow, ThisWeekend, Next7Days)</summary>
        public ExpertTimeFilter TimeFilter { get; set; } = ExpertTimeFilter.All;

        /// <summary>Tiêu chí sắp xếp kết quả (Recommended, RatingDesc, FeeAsc, FeeDesc, ExperienceDesc)</summary>
        public ExpertSortBy SortBy { get; set; } = ExpertSortBy.Recommended;

        /// <summary>Số trang hiện tại (bắt đầu từ 1)</summary>
        public int Page { get; set; } = 1;

        /// <summary>Số lượng phần tử trên mỗi trang (mặc định 10)</summary>
        public int Size { get; set; } = 10;
    }
}
