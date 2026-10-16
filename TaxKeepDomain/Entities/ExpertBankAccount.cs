using System;

namespace TaxKeepVN.Domain.Entities
{
    /// <summary>
    /// Tài khoản ngân hàng nhận thù lao của Chuyên gia (Đặc tả 8)
    /// </summary>
    public class ExpertBankAccount
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>FK → ExpertProfile.Id</summary>
        public Guid ExpertProfileId { get; set; }

        /// <summary>Mã ngân hàng theo chuẩn VietQR/Napas (VD: VCB, MB, TCB, VPB...)</summary>
        public string BankCode { get; set; } = string.Empty;

        /// <summary>Tên đầy đủ của ngân hàng (VD: Ngân hàng TMCP Ngoại thương Việt Nam)</summary>
        public string BankName { get; set; } = string.Empty;

        /// <summary>Số tài khoản ngân hàng</summary>
        public string AccountNumber { get; set; } = string.Empty;

        /// <summary>Tên chủ tài khoản ngân hàng (viết hoa không dấu, phải khớp với FullName của User để chống gian lận/rửa tiền)</summary>
        public string AccountHolderName { get; set; } = string.Empty;

        /// <summary>Tài khoản nhận tiền mặc định</summary>
        public bool IsDefault { get; set; } = true;

        /// <summary>Trạng thái xác thực tài khoản chính chủ</summary>
        public bool IsVerified { get; set; } = false;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        // Navigation property
        public ExpertProfile? ExpertProfile { get; set; }
    }
}
