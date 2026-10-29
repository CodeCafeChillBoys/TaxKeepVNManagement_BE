namespace TaxKeepVN.Domain.Enums
{
    /// <summary>
    /// Trạng thái thẩm định chứng chỉ / bằng cấp chuyên gia (Mục 11 - Đặc tả 1.1)
    /// </summary>
    public enum CertificateVerificationStatus
    {
        /// <summary>Đang chờ thẩm định, kiểm tra</summary>
        PendingVerification,

        /// <summary>Chứng chỉ hợp lệ, đã xác minh</summary>
        Verified,

        /// <summary>Chứng chỉ không hợp lệ hoặc bị từ chối</summary>
        Rejected,

        /// <summary>Chứng chỉ đã hết hiệu lực</summary>
        Expired
    }
}
