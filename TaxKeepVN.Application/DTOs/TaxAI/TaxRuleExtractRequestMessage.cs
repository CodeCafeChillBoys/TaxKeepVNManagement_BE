using System;
using System.Text.Json.Serialization;

namespace TaxKeepVN.Application.DTOs.TaxAI
{
    public class TaxRuleExtractRequestMessage
    {
        [JsonPropertyName("taskId")]
        public Guid TaskId { get; set; } = Guid.NewGuid();

        [JsonPropertyName("adminId")]
        public Guid? AdminId { get; set; }

        [JsonPropertyName("fileName")]
        public string FileName { get; set; } = string.Empty;

        [JsonPropertyName("fileUrl")]
        public string? FileUrl { get; set; }

        [JsonPropertyName("fileBase64")]
        public string? FileBase64 { get; set; }

        [JsonPropertyName("filePath")]
        public string? FilePath { get; set; }

        [JsonPropertyName("taxYear")]
        public int TaxYear { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("sourceUrl")]
        public string? SourceUrl { get; set; }
    }
}
