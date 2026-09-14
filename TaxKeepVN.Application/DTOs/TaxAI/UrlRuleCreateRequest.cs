using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TaxKeepVN.Application.DTOs.TaxAI
{
    public class UrlRuleCreateRequest
    {
        [Required(ErrorMessage = "Tên quy tắc không được để trống.")]
        [MaxLength(100)]
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Loại quy tắc không được để trống (DOMAIN, PREFIX, REGEX, EXACT).")]
        [JsonPropertyName("ruleType")]
        public string RuleType { get; set; } = string.Empty;

        [JsonPropertyName("rule_type")]
        public string? RuleTypeSnake
        {
            get => RuleType;
            set { if (!string.IsNullOrWhiteSpace(value)) RuleType = value; }
        }

        [Required(ErrorMessage = "Pattern không được để trống.")]
        [MaxLength(500)]
        [JsonPropertyName("pattern")]
        public string Pattern { get; set; } = string.Empty;

        [MaxLength(255)]
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; } = true;

        [JsonPropertyName("is_active")]
        public bool? IsActiveSnake
        {
            get => IsActive;
            set { if (value.HasValue) IsActive = value.Value; }
        }
    }
}
