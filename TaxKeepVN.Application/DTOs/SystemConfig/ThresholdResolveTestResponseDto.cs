using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TaxKeepVN.Application.DTOs.SystemConfig
{
    public class ThresholdResolveTestResponseDto
    {
        [JsonPropertyName("category_code")]
        public string? CategoryCode { get; set; }
        [JsonPropertyName("resolved_threshold")]
        public float ResolvedThreshold { get; set; }
        [JsonPropertyName("note")]
        public string Note { get; set; } = string.Empty;

    }
}