using System;

namespace TaxKeepVN.Domain.Entities.Law
{
    public class LawChangesetRelation
    {
        public Guid Id { get; set; }
        public Guid ChangesetId { get; set; }
        public string? RelationKey { get; set; }
        public string RelationType { get; set; } = string.Empty;
        public string TargetDocumentNumber { get; set; } = string.Empty;
        public string? TargetArticle { get; set; }
        public string? TargetClause { get; set; }
        public string? TargetPoint { get; set; }
        public string? SourceArticle { get; set; }
        public string? SourceClause { get; set; }
        public string? SourcePoint { get; set; }
        public int? SourcePage { get; set; }
        public DateOnly? EffectiveDate { get; set; }
        public string? EvidenceText { get; set; }
        public string? Note { get; set; }
        public string Origin { get; set; } = "AI";
        public string Decision { get; set; } = "PENDING";
        public string ConflictState { get; set; } = "NONE";
        public string? ConflictDetail { get; set; }
        public Guid? TargetDocId { get; set; }

        // Navigation properties
        public virtual LawChangeset Changeset { get; set; } = null!;
        public virtual LegalDocument? TargetDocument { get; set; }
    }
}
