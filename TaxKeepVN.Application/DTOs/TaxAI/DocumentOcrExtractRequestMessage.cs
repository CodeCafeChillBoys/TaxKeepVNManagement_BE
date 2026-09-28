using System;
using System.Text.Json.Serialization;

namespace TaxKeepVN.Application.DTOs.TaxAI
{
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

        [JsonPropertyName("appliedThreshold")]
        public double? AppliedThreshold { get; set; }
    }
}
