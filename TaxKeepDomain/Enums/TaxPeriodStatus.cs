namespace TaxKeepVN.Domain.Enums
{
    // <summary>
    /// Trạng thái của kỳ quyết toán/kê khai thuế TNCN
    /// </summary>
    public static class TaxPeriodStatus
    {
        /// <summary>Đang tạo nháp, đang thu thập chứng từ hóa đơn</summary>
        public const string DRAFT = "DRAFT";
        /// <summary>Đã nộp hồ sơ quyết toán lên cơ quan thuế và bị khóa chỉnh sửa</summary>
        public const string SUBMITTED = "SUBMITTED";
    }
}