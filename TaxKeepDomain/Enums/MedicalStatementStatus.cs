namespace TaxKeepVN.Domain.Enums
{
    /// <summary>
    /// Trạng thái của Bảng kê chi phí KCB (Medical Statement)
    /// </summary>
    public static class MedicalStatementStatus
    {
        /// <summary>Mới tải lên, chờ xử lý AI</summary>
        public const string UPLOADED = "UPLOADED";

        /// <summary>AI đã bóc tách và đối chiếu thành công</summary>
        public const string EXTRACTED = "EXTRACTED";

        /// <summary>Bóc tách thất bại hoặc đối chiếu chéo không hợp lệ</summary>
        public const string FAILED = "FAILED";

        /// <summary>Người dùng hoặc kế toán đã duyệt và xác nhận</summary>
        public const string CONFIRMED = "CONFIRMED";
    }
}
