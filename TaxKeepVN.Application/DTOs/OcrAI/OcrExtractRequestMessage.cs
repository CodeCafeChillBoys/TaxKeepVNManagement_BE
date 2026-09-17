using System;
using System.Text.Json.Serialization;

namespace TaxKeepVN.Application.DTOs.OcrAI
{
    public class OcrExtractRequestMessage
    {
        [JsonPropertyName("taskId")]
        public Guid TaskId { get; set; } = Guid.NewGuid();

        [JsonPropertyName("userId")]
        public Guid? UserId { get; set; }

        [JsonPropertyName("fileName")]
        public string FileName { get; set; } = string.Empty;

        [JsonPropertyName("fileUrl")]
        public string? FileUrl { get; set; }

        [JsonPropertyName("fileBase64")]
        public string? FileBase64 { get; set; }

        [JsonPropertyName("filePath")]
        public string? FilePath { get; set; }

        [JsonPropertyName("backFileUrl")]
        public string? BackFileUrl { get; set; }

        [JsonPropertyName("backFileBase64")]
        public string? BackFileBase64 { get; set; }
    }
}
