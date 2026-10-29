using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TaxKeepVN.Application.DTOs.OcrAI
{
    public class IncomeOcrResponseDto
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("statusCode")]
        public int StatusCode { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public IncomeExtractedDataDto? Data { get; set; }

        [JsonPropertyName("errors")]
        public object? Errors { get; set; }
    }
}
