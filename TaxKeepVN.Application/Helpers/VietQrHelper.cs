using System;
using System.Net;

namespace TaxKeepVN.Application.Helpers
{
    /// <summary>
    /// Tiện ích sinh mã và liên kết hình ảnh VietQR chuẩn NAPAS 247 cho lịch hẹn tư vấn thuế (Đặc tả 4)
    /// </summary>
    public static class VietQrHelper
    {
        /// <summary>
        /// Tạo URL ảnh VietQR QuickLink chuẩn NAPAS 247
        /// </summary>
        /// <param name="bankBin">Mã BIN ngân hàng (VD: 970422 - Quân Đội MB, 970415 - VietinBank)</param>
        /// <param name="accountNo">Số tài khoản ngân hàng thụ hưởng (Ví Escrow)</param>
        /// <param name="accountName">Tên chủ tài khoản thụ hưởng</param>
        /// <param name="amount">Số tiền thanh toán (VNĐ)</param>
        /// <param name="description">Nội dung chuyển khoản (Tối đa 25 ký tự, VD: Mã booking)</param>
        /// <param name="template">Giao diện QR: compact, compact2, qr_only, print</param>
        /// <returns>Đường dẫn hình ảnh QR Code</returns>
        public static string GenerateVietQrUrl(
            string bankBin,
            string accountNo,
            string? accountName,
            decimal amount,
            string description,
            string template = "compact2")
        {
            var cleanBin = string.IsNullOrWhiteSpace(bankBin) ? "970405" : bankBin.Trim();
            var cleanAccNo = string.IsNullOrWhiteSpace(accountNo) ? "5402205457446" : accountNo.Trim();
            var cleanTemplate = string.IsNullOrWhiteSpace(template) ? "compact2" : template.Trim();

            var cleanAmount = (long)Math.Round(amount, 0);
            var encodedDesc = WebUtility.UrlEncode(description.Trim());

            var url = $"https://img.vietqr.io/image/{cleanBin}-{cleanAccNo}-{cleanTemplate}.png?amount={cleanAmount}&addInfo={encodedDesc}";
            if (!string.IsNullOrWhiteSpace(accountName))
            {
                url += $"&accountName={WebUtility.UrlEncode(accountName.Trim())}";
            }

            return url;
        }
    }
}
