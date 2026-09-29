using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TaxKeepVN.Application.DTOs.SystemConfig
{
    public class SystemConfigResponseDto
    {
        [JsonPropertyName("config_key")]
        public string ConfigKey { get; set; } = string.Empty;
        [JsonPropertyName("config_value")]
        public string ConfigValue { get; set; } = string.Empty;
        [JsonPropertyName("data_type")]
        public string DataType { get; set; } = string.Empty;
        [JsonPropertyName("description")]
        public string? Description { get; set; }
        [JsonPropertyName("is_active")]
        public bool IsActive { get; set; }
        [JsonPropertyName("is_deleted")]
        public bool IsDeleted { get; set; }
        [JsonPropertyName("deleted_at")]
        public DateTime? DeletedAt { get; set; }
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }
        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }
        [JsonPropertyName("admin_id")]
        public Guid? AdminId { get; set; }
    }
}