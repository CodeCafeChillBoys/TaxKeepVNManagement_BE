using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Application.DTOs.Payments
{
    /// <summary>
    /// Thông tin link thanh toán VietQR / PayOS và chi tiết tạm giữ Escrow trả về cho khách hàng (Đặc tả 4)
    /// </summary>
    public class PaymentLinkResponse
    {
        public Guid BookingId { get; set; }
        public string BookingCode { get; set; } = string.Empty;
        public Guid EscrowTransactionId { get; set; }
        public long OrderCode { get; set; }
        public decimal Amount { get; set; }
        public decimal CommissionRate { get; set; }
        public decimal CommissionAmount { get; set; }
        public decimal ExpertReceivable { get; set; }
        public EscrowStatus EscrowStatus { get; set; }
        public string PaymentGateway { get; set; } = "PAYOS";
        public string? PaymentLinkId { get; set; }
        public string? CheckoutUrl { get; set; }
        public string? QrCode { get; set; }
        public DateTimeOffset HoldExpiresAt { get; set; }
        public int HoldCountdownSeconds { get; set; }
    }
}
