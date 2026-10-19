using System;
using System.Text.Json.Serialization;

namespace TaxKeepVN.Application.DTOs.OcrAI
{
    public class CertificateOcrResponseMessage
    {
        [JsonPropertyName("taskId")]
        public Guid TaskId { get; set; }

        [JsonPropertyName("userId")]
        public Guid? UserId { get; set; }

        [JsonPropertyName("applicationId")]
        public Guid? ApplicationId { get; set; }

        [JsonPropertyName("certificateId")]
        public Guid? CertificateId { get; set; }

        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("statusCode")]
        public int StatusCode { get; set; } = 200;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public CertificateExtractedDataDto? Data { get; set; }

        [JsonPropertyName("errors")]
        public object? Errors { get; set; }

        [JsonPropertyName("processedAt")]
        public string? ProcessedAt { get; set; }
    }
}
