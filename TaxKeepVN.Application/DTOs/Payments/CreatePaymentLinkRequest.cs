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
    }
}
