using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace TaxKeepVN.Application.DTOs.TaxAI
{
    [ExcludeFromCodeCoverage]
    public class PromptTemplateResponseDto
    {
        [JsonPropertyName("prompt_key")]
        public string PromptKey { get; set; } = string.Empty;

        [JsonPropertyName("system_prompt")]
        public string SystemPrompt { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("version")]
        public int Version { get; set; }

        [JsonPropertyName("variables")]
        public List<string>? Variables { get; set; }

        [JsonPropertyName("is_active")]
        public bool IsActive { get; set; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class PromptTemplateCreateRequestDto
    {
        [Required(ErrorMessage = "Mã khóa prompt không được để trống.")]
        [JsonPropertyName("prompt_key")]
        public string PromptKey { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nội dung system prompt không được để trống.")]
        [JsonPropertyName("system_prompt")]
        public string SystemPrompt { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("variables")]
        public List<string>? Variables { get; set; }

        [JsonPropertyName("is_active")]
        public bool IsActive { get; set; } = true;
    }

    [ExcludeFromCodeCoverage]
    public class PromptTemplateUpdateRequestDto
    {
        [Required(ErrorMessage = "Nội dung system prompt không được để trống.")]
        [JsonPropertyName("system_prompt")]
        public string SystemPrompt { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("variables")]
        public List<string>? Variables { get; set; }

        [JsonPropertyName("is_active")]
        public bool? IsActive { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class PromptTestRenderRequestDto
    {
        [Required(ErrorMessage = "Mã khóa prompt không được để trống.")]
        [JsonPropertyName("prompt_key")]
        public string PromptKey { get; set; } = string.Empty;

        [JsonPropertyName("params")]
        public Dictionary<string, object> Params { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class PromptTestRenderResponseDto
    {
        [JsonPropertyName("prompt_key")]
        public string PromptKey { get; set; } = string.Empty;

        [JsonPropertyName("rendered_prompt")]
        public string RenderedPrompt { get; set; } = string.Empty;

        [JsonPropertyName("variables_used")]
        public List<string> VariablesUsed { get; set; } = new();
    }
}
