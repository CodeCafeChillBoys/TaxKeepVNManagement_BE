using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace TaxKeepVN.Application.Helpers
{
    /// <summary>
    /// Tiện ích xử lý tạo URL thanh toán và xác thực chữ ký HMAC-SHA512 cho cổng thanh toán VNPay v2.1.0 (Đặc tả 4)
    /// </summary>
    public static class VnPayHelper
    {
        /// <summary>
        /// Tạo URL chuyển hướng thanh toán VNPay kèm chữ ký số HMAC-SHA512
        /// </summary>
        public static string CreatePaymentUrl(
            string baseUrl,
            string tmnCode,
            string hashSecret,
            string returnUrl,
            long orderCode,
            decimal amount,
            string bookingCode,
            DateTimeOffset expireDate,
            string ipAddress,
            string? customReturnUrl = null)
        {
            var effectiveReturnUrl = !string.IsNullOrWhiteSpace(customReturnUrl)
                ? customReturnUrl.Trim()
                : returnUrl.Trim();

            var nowVn = DateTime.UtcNow.AddHours(7);
            var minExpire = DateTime.UtcNow.AddMinutes(15);
            var effectiveExpireUtc = expireDate.UtcDateTime < minExpire ? minExpire : expireDate.UtcDateTime;
            var expireVn = effectiveExpireUtc.AddHours(7);

            var sanitizedIp = (string.IsNullOrWhiteSpace(ipAddress) || ipAddress == "::1" || ipAddress.Contains(':'))
                ? "127.0.0.1"
                : ipAddress.Trim();

            var vnpParams = new SortedDictionary<string, string>(new VnPayCompare())
            {
                ["vnp_Version"] = "2.1.0",
                ["vnp_Command"] = "pay",
                ["vnp_TmnCode"] = tmnCode.Trim(),
                ["vnp_Amount"] = ((long)Math.Round(amount * 100m, 0)).ToString(CultureInfo.InvariantCulture),
                ["vnp_CreateDate"] = nowVn.ToString("yyyyMMddHHmmss"),
                ["vnp_CurrCode"] = "VND",
                ["vnp_IpAddr"] = sanitizedIp,
                ["vnp_Locale"] = "vn",
                ["vnp_OrderInfo"] = $"Thanh toan lich hen {bookingCode}".Trim(),
                ["vnp_OrderType"] = "other",
                ["vnp_ReturnUrl"] = effectiveReturnUrl,
                ["vnp_TxnRef"] = orderCode.ToString(CultureInfo.InvariantCulture),
                ["vnp_ExpireDate"] = expireVn.ToString("yyyyMMddHHmmss")
            };

            var data = new StringBuilder();
            var query = new StringBuilder();

            foreach (var kv in vnpParams)
            {
                if (!string.IsNullOrEmpty(kv.Value))
                {
                    data.Append(WebUtility.UrlEncode(kv.Key) + "=" + WebUtility.UrlEncode(kv.Value) + "&");
                    query.Append(WebUtility.UrlEncode(kv.Key) + "=" + WebUtility.UrlEncode(kv.Value) + "&");
                }
            }

            var rawData = data.ToString().TrimEnd('&');
            var queryString = query.ToString().TrimEnd('&');

            var vnpSecureHash = HmacSha512(hashSecret.Trim(), rawData);
            return $"{baseUrl}?{queryString}&vnp_SecureHash={vnpSecureHash}";
        }

        /// <summary>
        /// Xác thực chữ ký HMAC-SHA512 từ phản hồi IPN / Return của VNPay
        /// </summary>
        public static bool ValidateSignature(IDictionary<string, string> queryParams, string hashSecret)
        {
            if (queryParams == null || !queryParams.TryGetValue("vnp_SecureHash", out var receivedHash) || string.IsNullOrWhiteSpace(receivedHash))
            {
                return false;
            }

            var sortedParams = new SortedDictionary<string, string>(new VnPayCompare());
            foreach (var kv in queryParams)
            {
                if (!string.IsNullOrEmpty(kv.Key) && 
                    kv.Key.StartsWith("vnp_", StringComparison.OrdinalIgnoreCase) && 
                    !kv.Key.Equals("vnp_SecureHash", StringComparison.OrdinalIgnoreCase) &&
                    !kv.Key.Equals("vnp_SecureHashType", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrEmpty(kv.Value))
                {
                    sortedParams[kv.Key] = kv.Value;
                }
            }

            var data = new StringBuilder();
            foreach (var kv in sortedParams)
            {
                data.Append(WebUtility.UrlEncode(kv.Key) + "=" + WebUtility.UrlEncode(kv.Value) + "&");
            }

            var rawData = data.ToString().TrimEnd('&');
            var calculatedHash = HmacSha512(hashSecret.Trim(), rawData);

            return string.Equals(calculatedHash, receivedHash.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        public static string HmacSha512(string key, string inputData)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);
            var inputBytes = Encoding.UTF8.GetBytes(inputData);

            using var hmac = new HMACSHA512(keyBytes);
            var hashBytes = hmac.ComputeHash(inputBytes);

            var sb = new StringBuilder();
            foreach (var b in hashBytes)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }

        private class VnPayCompare : IComparer<string>
        {
            public int Compare(string? x, string? y)
            {
                if (x == y) return 0;
                if (x == null) return -1;
                if (y == null) return 1;
                var Compare = CompareInfo.GetCompareInfo("en-US");
                return Compare.Compare(x, y, CompareOptions.Ordinal);
            }
        }
    }
}
