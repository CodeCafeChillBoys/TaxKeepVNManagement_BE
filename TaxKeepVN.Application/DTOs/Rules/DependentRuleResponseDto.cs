using System;

namespace TaxKeepVN.Application.DTOs.Rules
{
    public class DependentRuleResponseDto
    {
        public Guid RuleId { get; set; }
        public string TargetGroup { get; set; } = string.Empty;
        public string DocType { get; set; } = string.Empty;
        public bool IsMandatory { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
