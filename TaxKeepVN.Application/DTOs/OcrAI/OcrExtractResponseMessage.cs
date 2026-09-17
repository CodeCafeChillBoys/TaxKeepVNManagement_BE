using System;
using System.Text.Json.Serialization;

namespace TaxKeepVN.Application.DTOs.OcrAI
{
    public class OcrExtractResponseMessage
    {
        [JsonPropertyName("taskId")]
        public Guid TaskId { get; set; }

        [JsonPropertyName("userId")]
        public Guid? UserId { get; set; }

        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("statusCode")]
        public int StatusCode { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public ExtractedDependentDataDto? Data { get; set; }

        [JsonPropertyName("errors")]
        public object? Errors { get; set; }

        [JsonPropertyName("processedAt")]
        public string? ProcessedAt { get; set; }
    }
}
