using System;
using System.Collections.Generic;

namespace TaxKeepVN.Application.DTOs.Responses.Dependent
{
    /// <summary>
    /// Response đầy đủ cho GET /api/v1/dependents/{id} — bao gồm thông tin NPT và tất cả documents đã upload.
    /// </summary>
    public class DependentDetailResponse
    {
        public Guid DependentId { get; set; }
        public Guid TaxpayerId { get; set; }

        public string FullName { get; set; } = string.Empty;

        /// <summary>CHILD | SPOUSE | PARENT | OTHER_DEPENDENT</summary>
        public string Relationship { get; set; } = string.Empty;

        /// <summary>CHILD_UNDER_18 | CHILD_OVER_18_DISABLED | CHILD_OVER_18_STUDYING | SPOUSE_DISABLED | SPOUSE_RETIRED | PARENT_DISABLED | PARENT_RETIRED | OTHER_HELPLESS</summary>
        public string CurrentGroup { get; set; } = string.Empty;

        public DateTime BirthDate { get; set; }

        public string? CitizenId { get; set; }
        public string? BirthCertNumber { get; set; }
        public string? TaxIdNumber { get; set; }

        public string EffectiveFromMonth { get; set; } = string.Empty;
        public string EffectiveToMonth { get; set; } = string.Empty;

        /// <summary>PENDING_DOCUMENTS | ACTIVE | INACTIVE</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>true khi đã upload đủ tất cả giấy tờ bắt buộc theo nhóm</summary>
        public bool IsProfileComplete { get; set; }

        public string? Note { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }

        /// <summary>
        /// Danh sách các loại giấy tờ cần upload theo nhóm quan hệ (CurrentGroup).
        /// FE dùng để hiển thị checklist.
        /// </summary>
        public string[] RequiredDocuments { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Danh sách tất cả documents đã upload cho NPT này.
        /// </summary>
        public List<DependentDocumentDto> Documents { get; set; } = new();
    }
}
