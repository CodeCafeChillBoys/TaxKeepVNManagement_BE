namespace TaxKeepVN.Application.Constants
{
    public static class MedicalClaimStatus
    {
        /// <summary>Hồ sơ đủ điều kiện giảm trừ (có hóa đơn + bảng kê hợp lệ, đã giải quyết các dòng)</summary>
        public const string ELIGIBLE = "ELIGIBLE";

        /// <summary>Chưa có Bảng kê chi phí KCB gắn kèm theo hóa đơn y tế (Khoản 3a Điều 49 NĐ 253)</summary>
        public const string MISSING_STATEMENT = "MISSING_STATEMENT";

        /// <summary>Hồ sơ còn dòng chi phí cần người dùng kiểm tra hoặc xác nhận</summary>
        public const string NEED_REVIEW = "NEED_REVIEW";

        /// <summary>Hồ sơ bị từ chối giảm trừ (đã được bồi hoàn, sai người bệnh, cơ sở nước ngoài...)</summary>
        public const string REJECTED = "REJECTED";
    }

    public static class MedicalDeductionStatus
    {
        public const string ELIGIBLE = "ELIGIBLE";
        public const string EXCLUDED = "EXCLUDED";
        public const string NEED_REVIEW = "NEED_REVIEW";
    }

    public static class MedicalDeductionReason
    {
        /// <summary>Phần cùng chi trả trong phạm vi BHYT (cột 11) - ĐƯỢC GIẢM TRỪ</summary>
        public const string IN_COVERAGE_COPAY = "IN_COVERAGE_COPAY";

        /// <summary>Ngoài danh mục BHYT (cột 8 = 0, hoặc không thẻ mà không có trong danh mục) - LOẠI TRỪ</summary>
        public const string OUT_OF_COVERAGE = "OUT_OF_COVERAGE";

        /// <summary>Mục bị loại theo cấu hình admin cho trường hợp không thẻ (VD: Nhóm 12 Dịch vụ khác)</summary>
        public const string NON_MEDICAL_SERVICE = "NON_MEDICAL_SERVICE";

        /// <summary>Phần vượt giá BHYT (cột 4 > cột 5) - Mặc định không tính, user có thể bật</summary>
        public const string PRICE_DIFFERENCE_SELF_PAY = "PRICE_DIFFERENCE_SELF_PAY";

        /// <summary>Phần BHYT không trả do tỷ lệ thanh toán < 100% (cột 8 hoặc 6) - Mặc định không tính</summary>
        public const string PARTIAL_RATE_SELF_PAY = "PARTIAL_RATE_SELF_PAY";

        /// <summary>Có cột 13 nhưng không rõ lý do chênh lệch - User cần kiểm tra</summary>
        public const string SELF_PAY_UNKNOWN = "SELF_PAY_UNKNOWN";

        /// <summary>Không dùng thẻ BHYT, khớp danh mục chưa chắc chắn</summary>
        public const string NO_CARD_LOOKUP_REQUIRED = "NO_CARD_LOOKUP_REQUIRED";

        /// <summary>Không dùng thẻ, có trong danh mục nhưng không có đơn giá BH để tách phần vượt</summary>
        public const string NO_CARD_PRICE_UNKNOWN = "NO_CARD_PRICE_UNKNOWN";

        /// <summary>Phần do nguồn khác chi trả (cột 12) - Không tính giảm trừ</summary>
        public const string OTHER_SOURCE_PAID = "OTHER_SOURCE_PAID";

        /// <summary>Số liệu mờ, không đọc rõ</summary>
        public const string UNREADABLE = "UNREADABLE";

        /// <summary>Người dùng đã can thiệp điều chỉnh trạng thái dòng</summary>
        public const string USER_OVERRIDE = "USER_OVERRIDE";
    }

    public static class MedicalRejectReason
    {
        public const string REIMBURSED = "REIMBURSED";
        public const string PATIENT_NOT_ELIGIBLE = "PATIENT_NOT_ELIGIBLE";
        public const string FOREIGN_FACILITY = "FOREIGN_FACILITY";
    }

    public static class MedicalDocTypes
    {
        public const string MedicalReceipt = "MEDICAL_RECEIPT";
        public const string MedicalCostStatement = "MEDICAL_COST_STATEMENT";
    }

    public static class MedicalConfigKeys
    {
        public const string MedicalRequireStatement = "MEDICAL_REQUIRE_STATEMENT";
        public const string MedicalNoCardExcludedGroupCodes = "MEDICAL_NO_CARD_EXCLUDED_GROUP_CODES";
        public const string MedicalStatementReminderDays = "MEDICAL_STATEMENT_REMINDER_DAYS";
        public const string MedicalStatementAmountTolerance = "MEDICAL_STATEMENT_AMOUNT_TOLERANCE";
        public const string MedicalCatalogMatchMinScore = "MEDICAL_CATALOG_MATCH_MIN_SCORE";
        public const string PitMedicalMaxYearly = "PIT_MEDICAL_MAX_YEARLY";
    }

    public static class MedicalErrorCodes
    {
        public const string MedicalStatementRequired = "ERR_MEDICAL_STATEMENT_REQUIRED";
        public const string StatementInvoiceMismatch = "ERR_STATEMENT_INVOICE_MISMATCH";
        public const string PatientNotTaxpayerOrDependent = "ERR_PATIENT_NOT_TAXPAYER_OR_DEPENDENT";
        public const string NotMedicalStatement = "ERR_NOT_MEDICAL_STATEMENT";
        public const string MedicalItemsNeedReview = "ERR_MEDICAL_ITEMS_NEED_REVIEW";
        public const string MedicalReceiptNotFound = "ERR_MEDICAL_RECEIPT_NOT_FOUND";
        public const string MedicalStatementNotFound = "ERR_MEDICAL_STATEMENT_NOT_FOUND";
    }

    public static class MedicalErrorMessages
    {
        public const string MedicalStatementRequired = "Chi phí y tế bắt buộc phải có Bảng kê chi phí KCB (Mẫu 01/KBCB) kèm theo mới đủ điều kiện giảm trừ thuế TNCN.";
        public const string StatementInvoiceMismatch = "Thông tin trên Bảng kê chi phí KCB không khớp với Hóa đơn (sai lệch tên bệnh nhân, cơ sở KCB hoặc số tiền vượt ngưỡng cho phép).";
        public const string PatientNotTaxpayerOrDependent = "Người bệnh trên chứng từ y tế không khớp với người nộp thuế hoặc bất kỳ người phụ thuộc nào đang có hiệu lực.";
        public const string NotMedicalStatement = "Tài liệu tải lên không phải là Bảng kê chi phí khám bệnh, chữa bệnh hợp lệ.";
        public const string MedicalItemsNeedReview = "Hồ sơ y tế còn các dòng chi phí chưa được xác nhận hoặc cần rà soát.";
        public const string MedicalReceiptNotFound = "Không tìm thấy hóa đơn y tế chỉ định.";
        public const string MedicalStatementNotFound = "Không tìm thấy bảng kê chi phí KCB chỉ định.";
    }
}
