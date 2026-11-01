using System.Collections.Generic;

namespace TaxKeepVN.Application.DTOs.Common
{
    /// <summary>
    /// Wrapper cho tất cả danh sách có phân trang theo chuẩn API Guidelines mục 2.2.
    /// </summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();

        public PaginationMeta Pagination { get; set; } = new();
    }
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]

    public class PaginationMeta
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
    }
}
