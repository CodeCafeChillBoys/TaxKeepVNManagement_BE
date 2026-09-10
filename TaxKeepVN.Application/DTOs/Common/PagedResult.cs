using System.Collections.Generic;

namespace TaxKeepVN.Application.DTOs.Common
{
    /// <summary>
    /// Wrapper cho tất cả danh sách có phân trang theo chuẩn API Guidelines mục 2.2.
    /// </summary>
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();

        public PaginationMeta Pagination { get; set; } = new();
    }

    public class PaginationMeta
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
    }
}
