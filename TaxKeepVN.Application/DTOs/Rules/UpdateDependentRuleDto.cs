namespace TaxKeepVN.Application.DTOs.Rules
{
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class UpdateDependentRuleDto
    {
        public string? DocName { get; set; }

        public bool IsMandatory { get; set; } = true;

        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
