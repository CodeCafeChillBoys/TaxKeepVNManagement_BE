using System.Text.Json.Serialization;

namespace TaxKeepVN.Application.DTOs.OcrAI
{
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class RuleValidationResultDto
    {
        [JsonPropertyName("isMatchedRule")]
        public bool IsMatchedRule { get; set; }

        [JsonPropertyName("matchedDocType")]
        public string? MatchedDocType { get; set; }

        [JsonPropertyName("isCompliantWithDescription")]
        public bool? IsCompliantWithDescription { get; set; }

        [JsonPropertyName("notes")]
        public string? Notes { get; set; }
    }
}
