namespace TaxKeepVN.Domain.Enums
{
    /// <summary>
    /// Trạng thái của lệnh yêu cầu rút tiền của chuyên gia (Đặc tả 8)
    /// </summary>
    public enum WithdrawalStatus
    {
        /// <summary>
        /// Chuyên gia vừa tạo yêu cầu, đang chờ Admin/Kế toán đối soát và phê duyệt
        /// </summary>
        Pending,

        /// <summary>
        /// Admin/Kế toán đã duyệt lệnh rút, đang thực hiện thủ tục chuyển khoản ngân hàng
        /// </summary>
        Approved,

        /// <summary>
        /// Tiền đã được chuyển khoản thành công về tài khoản ngân hàng của Chuyên gia
        /// </summary>
        Transferred,

        /// <summary>
        /// Lệnh rút tiền bị từ chối (hoàn trả lại số dư khả dụng cho chuyên gia)
        /// </summary>
        Rejected
    }
}
