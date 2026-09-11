using System;
using System.Collections.Generic;
using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Domain.Entities
{
    public class Dependent
    {
        public Guid Id { get; set; }

        /// <summary>FK → User.UserId — người nộp thuế đăng ký người phụ thuộc này</summary>
        public Guid TaxpayerId { get; set; }

        public string FullName { get; set; } = string.Empty;

        public DateTime BirthDate { get; set; } = DateTime.UtcNow;
        public DependentGroup CurrentGroup { get; set; } = DependentGroup.CHILD_UNDER_18;
        public bool IsDeleted { get; set; } = false;
        public bool IsProfileComplete { get; set; } = false; // Added for profile completion tracking

        /// <summary>Nhóm quan hệ theo Điều 9 TT 111/2013</summary>
        public DependentRelationship Relationship { get; set; } = DependentRelationship.CHILD;

        public DateOnly? DateOfBirth { get; set; }

        /// <summary>
        /// Số Căn cước công dân (12 số). Nullable vì trẻ em nhỏ chưa có CCCD.
        /// Ít nhất một trong CitizenId hoặc BirthCertNumber phải có giá trị.
        /// </summary>
        public string? CitizenId { get; set; }

        /// <summary>
        /// Số Giấy khai sinh — dùng cho trẻ em chưa có CCCD.
        /// Ví dụ: "GKS-1234/2018"
        /// </summary>
        public string? BirthCertNumber { get; set; }

        /// <summary>Mã số thuế của người phụ thuộc (nếu có)</summary>
        public string? TaxIdNumber { get; set; }

        /// <summary>
        /// Tháng bắt đầu tính giảm trừ gia cảnh, format "YYYY-MM".
        /// Ví dụ: "2026-01"
        /// </summary>
        public string EffectiveFromMonth { get; set; } = string.Empty;

        /// <summary>
        /// Tháng kết thúc tính giảm trừ gia cảnh, format "YYYY-MM".
        /// Ví dụ: "2026-12"
        /// </summary>
        public string EffectiveToMonth { get; set; } = string.Empty;

        public string? Note { get; set; }

        /// <summary>Trạng thái hồ sơ người phụ thuộc</summary>
        public DependentStatus Status { get; set; } = DependentStatus.PENDING_DOCUMENTS;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        // ── Navigation property ─────────────────────────────────────────────────
        public ICollection<DependentDocument> Documents { get; set; } = new List<DependentDocument>();
    }
}
