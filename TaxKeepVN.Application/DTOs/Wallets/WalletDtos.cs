using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TaxKeepVN.Application.DTOs.Wallets
{
    /// <summary>
    /// Response thông tin ví điện tử (Ví thu nhập chuyên gia hoặc ví hoàn tiền người dùng)
    /// </summary>
    public class WalletResponse
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string WalletType { get; set; } = string.Empty;
        public decimal AvailableBalance { get; set; }
        public decimal FrozenBalance { get; set; }
        public decimal TotalEarned { get; set; }
        public decimal TotalWithdrawn { get; set; }

        /// <summary>
        /// Tổng tiền thù lao đang bị tạm giữ chưa giải ngân về ví EXPERT (EscrowStatus = HeldInEscrow hoặc CoolingDown)
        /// </summary>
        public decimal PendingEscrowReceivable { get; set; }

        /// <summary>
        /// Danh sách chi tiết các ca tư vấn đang tạm giữ tiền bảo chứng chờ giải ngân
        /// </summary>
        public List<PendingEscrowDetailResponse> PendingEscrowDetails { get; set; } = new();

        public DateTimeOffset UpdatedAt { get; set; }
        public List<WalletTransactionResponse> RecentTransactions { get; set; } = new();
    }

    /// <summary>
    /// Chi tiết ca tư vấn đang bị tạm giữ tiền ký quỹ Escrow chờ giải ngân
    /// </summary>
    public class PendingEscrowDetailResponse
    {
        public Guid EscrowId { get; set; }
        public Guid BookingId { get; set; }
        public string BookingCode { get; set; } = string.Empty;
        public string TaxpayerName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal CommissionRate { get; set; }
        public decimal CommissionAmount { get; set; }
        public decimal ExpertReceivable { get; set; }
        public string EscrowStatus { get; set; } = string.Empty;
        public string EscrowStatusDescription { get; set; } = string.Empty;
        public DateTimeOffset? PaidAt { get; set; }
        public DateTimeOffset? ConsultationTime { get; set; }
        public DateTimeOffset? EstimatedReleaseAt { get; set; }
    }

    /// <summary>
    /// Bản ghi lịch sử giao dịch / biến động số dư ví
    /// </summary>
    public class WalletTransactionResponse
    {
        public Guid Id { get; set; }
        public Guid WalletId { get; set; }
        public decimal Amount { get; set; }
        public decimal BalanceBefore { get; set; }
        public decimal BalanceAfter { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public Guid? ReferenceId { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
    }

    /// <summary>
    /// Request chuyên gia gửi lệnh rút tiền về tài khoản ngân hàng (Đặc tả 7 - Mục 5.3)
    /// </summary>
    public class CreateWithdrawalRequest
    {
        /// <summary>Số tiền yêu cầu rút (VNĐ, tối thiểu EXPERT_MIN_WITHDRAWAL_VND)</summary>
        public decimal Amount { get; set; }

        /// <summary>Tên ngân hàng thụ hưởng</summary>
        public string BankName { get; set; } = string.Empty;

        /// <summary>Số tài khoản ngân hàng</summary>
        public string AccountNumber { get; set; } = string.Empty;

        /// <summary>Tên chủ tài khoản</summary>
        public string AccountName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response chi tiết lệnh rút tiền
    /// </summary>
    public class WithdrawalResponse
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string UserFullName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string BankName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? RejectionReason { get; set; }
        public string? TransactionReference { get; set; }
        public string? AdminNotes { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? ProcessedAt { get; set; }
    }

    /// <summary>
    /// Request Admin duyệt hoặc từ chối lệnh rút tiền
    /// </summary>
    public class AdminProcessWithdrawalRequest
    {
        /// <summary>Hành động xử lý: APPROVE (Duyệt & đã chuyển tiền) | REJECT (Từ chối & hoàn tiền về ví)</summary>
        public string Action { get; set; } = "APPROVE";

        /// <summary>Mã giao dịch ngân hàng / Ủy nhiệm chi (nếu duyệt)</summary>
        public string? TransactionReference { get; set; }

        /// <summary>Lý do từ chối (bắt buộc nếu REJECT)</summary>
        public string? RejectionReason { get; set; }

        /// <summary>Ghi chú của Admin</summary>
        public string? AdminNotes { get; set; }
    }

    /// <summary>
    /// Request nạp tiền vào ví cá nhân qua cổng thanh toán VNPay / VietQR
    /// </summary>
    public class WalletTopUpRequest
    {
        [Range(10000, 100000000, ErrorMessage = "Số tiền nạp tối thiểu là 10.000 VNĐ và tối đa 100.000.000 VNĐ.")]
        public decimal Amount { get; set; }

        /// <summary>Đường dẫn quay về sau khi hoàn tất thanh toán (Tùy chọn)</summary>
        public string? ReturnUrl { get; set; }
    }

    /// <summary>
    /// Response khởi tạo link nạp tiền vào ví
    /// </summary>
    public class WalletTopUpResponse
    {
        public long OrderCode { get; set; }
        public decimal Amount { get; set; }
        public string PaymentGateway { get; set; } = "VNPAY";
        public string? CheckoutUrl { get; set; }
        public string? QrCode { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
