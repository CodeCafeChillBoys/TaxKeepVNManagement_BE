using System;
using System.Collections.Generic;

namespace TaxKeepVN.Domain.Entities.Law
{
    public class LawRuleDefinition
    {
        public string RuleCode { get; set; } = string.Empty;
        public string RuleGroup { get; set; } = string.Empty;
        public string ValueKind { get; set; } = string.Empty;
        public string? DefaultUnit { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool RequiredForFlow3 { get; set; }
        public int? RequiredFromTaxYear { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<LawRuleVersion> Versions { get; set; } = new List<LawRuleVersion>();
    }
}
