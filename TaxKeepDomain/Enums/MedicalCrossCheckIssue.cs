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

        /// <summary>Người bệnh không phải người nộp thuế hoặc người phụ thuộc hợp lệ (Khoản 3b NĐ 253/2026/NĐ-CP)</summary>
        public const string PATIENT_NOT_TAXPAYER_OR_DEPENDENT = "PATIENT_NOT_TAXPAYER_OR_DEPENDENT";

        /// <summary>Ngày sinh người bệnh trên bảng kê không khớp hồ sơ NNT/NPT</summary>
        public const string PATIENT_DOB_MISMATCH = "PATIENT_DOB_MISMATCH";

        /// <summary>Giới tính OCR trên bảng kê không thuộc danh mục hợp lệ</summary>
        public const string PATIENT_GENDER_INVALID = "PATIENT_GENDER_INVALID";

        /// <summary>Tên cơ sở khám chữa bệnh không khớp đơn vị bán trên hóa đơn</summary>
        public const string HOSPITAL_NAME_MISMATCH = "HOSPITAL_NAME_MISMATCH";

        /// <summary>Ngày vào/ra viện không hợp lệ hoặc không phù hợp hóa đơn</summary>
        public const string TREATMENT_DATE_INVALID = "TREATMENT_DATE_INVALID";

        /// <summary>Một hoặc nhiều field định dạng trong medicalHeader không hợp lệ</summary>
        public const string HEADER_FIELD_INVALID = "HEADER_FIELD_INVALID";
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
        public const string EXCLUDED_VOLUNTARY_SCREENING = "EXCLUDED_VOLUNTARY_SCREENING";
        public const string EXCLUDED_OUT_OF_COVERAGE = "EXCLUDED_OUT_OF_COVERAGE";
        public const string EXCLUDED_VIP_ROOM = "EXCLUDED_VIP_ROOM";
        public const string NEED_REVIEW_UNREADABLE = "NEED_REVIEW_UNREADABLE";
    }
}
