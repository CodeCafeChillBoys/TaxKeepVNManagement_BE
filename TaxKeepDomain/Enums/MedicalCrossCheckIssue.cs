namespace TaxKeepVN.Domain.Enums
{
    /// <summary>
    /// Các mã lỗi đối chiếu chéo (Cross-Check Issues) giữa Bảng kê KCB và Hóa đơn viện phí
    /// </summary>
    public static class MedicalCrossCheckIssue
    {
        /// <summary>Họ tên người bệnh trên bảng kê không khớp hóa đơn</summary>
        public const string PATIENT_NAME_MISMATCH = "PATIENT_NAME_MISMATCH";

        /// <summary>Số CCCD người bệnh trên bảng kê không khớp hóa đơn</summary>
        public const string PATIENT_IDCARD_MISMATCH = "PATIENT_IDCARD_MISMATCH";

        /// <summary>Số tiền người bệnh thực trả không khớp tổng tiền hóa đơn</summary>
        public const string TOTAL_AMOUNT_MISMATCH = "TOTAL_AMOUNT_MISMATCH";

        /// <summary>Mục dịch vụ y tế trên hóa đơn không tìm thấy trong bảng kê</summary>
        public const string SERVICE_ITEM_MISMATCH = "SERVICE_ITEM_MISMATCH";
    }

    /// <summary>
    /// Các mã lý do phân loại giảm trừ y tế
    /// </summary>
    public static class MedicalDeductionReason
    {
        public const string IN_COVERAGE_COPAY = "IN_COVERAGE_COPAY";
        public const string TREATMENT_EXPENSE_SELF_PAY = "TREATMENT_EXPENSE_SELF_PAY";
        public const string EXCLUDED_COSMETIC_SURGERY = "EXCLUDED_COSMETIC_SURGERY";
        public const string EXCLUDED_SKIN_CARE_SPA = "EXCLUDED_SKIN_CARE_SPA";
        public const string EXCLUDED_FUNCTIONAL_FOOD = "EXCLUDED_FUNCTIONAL_FOOD";
        public const string EXCLUDED_VIP_ROOM = "EXCLUDED_VIP_ROOM";
        public const string NEED_REVIEW_UNREADABLE = "NEED_REVIEW_UNREADABLE";
    }
}
