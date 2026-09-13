using System;
using System.Text.Json.Serialization;

namespace TaxKeepVN.Application.DTOs.TaxAI
{
    public class TaxRuleExtractResponseMessage
    {
        [JsonPropertyName("taskId")]
        public Guid TaskId { get; set; }

        [JsonPropertyName("adminId")]
        public Guid? AdminId { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty; // "SUCCESS" hoặc "FAILED"

        [JsonPropertyName("ruleSetId")]
        public Guid? RuleSetId { get; set; }

        [JsonPropertyName("data")]
        public object? Data { get; set; }

        [JsonPropertyName("errorMessage")]
        public string? ErrorMessage { get; set; }

        [JsonPropertyName("processedAt")]
        public string ProcessedAt { get; set; } = string.Empty;
    }
}
