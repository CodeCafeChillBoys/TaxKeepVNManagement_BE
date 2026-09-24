using System;

namespace TaxKeepVN.Domain.Entities.Law
{
    public class LawOrphanResolution
    {
        public Guid Id { get; set; }
        public Guid ChangesetId { get; set; }
        public Guid VersionId { get; set; }
        public string Action { get; set; } = string.Empty;
        public Guid? GeneratedOpId { get; set; }
        public string? Note { get; set; }
        public Guid? ResolvedBy { get; set; }
        public DateTime? ResolvedAt { get; set; }

        // Navigation properties
        public virtual LawChangeset Changeset { get; set; } = null!;
        public virtual LawRuleVersion RuleVersion { get; set; } = null!;
        public virtual LawChangeOp? GeneratedOp { get; set; }
    }
}
