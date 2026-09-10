namespace TaxKeepVN.Application.DTOs.Common
{
    /// <summary>
    /// Query parameters dùng chung cho tất cả List API theo chuẩn API Guidelines.
    /// Hỗ trợ: Searching, Sorting, Paging.
    /// </summary>
    public class QueryParameters
    {
        private const int MaxPageSize = 100;
        private int _pageSize = 10;

        /// <summary>Số trang hiện tại (bắt đầu từ 1).</summary>
        public int Page { get; set; } = 1;

        /// <summary>Số bản ghi trên mỗi trang (tối đa 100).</summary>
        public int Size
        {
            get => _pageSize;
            set => _pageSize = value > MaxPageSize ? MaxPageSize : value < 1 ? 10 : value;
        }

        /// <summary>
        /// Từ khóa tìm kiếm. Ví dụ: ?search=nguyen
        /// </summary>
        public string? Search { get; set; }

        /// <summary>
        /// Trường sắp xếp. Dấu '-' đứng trước là giảm dần (DESC).
        /// Ví dụ: ?sort=companyName,-createdAt
        /// </summary>
        public string? Sort { get; set; }
    }
}
