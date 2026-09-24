using System;

namespace TaxKeepVN.Domain.Entities.Law
{
    public class LawChangeOp
    {
        public Guid Id { get; set; }
        public Guid ChangesetId { get; set; }
        public int Seq { get; set; }
        public string? OpKey { get; set; }
        public string OpType { get; set; } = "ADD";
        public string RuleCode { get; set; } = string.Empty;
        public bool NewCode { get; set; }
        public string? ProposedDefinition { get; set; }
        public string? Before { get; set; }
        public string? After { get; set; }
        public DateOnly? ApplyFrom { get; set; }
        public DateOnly? ApplyTo { get; set; }
        public string? ApplyBasis { get; set; }
        public string? Article { get; set; }
        public string? Clause { get; set; }
        public string? Point { get; set; }
        public int? Page { get; set; }
        public string? EvidenceText { get; set; }
        public decimal? Confidence { get; set; }
        public string? Rationale { get; set; }
        public string Origin { get; set; } = "AI";
        public string Decision { get; set; } = "PENDING";
        public bool EditedByAdmin { get; set; }
        public string? AdminNote { get; set; }
        public string ConflictState { get; set; } = "NONE";
        public string Flags { get; set; } = "[]";
        public Guid? DecidedBy { get; set; }
        public DateTime? DecidedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual LawChangeset Changeset { get; set; } = null!;
    }
}
