using System;
using System.Collections.Generic;

namespace TaxKeepVN.Domain.Entities.Law
{
    public class LegalDocument
    {
        public Guid Id { get; set; }
        public string? DocumentNumber { get; set; }
        public string? NumberNormalized { get; set; }
        public string? DocumentType { get; set; }
        public string? Title { get; set; }
        public string? Issuer { get; set; }
        public DateOnly? IssuedDate { get; set; }
        public DateOnly? EffectiveDate { get; set; }
        public string? FileUrl { get; set; }
        public string? OriginalFilename { get; set; }
        public string? SourceUrl { get; set; }
        public int? TotalPages { get; set; }
        public string LegalStatus { get; set; } = "CHUA_RO";
        public string? LegalStatusNote { get; set; }
        public bool IsPlaceholder { get; set; }
        public Guid? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ICollection<LegalDocumentRelation> SourceRelations { get; set; } = new List<LegalDocumentRelation>();
        public virtual ICollection<LegalDocumentRelation> TargetRelations { get; set; } = new List<LegalDocumentRelation>();
        public virtual ICollection<LawChangeset> Changesets { get; set; } = new List<LawChangeset>();
        public virtual ICollection<LawRuleVersion> RuleVersions { get; set; } = new List<LawRuleVersion>();
    }
}
