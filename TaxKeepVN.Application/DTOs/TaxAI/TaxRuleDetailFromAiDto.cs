using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TaxKeepVN.Application.DTOs.TaxAI
{
    public class TaxRuleDetailFromAiDto
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("data")]
        public TaxRuleExtractionDataFromAiDto? Data { get; set; }
    }

    public class TaxRuleExtractionDataFromAiDto
    {
        [JsonPropertyName("taxRuleSet")]
        public TaxRuleSetFromAiDto? TaxRuleSet { get; set; }

        [JsonPropertyName("dependentRules")]
        public List<DependentRuleFromAiDto>? DependentRules { get; set; }
    }

    public class TaxRuleSetFromAiDto
    {
        [JsonPropertyName("ruleSetId")]
        public Guid? RuleSetId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("taxYear")]
        public int TaxYear { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }
    }

    public class DependentRuleFromAiDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("ruleSetId")]
        public string? RuleSetId { get; set; }

        [JsonPropertyName("dependentType")]
        public string? DependentType { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("maxAge")]
        public int? MaxAge { get; set; }

        [JsonPropertyName("maxMonthlyIncome")]
        public double? MaxMonthlyIncome { get; set; }

        [JsonPropertyName("isStudying")]
        public bool IsStudying { get; set; }

        [JsonPropertyName("isDisabled")]
        public bool IsDisabled { get; set; }

        [JsonPropertyName("conditions")]
        public object? Conditions { get; set; }

        [JsonPropertyName("requiredDocuments")]
        public List<RequiredDocumentFromAiDto>? RequiredDocuments { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }
    }

    public class RequiredDocumentFromAiDto
    {
        [JsonPropertyName("docType")]
        public string? DocType { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("isMandatory")]
        public bool IsMandatory { get; set; } = true;
    }
}
