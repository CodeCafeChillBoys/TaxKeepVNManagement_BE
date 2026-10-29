namespace TaxKeepVN.Domain.Enums
{
    /// <summary>
    /// Trạng thái hồ sơ người phụ thuộc.
    /// </summary>
    public enum DependentStatus
    {
        /// <summary>Vừa tạo — đang chờ người dùng upload giấy tờ minh chứng.</summary>
        PENDING_DOCUMENTS,

        /// <summary>Đã đủ giấy tờ hợp lệ, đang được tính giảm trừ gia cảnh.</summary>
        ACTIVE,

        /// <summary>Không còn đủ điều kiện hoặc đã hết kỳ tính giảm trừ.</summary>
        INACTIVE
    }
}
