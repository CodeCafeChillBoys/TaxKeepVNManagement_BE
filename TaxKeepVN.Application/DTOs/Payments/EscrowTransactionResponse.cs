using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Application.DTOs.Payments
{
    /// <summary>
    /// Thông tin chi tiết bản ghi sổ cái tạm giữ tiền bảo đảm (EscrowTransaction) gắn với Booking (Đặc tả 4 & 7)
    /// </summary>
    public class EscrowTransactionResponse
    {
        public Guid Id { get; set; }
        public Guid BookingId { get; set; }
        public string BookingCode { get; set; } = string.Empty;
        public BookingStatus BookingStatus { get; set; }
        public long OrderCode { get; set; }
        public decimal Amount { get; set; }
        public decimal CommissionRate { get; set; }
        public decimal CommissionAmount { get; set; }
        public decimal ExpertReceivable { get; set; }
        public EscrowStatus EscrowStatus { get; set; }
        public string PaymentGateway { get; set; } = "PAYOS";
        public string? PaymentReference { get; set; }
        public string? PaymentLinkId { get; set; }
        public string? CheckoutUrl { get; set; }
        public string? QrCode { get; set; }
        public decimal RefundAmount { get; set; }
        public string? RefundReason { get; set; }
        public DateTimeOffset? PaidAt { get; set; }
        public DateTimeOffset? SettledAt { get; set; }
        public DateTimeOffset? RefundedAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
