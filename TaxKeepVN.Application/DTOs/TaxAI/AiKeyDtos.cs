using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace TaxKeepVN.Application.DTOs.TaxAI
{
    [ExcludeFromCodeCoverage]
    public class AiKeyCreateRequestDto
    {
        [Required(ErrorMessage = "Tên gợi nhớ của Key không được để trống.")]
        [JsonPropertyName("key_name")]
        public string KeyName { get; set; } = string.Empty;

        [Required(ErrorMessage = "API Key không được để trống.")]
        [JsonPropertyName("api_key")]
        public string ApiKey { get; set; } = string.Empty;

        [JsonPropertyName("provider")]
        public string Provider { get; set; } = "GEMINI";

        [Range(1, int.MaxValue, ErrorMessage = "Độ ưu tiên phải >= 1.")]
        [JsonPropertyName("priority")]
        public int Priority { get; set; } = 1;
    }

    [ExcludeFromCodeCoverage]
    public class AiKeyUpdateRequestDto
    {
        [JsonPropertyName("key_name")]
        public string? KeyName { get; set; }

        [JsonPropertyName("priority")]
        public int? Priority { get; set; }

        [JsonPropertyName("is_active")]
        public bool? IsActive { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class AiKeyResponseDto
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("provider")]
        public string Provider { get; set; } = string.Empty;

        [JsonPropertyName("key_name")]
        public string KeyName { get; set; } = string.Empty;

        [JsonPropertyName("masked_api_key")]
        public string MaskedApiKey { get; set; } = string.Empty;

        [JsonPropertyName("priority")]
        public int Priority { get; set; }

        [JsonPropertyName("is_active")]
        public bool IsActive { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("request_count")]
        public long RequestCount { get; set; }

        [JsonPropertyName("last_used_at")]
        public DateTimeOffset? LastUsedAt { get; set; }

        [JsonPropertyName("cooldown_until")]
        public DateTimeOffset? CooldownUntil { get; set; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset CreatedAt { get; set; }
    }
}
