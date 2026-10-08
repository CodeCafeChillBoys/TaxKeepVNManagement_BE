using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TaxKeepVN.Application.DTOs.OcrAI
{
    public class IncomeThresholdValidationResultDto
    {
        [JsonPropertyName("appliedThreshold")]
        public float AppliedThreshold { get; set; }

        [JsonPropertyName("overallConfidence")]
        public float OverallConfidence { get; set; }

        [JsonPropertyName("isPassedThreshold")]
        public bool IsPassedThreshold { get; set; }

        [JsonPropertyName("lowConfidenceFields")]
        public List<string> LowConfidenceFields { get; set; } = new List<string>();

        [JsonPropertyName("warningMessage")]
        public string? WarningMessage { get; set; }
    }
}
