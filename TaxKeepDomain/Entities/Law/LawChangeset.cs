using System;
using System.Collections.Generic;

namespace TaxKeepVN.Domain.Entities.Law
{
    public class LawChangeset
    {
        public Guid Id { get; set; }
        public Guid? DocumentId { get; set; }
        public int BaseRevisionNo { get; set; }
        public string Status { get; set; } = "EXTRACTING";
        public string Origin { get; set; } = "AI";
        public Guid? AiTaskId { get; set; }
        public string? AiModel { get; set; }
        public string? AiRawResponse { get; set; }
        public string? AiWarnings { get; set; }
        public string? AiErrorCode { get; set; }
        public string? AiErrorMessage { get; set; }
        public int? PagesRead { get; set; }
        public int? TotalPages { get; set; }
        public string? Reason { get; set; }
        public Guid? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public int? MergedRevisionNo { get; set; }
        public Guid? MergedBy { get; set; }
        public DateTime? MergedAt { get; set; }
        public string? RejectedReason { get; set; }
        public Guid? RejectedBy { get; set; }
        public DateTime? RejectedAt { get; set; }

        // Navigation properties
        public virtual LegalDocument? Document { get; set; }
        public virtual LawRevision? Revision { get; set; }
        public virtual ICollection<LawChangeOp> Ops { get; set; } = new List<LawChangeOp>();
        public virtual ICollection<LawChangesetRelation> Relations { get; set; } = new List<LawChangesetRelation>();
        public virtual ICollection<LawOrphanResolution> OrphanResolutions { get; set; } = new List<LawOrphanResolution>();
    }
}
