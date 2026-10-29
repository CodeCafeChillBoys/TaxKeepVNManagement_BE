namespace TaxKeepVN.Domain.Enums
{
    /// <summary>
    /// Các hành động xử lý trong quá trình thẩm định hồ sơ chuyên gia (Audit Trail - BR-11)
    /// </summary>
    public enum ApplicationAuditAction
    {
        /// <summary>Lưu bản nháp</summary>
        DraftSaved,

        /// <summary>Gửi hồ sơ đăng ký lần đầu</summary>
        Submitted,

        /// <summary>Reviewer yêu cầu bổ sung thông tin</summary>
        SupplementRequested,

        /// <summary>Ứng viên nộp lại hồ sơ sau khi bổ sung</summary>
        Resubmitted,

        /// <summary>Phê duyệt hồ sơ</summary>
        Approved,

        /// <summary>Từ chối hồ sơ</summary>
        Rejected
    }
}
