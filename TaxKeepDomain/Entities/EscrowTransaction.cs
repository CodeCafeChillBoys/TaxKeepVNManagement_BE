using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Domain.Entities
{
    /// <summary>
    /// Bản ghi sổ cái giao dịch tạm giữ tiền bảo đảm (Escrow) cho từng Booking (Đặc tả 4 & 7)
    /// Chụp cứng tỷ lệ hoa hồng nền tảng (CommissionRate) tại thời điểm tạo giao dịch.
    /// </summary>
    public class EscrowTransaction
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>FK 1-1 trỏ tới Booking</summary>
        public Guid BookingId { get; set; }

        /// <summary>Mã đơn hàng số nguyên duy nhất gửi sang cổng thanh toán PayOS (orderCode)</summary>
        public long OrderCode { get; set; }

        /// <summary>Tổng số tiền phí tư vấn thu từ người nộp thuế (VNĐ)</summary>
        public decimal Amount { get; set; }

        /// <summary>Tỷ lệ phần trăm hoa hồng nền tảng chụp cứng tại thời điểm tạo (đọc từ PLATFORM_COMMISSION_RATE_PERCENT)</summary>
        public decimal CommissionRate { get; set; }

        /// <summary>Số tiền hoa hồng nền tảng được hưởng (Amount * CommissionRate / 100)</summary>
        public decimal CommissionAmount { get; set; }

        /// <summary>Số tiền thù lao ròng chuyên gia thực nhận sau khi trừ hoa hồng (Amount - CommissionAmount)</summary>
        public decimal ExpertReceivable { get; set; }

        /// <summary>Trạng thái hiện tại của khoản tiền trong ví Escrow</summary>
        public EscrowStatus EscrowStatus { get; set; } = EscrowStatus.PendingPayment;

        /// <summary>Tên cổng thanh toán sử dụng (mặc định: PAYOS)</summary>
        public string PaymentGateway { get; set; } = "PAYOS";

        /// <summary>Mã tham chiếu bút toán thanh toán từ cổng PayOS / Ngân hàng</summary>
        public string? PaymentReference { get; set; }

        /// <summary>Mã định danh Payment Link trên hệ thống PayOS</summary>
        public string? PaymentLinkId { get; set; }

        /// <summary>Đường dẫn URL trang thanh toán VietQR của PayOS</summary>
        public string? CheckoutUrl { get; set; }

        /// <summary>Chuỗi dữ liệu mã VietQR (EMVCo) để Frontend/Mobile render trực tiếp mã QR</summary>
        public string? QrCode { get; set; }

        /// <summary>Số tiền hoàn trả cho người nộp thuế (nếu phát sinh hoàn tiền toàn phần hoặc một phần)</summary>
        public decimal RefundAmount { get; set; } = 0m;

        /// <summary>Lý do hoàn tiền hoặc ghi chú đối soát</summary>
        public string? RefundReason { get; set; }

        /// <summary>Thời điểm người nộp thuế thanh toán thành công vào Escrow</summary>
        public DateTimeOffset? PaidAt { get; set; }

        /// <summary>Thời điểm giải ngân thù lao cho chuyên gia sau thời gian Cool-down</summary>
        public DateTimeOffset? SettledAt { get; set; }

        /// <summary>Thời điểm hoàn tiền cho người nộp thuế</summary>
        public DateTimeOffset? RefundedAt { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        // Navigation property
        public Booking? Booking { get; set; }
    }
}
