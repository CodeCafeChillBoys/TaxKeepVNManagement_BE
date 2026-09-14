using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TaxKeepVN.Application.DTOs.TaxAI
{
    /// <summary>
    /// Schema đại diện cho dữ liệu yêu cầu chỉnh sửa toàn bộ nội dung của Tax Rule Set
    /// </summary>
    public class TaxRuleUpdateRequest
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("taxYear")]
        public int? TaxYear { get; set; }

        [JsonPropertyName("effectiveFrom")]
        public string? EffectiveFrom { get; set; }

        [JsonPropertyName("effectiveTo")]
        public string? EffectiveTo { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("taxRules")]
        public List<TaxRuleItemUpdateRequest>? TaxRules { get; set; }

        [JsonPropertyName("dependentRules")]
        public List<DependentRuleUpdateRequest>? DependentRules { get; set; }
    }

    /// <summary>
    /// Schema cập nhật cho từng quy tắc thuế con
    /// </summary>
    public class TaxRuleItemUpdateRequest
    {
        [JsonPropertyName("ruleId")]
        public Guid? RuleId { get; set; }

        [JsonPropertyName("ruleCode")]
        public string? RuleCode { get; set; }

        [JsonPropertyName("ruleName")]
        public string? RuleName { get; set; }

        [JsonPropertyName("ruleType")]
        public string? RuleType { get; set; }

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
        public string? Status { get; set; }

        [JsonPropertyName("version")]
        public int? Version { get; set; }
    }

    /// <summary>
    /// Schema cập nhật cho từng quy tắc người phụ thuộc
    /// </summary>
    public class DependentRuleUpdateRequest
    {
        [JsonPropertyName("id")]
        public Guid? Id { get; set; }

        [JsonPropertyName("ruleId")]
        public Guid? RuleId { get; set; }

        [JsonPropertyName("dependentType")]
        public string? DependentType { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("maxAge")]
        public int? MaxAge { get; set; }

        [JsonPropertyName("maxMonthlyIncome")]
        public decimal? MaxMonthlyIncome { get; set; }

        [JsonPropertyName("isStudying")]
        public bool? IsStudying { get; set; }

        [JsonPropertyName("isDisabled")]
        public bool? IsDisabled { get; set; }

        [JsonPropertyName("conditions")]
        public object? Conditions { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }
    }
}
