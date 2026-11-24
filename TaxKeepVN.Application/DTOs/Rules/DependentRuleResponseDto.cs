using System;

namespace TaxKeepVN.Application.DTOs.Rules
{
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class DependentRuleResponseDto
    {
        public Guid RuleId { get; set; }
        public string TargetGroup { get; set; } = string.Empty;
        public string DocType { get; set; } = string.Empty;
        public string? DocName { get; set; }
        public bool IsMandatory { get; set; }
        public string? Description { get; set; }
        public string? AlternativeGroup { get; set; }
        public string Source { get; set; } = "ADMIN";
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
