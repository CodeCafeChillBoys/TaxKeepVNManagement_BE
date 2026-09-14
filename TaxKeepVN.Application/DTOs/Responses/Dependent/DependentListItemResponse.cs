using System;

namespace TaxKeepVN.Application.DTOs.Responses.Dependent
{
    /// <summary>
    /// DTO rút gọn dùng trong danh sách NPT — không bao gồm Documents để giảm payload.
    /// </summary>
    public class DependentListItemResponse
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

        public string EffectiveFromMonth { get; set; } = string.Empty;
        public string EffectiveToMonth { get; set; } = string.Empty;

        /// <summary>PENDING_DOCUMENTS | ACTIVE | INACTIVE</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>true khi đã upload đủ tất cả giấy tờ bắt buộc theo nhóm</summary>
        public bool IsProfileComplete { get; set; }

        public string? Note { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
    }
}
