using System;

namespace TaxKeepVN.Domain.Entities.Law
{
    public class LegalDocumentRelation
    {
        public Guid Id { get; set; }
        public Guid SourceDocumentId { get; set; }
        public Guid TargetDocumentId { get; set; }
        public string RelationType { get; set; } = string.Empty;
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
        public int CreatedInRevision { get; set; }

        public virtual LegalDocument SourceDocument { get; set; } = null!;
        public virtual LegalDocument TargetDocument { get; set; } = null!;
        public virtual LawRevision Revision { get; set; } = null!;
    }
}
