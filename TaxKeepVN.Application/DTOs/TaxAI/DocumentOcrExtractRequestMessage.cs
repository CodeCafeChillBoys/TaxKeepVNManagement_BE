using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TaxKeepVN.Application.DTOs.TaxAI
{
    public class CategoryItemDto
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
    }

    public class DocumentOcrExtractRequestMessage
    {
        [JsonPropertyName("taskId")]
        public Guid TaskId { get; set; }

        [JsonPropertyName("periodId")]
        public Guid PeriodId { get; set; }

        [JsonPropertyName("userId")]
        public Guid UserId { get; set; }

        [JsonPropertyName("targetYear")]
        public int TargetYear { get; set; }

        [JsonPropertyName("fileUrl")]
        public string FileUrl { get; set; } = string.Empty;

        [JsonPropertyName("originalFilename")]
        public string OriginalFilename { get; set; } = string.Empty;

        [JsonPropertyName("categories")]
        public List<CategoryItemDto> Categories { get; set; } = new();

        [JsonPropertyName("appliedThreshold")]
        public double? AppliedThreshold { get; set; }
    }
}

