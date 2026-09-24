namespace TaxKeepVN.Application.DTOs.Law
{
    public class LawRuleDefinitionDto
    {
        public string RuleCode { get; set; } = string.Empty;
        public string RuleGroup { get; set; } = string.Empty;
        public string ValueKind { get; set; } = string.Empty;
        public string? DefaultUnit { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool RequiredForFlow3 { get; set; }
        public int? RequiredFromTaxYear { get; set; }
        public bool IsActive { get; set; }
        public bool HasVersions { get; set; }
    }

    public class LawRuleDefinitionInput
    {
        public string RuleCode { get; set; } = string.Empty;
        public string RuleGroup { get; set; } = string.Empty;
        public string ValueKind { get; set; } = string.Empty;
        public string? DefaultUnit { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool RequiredForFlow3 { get; set; }
        public int? RequiredFromTaxYear { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class LawRuleDefinitionUpdateInput
    {
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? DefaultUnit { get; set; }
        public bool RequiredForFlow3 { get; set; }
        public int? RequiredFromTaxYear { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
