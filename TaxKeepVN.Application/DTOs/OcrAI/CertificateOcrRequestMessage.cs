using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TaxKeepVN.Application.DTOs.OcrAI
{
    public class CertificateOcrRequestMessage
    {
        [JsonPropertyName("taskId")]
        public Guid TaskId { get; set; } = Guid.NewGuid();

        [JsonPropertyName("userId")]
        public Guid? UserId { get; set; }

        [JsonPropertyName("applicationId")]
        public Guid? ApplicationId { get; set; }

        [JsonPropertyName("certificateId")]
        public Guid? CertificateId { get; set; }

        [JsonPropertyName("fileName")]
        public string FileName { get; set; } = string.Empty;

        [JsonPropertyName("fileUrl")]
        public string? FileUrl { get; set; }

        [JsonPropertyName("fileBase64")]
        public string? FileBase64 { get; set; }

        [JsonPropertyName("filePath")]
        public string? FilePath { get; set; }

        [JsonPropertyName("allowedTypes")]
        public List<Dictionary<string, string>>? AllowedTypes { get; set; }
    }
}
