using System;

namespace TaxKeepVN.Domain.Entities
{
    /// <summary>
    /// Yêu cầu rút tiền từ Ví thu nhập về tài khoản ngân hàng cá nhân của Chuyên gia (Đặc tả 7 - Mục 5.3)
    /// </summary>
    public class WithdrawalRequest
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>FK → User.UserId (Chuyên gia gửi yêu cầu)</summary>
        public Guid UserId { get; set; }

        /// <summary>Số tiền yêu cầu rút (VNĐ, tối thiểu EXPERT_MIN_WITHDRAWAL_VND)</summary>
        public decimal Amount { get; set; }

        /// <summary>Tên ngân hàng thụ hưởng (VD: Vietcombank, Techcombank, MBBank)</summary>
        public string BankName { get; set; } = string.Empty;

        /// <summary>Số tài khoản ngân hàng thụ hưởng</summary>
        public string AccountNumber { get; set; } = string.Empty;

        /// <summary>Tên chủ tài khoản ngân hàng (phải trùng với họ tên xác thực eKYC)</summary>
        public string AccountName { get; set; } = string.Empty;

        /// <summary>Trạng thái: PENDING (Chờ duyệt), APPROVED (Đã duyệt), REJECTED (Từ chối), COMPLETED (Đã chuyển khoản thành công)</summary>
        public string Status { get; set; } = "PENDING";

        /// <summary>Lý do từ chối nếu bị Admin bác bỏ</summary>
        public string? RejectionReason { get; set; }

        /// <summary>Mã giao dịch ngân hàng / Bằng chứng chuyển khoản</summary>
        public string? TransactionReference { get; set; }

        /// <summary>Ghi chú của Admin khi xử lý</summary>
        public string? AdminNotes { get; set; }

        /// <summary>Admin tiếp nhận và xử lý</summary>
        public Guid? ProcessedBy { get; set; }

        public DateTimeOffset? ProcessedAt { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        // Navigation
        public User? User { get; set; }
    }
}
