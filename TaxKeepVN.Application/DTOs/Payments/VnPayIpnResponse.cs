using System.Text.Json.Serialization;

namespace TaxKeepVN.Application.DTOs.Payments
{
    /// <summary>
    /// Phản hồi chuẩn trả về cho VNPay khi tiếp nhận Webhook IPN (Đặc tả 4)
    /// </summary>
    public class VnPayIpnResponse
    {
        [JsonPropertyName("RspCode")]
        public string RspCode { get; set; } = "00";

        [JsonPropertyName("Message")]
        public string Message { get; set; } = "Confirm Success";

        public static VnPayIpnResponse Success() => new() { RspCode = "00", Message = "Confirm Success" };
        public static VnPayIpnResponse OrderNotFound() => new() { RspCode = "01", Message = "Order not found" };
        public static VnPayIpnResponse OrderAlreadyConfirmed() => new() { RspCode = "02", Message = "Order already confirmed" };
        public static VnPayIpnResponse InvalidAmount() => new() { RspCode = "04", Message = "Invalid amount" };
        public static VnPayIpnResponse InvalidSignature() => new() { RspCode = "97", Message = "Invalid signature" };
        public static VnPayIpnResponse OtherError(string message) => new() { RspCode = "99", Message = message };
    }
}
