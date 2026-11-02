namespace TaxKeepVN.Application.DTOs.Rules
{
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class UpdateDependentRuleDto
    {
        public bool IsMandatory { get; set; } = true;

        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
