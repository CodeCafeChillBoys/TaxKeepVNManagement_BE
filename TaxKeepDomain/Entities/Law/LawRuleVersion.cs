using System;

namespace TaxKeepVN.Domain.Entities.Law
{
    public class LawRuleVersion
    {
        public Guid Id { get; set; }
        public int CreatedInRevision { get; set; }
        public string RuleCode { get; set; } = string.Empty;
        public Guid? DocumentId { get; set; }
        public string? Article { get; set; }
        public string? Clause { get; set; }
        public string? Point { get; set; }
        public int? Page { get; set; }
        public string? EvidenceText { get; set; }
        public DateOnly ApplyFrom { get; set; }
        public DateOnly? ApplyTo { get; set; }
        public string RuleValue { get; set; } = "{}";
        public int? SupersededInRevision { get; set; }
        public DateTime? SupersededAt { get; set; }
        public Guid? DerivedFromVersionId { get; set; }
        public Guid? SourceOpId { get; set; }

        // Navigation properties
        public virtual LawRevision Revision { get; set; } = null!;
        public virtual LawRevision? SupersededRevision { get; set; }
        public virtual LawRuleDefinition RuleDefinition { get; set; } = null!;
        public virtual LegalDocument? Document { get; set; }
        public virtual LawChangeOp? SourceOp { get; set; }
    }
}
