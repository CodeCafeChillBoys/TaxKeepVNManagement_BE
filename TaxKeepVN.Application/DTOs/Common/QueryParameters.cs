namespace TaxKeepVN.Application.DTOs.Common
{
    /// <summary>
    /// Query parameters dùng chung cho tất cả List API theo chuẩn API Guidelines.
    /// </summary>
    public class QueryParameters
    {
        private const int MaxPageSize = 100;
        private int _pageSize = 10;

        public int Page { get; set; } = 1;

        public int Size
        {
            get => _pageSize;
            set => _pageSize = value > MaxPageSize ? MaxPageSize : value < 1 ? 10 : value;
        }

        public string? Search { get; set; }
        public string? Sort { get; set; }
    }

    /// <summary>
    /// Query parameters đặc thù cho Notifications — thêm filter theo trạng thái đọc.
    /// </summary>
    public class NotificationQueryParameters : QueryParameters
    {
        /// <summary>Lọc theo trạng thái đọc. null = tất cả, true = đã đọc, false = chưa đọc.</summary>
        public bool? IsRead { get; set; }
    }
}
