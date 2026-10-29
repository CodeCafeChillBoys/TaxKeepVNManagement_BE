using System;

namespace TaxKeepVN.Domain.Entities
{
    /// <summary>
    /// Bản ghi biến động số dư ví (Sổ cái giao dịch ví cá nhân) (Đặc tả 7)
    /// </summary>
    public class WalletTransaction
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>FK → UserWallet.Id</summary>
        public Guid WalletId { get; set; }

        /// <summary>FK → User.UserId</summary>
        public Guid UserId { get; set; }

        /// <summary>Số tiền biến động (VNĐ) — Giá trị dương: Cộng tiền; Giá trị âm: Trừ tiền</summary>
        public decimal Amount { get; set; }

        /// <summary>Số dư khả dụng trước biến động</summary>
        public decimal BalanceBefore { get; set; }

        /// <summary>Số dư khả dụng sau biến động</summary>
        public decimal BalanceAfter { get; set; }

        /// <summary>
        /// Loại giao dịch:
        /// DISBURSEMENT: Giải ngân thù lao ca tư vấn sau 24h hoặc User bấm xác nhận hài lòng
        /// WITHDRAWAL: Trừ tiền khi gửi lệnh rút tiền về ngân hàng
        /// WITHDRAWAL_REFUND: Hoàn lại tiền khi lệnh rút tiền bị từ chối
        /// REFUND: Hoàn tiền hủy lịch / khiếu nại thành công
        /// COMPENSATION: Tiền đền bù khi khách hủy sát giờ hoặc No-show
        /// PENALTY: Phạt tiền vi phạm khi chuyên gia hủy sát giờ
        /// </summary>
        public string TransactionType { get; set; } = string.Empty;

        /// <summary>Mã tham chiếu nghiệp vụ (BookingId, WithdrawalRequestId, DisputeId)</summary>
        public Guid? ReferenceId { get; set; }

        /// <summary>Mô tả chi tiết nội dung giao dịch</summary>
        public string Description { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        // Navigation
        public UserWallet? Wallet { get; set; }
        public User? User { get; set; }
    }
}
