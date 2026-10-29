using System;
using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Domain.Entities
{
    /// <summary>
    /// Nhật ký lưu vết xử lý hồ sơ (Audit Trail - BR-11)
    /// </summary>
    public class ExpertApplicationAudit
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid ApplicationId { get; set; }

        /// <summary>FK → User.UserId (Người thực hiện hành động: Ứng viên hoặc Admin)</summary>
        public Guid ActorId { get; set; }

        /// <summary>
        /// Hành động: DraftSaved | Submitted | SupplementRequested | Resubmitted | Approved | Rejected
        /// </summary>
        public ApplicationAuditAction Action { get; set; } = ApplicationAuditAction.DraftSaved;

        /// <summary>Trạng thái trước hành động</summary>
        public ExpertApplicationStatus? FromStatus { get; set; }

        /// <summary>Trạng thái sau hành động</summary>
        public ExpertApplicationStatus ToStatus { get; set; } = ExpertApplicationStatus.Draft;

        /// <summary>Ghi chú / Lý do từ chối / Nội dung yêu cầu bổ sung</summary>
        public string? Notes { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        // Navigation properties
        public ExpertApplication? Application { get; set; }
        public User? Actor { get; set; }
    }
}
