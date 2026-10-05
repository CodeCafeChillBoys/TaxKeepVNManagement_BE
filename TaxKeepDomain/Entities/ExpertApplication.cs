using System;
using System.Collections.Generic;
using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Domain.Entities
{
    /// <summary>
    /// Hồ sơ đăng ký trở thành chuyên gia (Đặc tả 1.1)
    /// </summary>
    public class ExpertApplication
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>FK → User.UserId (Ứng viên)</summary>
        public Guid UserId { get; set; }

        /// <summary>Mã số hồ sơ (VD: EXP-202609-0001)</summary>
        public string ApplicationNumber { get; set; } = string.Empty;

        // ── 5.1 Thông tin cá nhân ──────────────────────────────────────────
        public string FullName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string JobTitle { get; set; } = string.Empty;

        /// <summary>Đơn vị / công ty đang công tác (Nullable: cho phép chuyên gia tự do/nghỉ hưu)</summary>
        public string? CompanyName { get; set; }

        /// <summary>Giới thiệu bản thân</summary>
        public string? Bio { get; set; }

        // ── 5.2 Kinh nghiệm ────────────────────────────────────────────────
        public int YearsOfExperience { get; set; } = 0;
        public string? CurrentPosition { get; set; }
        public string ExperienceDescription { get; set; } = string.Empty;

        // ── 13 Trạng thái hồ sơ ─────────────────────────────────────────────
        /// <summary>Draft | PendingReview | NeedSupplement | Rejected | Approved</summary>
        public ExpertApplicationStatus Status { get; set; } = ExpertApplicationStatus.Draft;

        /// <summary>Thời điểm gửi hồ sơ chính thức (Draft -> PendingReview)</summary>
        public DateTimeOffset? SubmittedAt { get; set; }

        // ── Thẩm định & Audit (BR-09, BR-10, BR-11) ────────────────────────
        /// <summary>Admin / Reviewer thẩm định hồ sơ</summary>
        public Guid? ReviewedBy { get; set; }
        public DateTimeOffset? ReviewedAt { get; set; }

        /// <summary>Lý do từ chối (bắt buộc khi Rejected theo BR-09)</summary>
        public string? RejectionReason { get; set; }

        /// <summary>Nội dung yêu cầu bổ sung (bắt buộc khi NeedSupplement theo BR-10)</summary>
        public string? SupplementRequestReason { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        // ── Navigation Properties ───────────────────────────────────────────
        public User? User { get; set; }
        public User? Reviewer { get; set; }

        public ICollection<ExpertApplicationSpecialization> ApplicationSpecializations { get; set; } = new List<ExpertApplicationSpecialization>();
        public ICollection<ExpertApplicationCertificate> Certificates { get; set; } = new List<ExpertApplicationCertificate>();
        public ICollection<ExpertApplicationFeeProposal> FeeProposals { get; set; } = new List<ExpertApplicationFeeProposal>();
        public ICollection<ExpertApplicationAudit> Audits { get; set; } = new List<ExpertApplicationAudit>();
    }
}
