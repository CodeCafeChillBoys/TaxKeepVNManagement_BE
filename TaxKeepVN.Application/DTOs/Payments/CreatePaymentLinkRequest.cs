namespace TaxKeepVN.Application.DTOs.Payments
{
    /// <summary>
    /// Request khởi tạo link thanh toán PayOS cho phiên đặt lịch tư vấn (Đặc tả 4)
    /// </summary>
    public class CreatePaymentLinkRequest
    {
        /// <summary>Đường dẫn điều hướng về Frontend/Mobile khi thanh toán thành công (Tùy chọn)</summary>
        public string? ReturnUrl { get; set; }

        /// <summary>Đường dẫn điều hướng về Frontend/Mobile khi khách hàng bấm hủy thanh toán (Tùy chọn)</summary>
        public string? CancelUrl { get; set; }

        /// <summary>Mã ID của lịch hẹn (Tùy chọn nếu truyền qua URL route)</summary>
        public Guid? BookingId { get; set; }

        /// <summary>
        /// Phương thức thanh toán:
        /// - GATEWAY: Cổng thanh toán trực tuyến VNPay / VietQR (Mặc định)
        /// - WALLET: Trừ trực tiếp từ số dư khả dụng ví User
        /// </summary>
        public string? PaymentMethod { get; set; } = "GATEWAY";
    }
}
