using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TaxKeepVN.Application.DTOs.SystemConfig
{
    public class SystemConfigUpdateRequestDto
    {
        [JsonPropertyName("config_value")]
        public string? ConfigValue { get; set; }
        [JsonPropertyName("description")]
        public string? Description { get; set; }
        [JsonPropertyName("is_active")]
        public bool? IsActive { get; set; }
        [JsonPropertyName("admin_id")]
        public Guid? AdminId { get; set; }

    }
}