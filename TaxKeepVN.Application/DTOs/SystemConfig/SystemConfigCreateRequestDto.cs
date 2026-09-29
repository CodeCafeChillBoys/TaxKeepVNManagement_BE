using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TaxKeepVN.Application.DTOs.SystemConfig
{
    public class SystemConfigCreateRequestDto
    {
        [Required]
        [JsonPropertyName("config_key")]
        public string ConfigKey { get; set; } = string.Empty;
        [Required]
        [JsonPropertyName("config_value")]
        public string ConfigValue { get; set; } = string.Empty;
        [JsonPropertyName("description")]
        public string? Description { get; set; }
        [JsonPropertyName("admin_id")]
        public Guid? AdminId { get; set; }

    }
}