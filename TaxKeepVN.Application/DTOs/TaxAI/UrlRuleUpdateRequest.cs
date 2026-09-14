using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TaxKeepVN.Application.DTOs.TaxAI
{
    public class UrlRuleUpdateRequest
    {
        [MaxLength(100)]
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("ruleType")]
        public string? RuleType { get; set; }

        [JsonPropertyName("rule_type")]
        public string? RuleTypeSnake
        {
            get => RuleType;
            set { if (!string.IsNullOrWhiteSpace(value)) RuleType = value; }
        }

        [MaxLength(500)]
        [JsonPropertyName("pattern")]
        public string? Pattern { get; set; }

        [MaxLength(255)]
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("isActive")]
        public bool? IsActive { get; set; }

        [JsonPropertyName("is_active")]
        public bool? IsActiveSnake
        {
            get => IsActive;
            set { if (value.HasValue) IsActive = value.Value; }
        }
    }
}
