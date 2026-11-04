using System;

namespace TaxKeepVN.Domain.Entities
{
    /// <summary>
    /// Gói đăng ký hiện tại / lịch sử của từng người dùng.
    /// Quota được snapshot tại thời điểm mua để bảo vệ user khi admin đổi plan.
    /// </summary>
    public class UserSubscription
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>FK → users.user_id</summary>
        public Guid UserId { get; set; }

        /// <summary>FK → subscription_plans.id</summary>
        public Guid PlanId { get; set; }

        /// <summary>monthly | yearly | free</summary>
        public string BillingCycle { get; set; } = "free";

        /// <summary>active | expired | cancelled</summary>
        public string Status { get; set; } = "active";

        /// <summary>Số tiền đã thanh toán (VNĐ). 0 = gói miễn phí</summary>
        public decimal AmountPaid { get; set; } = 0m;

        /// <summary>WALLET | VNPAY | VIETQR | FREE</summary>
        public string PaymentMethod { get; set; } = "FREE";

        /// <summary>paid | free | pending | refunded</summary>
        public string PaymentStatus { get; set; } = "free";

        /// <summary>Mã tham chiếu giao dịch thanh toán (WalletTransaction.Id hoặc order code)</summary>
        public string? PaymentReference { get; set; }

        /// <summary>Ngày bắt đầu gói có hiệu lực</summary>
        public DateTimeOffset StartsAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>Ngày hết hạn gói. NULL = vĩnh viễn (Basic miễn phí)</summary>
        public DateTimeOffset? ExpiresAt { get; set; }

        // ── Snapshot quota tại thời điểm mua (bảo vệ user khi admin đổi plan) ──
        public int SnapshotMaxAiQuestionsPerMonth { get; set; }
        public int SnapshotChatHistoryRetentionDays { get; set; }
        public int SnapshotMaxOcrScansPerMonth { get; set; }
        public int SnapshotMaxIncomeSources { get; set; }
        public int SnapshotMaxDependents { get; set; }
        public bool SnapshotCanExportPdf { get; set; }
        public bool SnapshotCanExportZip { get; set; }
        public int SnapshotBookingDiscountPercent { get; set; }
        public int SnapshotFreeConsultationsPerYear { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        // Navigation
        public User? User { get; set; }
        public SubscriptionPlan? Plan { get; set; }
    }
}

