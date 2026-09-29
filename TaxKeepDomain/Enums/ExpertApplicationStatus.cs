namespace TaxKeepVN.Domain.Enums
{
    /// <summary>
    /// Trạng thái của hồ sơ đăng ký chuyên gia (Mục 13 - Đặc tả 1.1)
    /// </summary>
    public enum ExpertApplicationStatus
    {
        /// <summary>Hồ sơ nháp, người dùng đang chỉnh sửa chưa gửi</summary>
        Draft,

        /// <summary>Hồ sơ đã gửi, đang chờ Admin/Reviewer thẩm định</summary>
        PendingReview,

        /// <summary>Admin yêu cầu bổ sung thông tin hoặc giấy tờ chứng chỉ</summary>
        NeedSupplement,

        /// <summary>Hồ sơ bị từ chối phê duyệt (có lý do kèm theo)</summary>
        Rejected,

        /// <summary>Hồ sơ đã được phê duyệt chính thức thành chuyên gia</summary>
        Approved
    }
}
