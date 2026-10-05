using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TaxKeepVN.Application.DTOs.TaxAI
{
    public class UrlRuleUpdateRequest
    {
        [MaxLength(100)]
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [MaxLength(255)]
        [JsonPropertyName("domain")]
        public string? Domain { get; set; }

        [MaxLength(255)]
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("isActive")]
        public bool? IsActive { get; set; }
    }
}
