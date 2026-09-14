using System;
using System.Text.Json.Serialization;

namespace TaxKeepVN.Application.DTOs.TaxAI
{
    public class UrlRuleResponse
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("ruleType")]
        public string RuleType { get; set; } = string.Empty;

        [JsonPropertyName("rule_type")]
        public string? RuleTypeSnake
        {
            get => RuleType;
            set { if (!string.IsNullOrWhiteSpace(value)) RuleType = value; }
        }

        [JsonPropertyName("pattern")]
        public string Pattern { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; }

        [JsonPropertyName("is_active")]
        public bool? IsActiveSnake
        {
            get => IsActive;
            set { if (value.HasValue) IsActive = value.Value; }
        }

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime? CreatedAtSnake
        {
            get => CreatedAt;
            set { if (value.HasValue) CreatedAt = value.Value; }
        }

        [JsonPropertyName("updatedAt")]
        public DateTime UpdatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTime? UpdatedAtSnake
        {
            get => UpdatedAt;
            set { if (value.HasValue) UpdatedAt = value.Value; }
        }
    }
}
