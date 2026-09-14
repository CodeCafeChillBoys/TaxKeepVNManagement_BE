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

    /// <summary>
    /// Query parameters đặc thù cho Income Sources — hỗ trợ lọc theo năm tính thuế và trạng thái.
    /// </summary>
    public class IncomeSourceQueryParameters : QueryParameters
    {
        /// <summary>Lọc theo năm tính thuế (ví dụ: 2026)</summary>
        public int? TaxYear { get; set; }

        /// <summary>Lọc theo trạng thái hoạt động</summary>
        public bool? IsActive { get; set; }
    }

    /// <summary>
    /// Query parameters cho Dependent Document Rules — hỗ trợ lọc theo nhóm đối tượng và trạng thái hoạt động.
    /// </summary>
    public class DependentRuleQueryParameters : QueryParameters
    {
        /// <summary>Lọc theo nhóm đối tượng (ví dụ: CHILD_UNDER_18, PARENT_RETIRED)</summary>
        public string? TargetGroup { get; set; }

        /// <summary>Lọc theo trạng thái hoạt động (true/false)</summary>
        public bool? IsActive { get; set; }
    }

    /// <summary>
    /// Query parameters cho Dependent List — hỗ trợ filter theo trạng thái hồ sơ và nhóm quan hệ.
    /// </summary>
    public class DependentQueryParameters : QueryParameters
    {
        /// <summary>
        /// Lọc theo trạng thái hồ sơ: PENDING_DOCUMENTS | ACTIVE | INACTIVE.
        /// Để trống = lấy tất cả trạng thái.
        /// </summary>
        public string? Status { get; set; }

        /// <summary>
        /// Lọc theo nhóm quan hệ: CHILD | SPOUSE | PARENT | OTHER_DEPENDENT.
        /// Để trống = lấy tất cả nhóm quan hệ.
        /// </summary>
        public string? Relationship { get; set; }
    }
}
