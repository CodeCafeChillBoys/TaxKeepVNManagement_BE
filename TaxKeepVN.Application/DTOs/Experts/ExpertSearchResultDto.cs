using System.Collections.Generic;
using TaxKeepVN.Application.DTOs.Common;

namespace TaxKeepVN.Application.DTOs.Experts
{
    /// <summary>
    /// Kết quả tìm kiếm chuyên gia có phân trang kèm gợi ý thông minh
    /// </summary>
    public class ExpertSearchResultDto
    {
        public List<ExpertListItemResponse> Items { get; set; } = new();
        public PaginationMeta Pagination { get; set; } = new();
        public ExpertSearchSuggestionDto? Suggestion { get; set; }
    }
}
