using System;
using System.Collections.Generic;

namespace TaxKeepVN.Domain.Entities.Law
{
    public class LawRevision
    {
        public int RevisionNo { get; set; }
        public Guid ChangesetId { get; set; }
        public Guid? DocumentId { get; set; }
        public string Description { get; set; } = string.Empty;
        public string CommittedBy { get; set; } = string.Empty;
        public DateTime CommittedAt { get; set; } = DateTime.UtcNow;
        public long MetaVersion { get; set; } = 1;

        // Navigation properties
        public virtual LawChangeset Changeset { get; set; } = null!;
        public virtual LegalDocument? Document { get; set; }
        public virtual ICollection<LawRuleVersion> RuleVersionsCreated { get; set; } = new List<LawRuleVersion>();
        public virtual ICollection<LawRuleVersion> RuleVersionsSuperseded { get; set; } = new List<LawRuleVersion>();
        public virtual ICollection<LegalDocumentRelation> DocumentRelationsCreated { get; set; } = new List<LegalDocumentRelation>();
    }
}
