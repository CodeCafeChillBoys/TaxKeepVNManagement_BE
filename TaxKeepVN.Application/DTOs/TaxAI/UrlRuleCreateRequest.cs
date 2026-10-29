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

        [Required(ErrorMessage = "Tên miền không được để trống.")]
        [MaxLength(255)]
        [JsonPropertyName("domain")]
        public string Domain { get; set; } = string.Empty;

        [MaxLength(255)]
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; } = true;
    }
}
