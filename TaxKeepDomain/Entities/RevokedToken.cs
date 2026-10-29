using System;

namespace TaxKeepVN.Domain.Entities
{
    /// <summary>
    /// Lưu JTI của các JWT token đã bị thu hồi (đăng xuất).
    /// Các bản ghi hết hạn (ExpiresAt &lt; now) có thể được dọn dẹp định kỳ.
    /// </summary>
    public class RevokedToken
    {
        public Guid Id { get; set; }

        /// <summary>JWT ID claim (jti) — unique per token</summary>
        public string Jti { get; set; } = string.Empty;

        /// <summary>Thời điểm token hết hạn (lấy từ claim exp) để dọn DB sau này</summary>
        public DateTimeOffset ExpiresAt { get; set; }

        /// <summary>Thời điểm token bị thu hồi (logout)</summary>
        public DateTimeOffset RevokedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
