using System;
using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Domain.Entities
{
    /// <summary>
    /// Lệnh yêu cầu rút tiền về tài khoản ngân hàng của Chuyên gia (Đặc tả 8)
    /// </summary>
    public class WithdrawalRequest
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>Mã yêu cầu rút tiền thân thiện với người dùng (VD: WDR-20261016-A1B2C3)</summary>
        public string RequestCode { get; set; } = string.Empty;

        /// <summary>FK → ExpertProfile.Id</summary>
        public Guid ExpertProfileId { get; set; }

        /// <summary>Số tiền chuyên gia yêu cầu rút (VNĐ)</summary>
        public decimal Amount { get; set; }

        /// <summary>Số tiền thuế TNCN khấu trừ tại nguồn nếu có quy định (VNĐ)</summary>
        public decimal TaxWithheld { get; set; } = 0m;

        /// <summary>Số tiền thực nhận sau khi khấu trừ thuế (Amount - TaxWithheld)</summary>
        public decimal NetAmount { get; set; }

        // ── Snapshot thông tin ngân hàng thụ hưởng tại thời điểm tạo lệnh rút ──
        public string BankCode { get; set; } = string.Empty;
        public string BankName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountHolderName { get; set; } = string.Empty;

        /// <summary>Trạng thái xử lý: Pending | Approved | Transferred | Rejected</summary>
        public WithdrawalStatus Status { get; set; } = WithdrawalStatus.Pending;

        /// <summary>Đường dẫn ảnh chụp chứng từ ủy nhiệm chi / biên lai chuyển khoản</summary>
        public string? TransferProofUrl { get; set; }

        /// <summary>Mã tham chiếu bút toán ngân hàng chuyển khoản</summary>
        public string? BankTransactionReference { get; set; }

        /// <summary>Ghi chú của Admin / Kế toán</summary>
        public string? AdminNote { get; set; }

        /// <summary>Lý do từ chối (bắt buộc khi Rejected)</summary>
        public string? RejectionReason { get; set; }

        /// <summary>Admin / Kế toán duyệt hoặc từ chối lệnh</summary>
        public Guid? ReviewedBy { get; set; }
        public DateTimeOffset? ReviewedAt { get; set; }

        /// <summary>Thời điểm chuyển tiền thành công</summary>
        public DateTimeOffset? TransferredAt { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        // Navigation properties
        public ExpertProfile? ExpertProfile { get; set; }
        public User? Reviewer { get; set; }
    }
}
