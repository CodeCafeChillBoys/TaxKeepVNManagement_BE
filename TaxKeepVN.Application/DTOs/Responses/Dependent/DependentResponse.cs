using System;

namespace TaxKeepVN.Application.DTOs.Responses.Dependent
{
    public class DependentResponse
    {
        public Guid DependentId { get; set; }
        public Guid TaxpayerId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Relationship { get; set; } = string.Empty;
        public DateOnly? DateOfBirth { get; set; }
        public string? CitizenId { get; set; }
        public string? BirthCertNumber { get; set; }
        public string? TaxIdNumber { get; set; }
        public string EffectiveFromMonth { get; set; } = string.Empty;
        public string EffectiveToMonth { get; set; } = string.Empty;
        public string? Note { get; set; }

        /// <summary>
        /// Trạng thái hồ sơ: PENDING_DOCUMENTS | ACTIVE | INACTIVE.
        /// Sau khi tạo mới luôn là PENDING_DOCUMENTS — hướng dẫn FE phải upload giấy tờ.
        /// </summary>
        public string Status { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }

        /// <summary>
        /// Danh sách docType cần upload tùy theo nhóm quan hệ.
        /// FE dùng thông tin này để hiển thị checklist giấy tờ cho người dùng.
        /// </summary>
        public string[] RequiredDocuments { get; set; } = Array.Empty<string>();
    }
}
