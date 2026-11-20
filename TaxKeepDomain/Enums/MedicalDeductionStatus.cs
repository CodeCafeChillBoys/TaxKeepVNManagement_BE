namespace TaxKeepVN.Domain.Enums
{
    /// <summary>
    /// Trạng thái giảm trừ thuế TNCN của từng dòng chi phí trên Bảng kê chi phí KCB
    /// </summary>
    public static class MedicalDeductionStatus
    {
        /// <summary>Hợp lệ để tính giảm trừ thuế TNCN</summary>
        public const string ELIGIBLE = "ELIGIBLE";

        /// <summary>Bị loại trừ (thẩm mỹ, làm đẹp, thực phẩm chức năng, tiện ích cá nhân...)</summary>
        public const string EXCLUDED = "EXCLUDED";

        /// <summary>Cần người dùng hoặc kế toán kiểm tra lại</summary>
        public const string NEED_REVIEW = "NEED_REVIEW";
    }
}
