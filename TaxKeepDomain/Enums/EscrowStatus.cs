namespace TaxKeepVN.Domain.Enums
{
    /// <summary>
    /// Trạng thái của giao dịch tạm giữ tiền bảo đảm (Escrow) cho mỗi ca đặt lịch tư vấn (Đặc tả 4 & 7)
    /// </summary>
    public enum EscrowStatus
    {
        /// <summary>
        /// Đã khởi tạo link thanh toán PayOS, đang chờ người nộp thuế quét mã thanh toán
        /// </summary>
        PendingPayment,

        /// <summary>
        /// Người nộp thuế đã thanh toán thành công, tiền đang được cô lập và tạm giữ trong ví Escrow của nền tảng
        /// </summary>
        HeldInEscrow,

        /// <summary>
        /// Ca tư vấn đã hoàn thành (COMPLETED), khoản tiền đang trong thời gian chờ khiếu nại (ESCROW_COOLDOWN_HOURS = 24h)
        /// </summary>
        CoolingDown,

        /// <summary>
        /// Đã khấu trừ hoa hồng nền tảng (A%) và giải ngân thù lao vào ví thu nhập khả dụng của chuyên gia
        /// </summary>
        Settled,

        /// <summary>
        /// Đã giải phóng khỏi Escrow và hoàn trả 100% số tiền về cho người nộp thuế (do chuyên gia từ chối, quá hạn 2h không duyệt, hoặc hủy sớm)
        /// </summary>
        Refunded,

        /// <summary>
        /// Đã hoàn trả một phần số tiền cho người nộp thuế theo chính sách hủy lịch cận giờ
        /// </summary>
        PartiallyRefunded,

        /// <summary>
        /// Giao dịch thanh toán bị hủy hoặc hết hạn giữ slot 10 phút trước khi khách hàng thanh toán
        /// </summary>
        Cancelled,

        /// <summary>
        /// Khoản tiền tạm giữ bị đóng băng (Freeze Escrow) do khách hàng gửi khiếu nại chất lượng dịch vụ (Đặc tả 7)
        /// </summary>
        Disputed
    }
}
