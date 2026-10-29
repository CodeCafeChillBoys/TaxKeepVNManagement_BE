using System;
using System.Collections.Generic;

namespace TaxKeepVN.Domain.Entities
{
    /// <summary>
    /// Ví điện tử trên nền tảng dành cho Chuyên gia (Ví thu nhập) và Khách hàng (Ví hoàn tiền) (Đặc tả 7)
    /// </summary>
    public class UserWallet
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>FK → User.UserId</summary>
        public Guid UserId { get; set; }

        /// <summary>Loại ví: EXPERT (Ví thu nhập chuyên gia) | USER (Ví hoàn tiền khách hàng)</summary>
        public string WalletType { get; set; } = "USER";

        /// <summary>Số dư khả dụng (VNĐ) — Chuyên gia có thể rút hoặc Khách hàng có thể sử dụng</summary>
        public decimal AvailableBalance { get; set; } = 0m;

        /// <summary>Số dư đang tạm đóng băng (VNĐ) — khi đang chờ xử lý lệnh rút tiền hoặc tranh chấp</summary>
        public decimal FrozenBalance { get; set; } = 0m;

        /// <summary>Tổng thu nhập lũy kế đã nhận qua sàn (VNĐ) (chỉ áp dụng cho Chuyên gia)</summary>
        public decimal TotalEarned { get; set; } = 0m;

        /// <summary>Tổng số tiền đã rút về tài khoản ngân hàng thành công (VNĐ)</summary>
        public decimal TotalWithdrawn { get; set; } = 0m;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        // Navigation
        public User? User { get; set; }
        public ICollection<WalletTransaction> Transactions { get; set; } = new List<WalletTransaction>();
    }
}
