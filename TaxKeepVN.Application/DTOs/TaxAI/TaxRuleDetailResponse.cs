using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TaxKeepVN.Application.DTOs.TaxAI
{
    /// <summary>
    /// Response trả về khi Review (GET) hoặc Edit (PUT) toàn bộ nội dung bộ quy tắc thuế từ AI Service
    /// </summary>
    public class TaxRuleDetailResponse
    {
        [JsonPropertyName("message")]
        public string Message { get; set; } = "Tax rule set retrieved successfully.";

        [JsonPropertyName("warning")]
        public string? Warning { get; set; }

        [JsonPropertyName("data")]
        public TaxRuleExtractionDataResponse Data { get; set; } = new();
    }

    public class TaxRuleExtractionDataResponse
    {
        [JsonPropertyName("taxRuleSet")]
        public TaxRuleSetResponse TaxRuleSet { get; set; } = new();

        [JsonPropertyName("taxRules")]
        public List<TaxRuleItemResponse> TaxRules { get; set; } = new();

        [JsonPropertyName("dependentRules")]
        public List<DependentRuleResponse>? DependentRules { get; set; }

        [JsonPropertyName("verification")]
        public object? Verification { get; set; }

        [JsonPropertyName("warning")]
        public string? Warning { get; set; }
    }

    public class TaxRuleSetResponse
    {
        [JsonPropertyName("ruleSetId")]
        public Guid? RuleSetId { get; set; }

        [JsonPropertyName("adminId")]
        public Guid? AdminId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("taxYear")]
        public int TaxYear { get; set; }

        [JsonPropertyName("effectiveFrom")]
        public string? EffectiveFrom { get; set; }

        [JsonPropertyName("effectiveTo")]
        public string? EffectiveTo { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = "Draft";

        [JsonPropertyName("approvedBy")]
        public Guid? ApprovedBy { get; set; }

        [JsonPropertyName("approvedAt")]
        public DateTime? ApprovedAt { get; set; }
    }

    public class TaxRuleItemResponse
    {
        [JsonPropertyName("ruleCode")]
        public string RuleCode { get; set; } = string.Empty;

        [JsonPropertyName("ruleName")]
        public string RuleName { get; set; } = string.Empty;

        [JsonPropertyName("ruleType")]
        public string RuleType { get; set; } = string.Empty;

        [JsonPropertyName("condition")]
        public object? Condition { get; set; }

        [JsonPropertyName("value")]
        public decimal? Value { get; set; }

        [JsonPropertyName("unit")]
        public string? Unit { get; set; }

        [JsonPropertyName("effectiveFrom")]
        public string? EffectiveFrom { get; set; }

        [JsonPropertyName("effectiveTo")]
        public string? EffectiveTo { get; set; }

        [JsonPropertyName("legalDocument")]
        public string? LegalDocument { get; set; }

        [JsonPropertyName("article")]
        public string? Article { get; set; }

        [JsonPropertyName("clause")]
        public string? Clause { get; set; }

        [JsonPropertyName("point")]
        public string? Point { get; set; }

        [JsonPropertyName("sourceUrl")]
        public string? SourceUrl { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = "Draft";
    }

    public class DependentRuleResponse
    {
        [JsonPropertyName("id")]
        public Guid? Id { get; set; }

        [JsonPropertyName("ruleId")]
        public Guid? RuleId { get; set; }

        [JsonPropertyName("ruleSetId")]
        public Guid? RuleSetId { get; set; }

        [JsonPropertyName("dependentType")]
        public string DependentType { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("maxAge")]
        public int? MaxAge { get; set; }

        [JsonPropertyName("maxMonthlyIncome")]
        public decimal? MaxMonthlyIncome { get; set; }

        [JsonPropertyName("isStudying")]
        public bool IsStudying { get; set; } = false;

        [JsonPropertyName("isDisabled")]
        public bool IsDisabled { get; set; } = false;

        [JsonPropertyName("conditions")]
        public object? Conditions { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = "Draft";
    }
}
