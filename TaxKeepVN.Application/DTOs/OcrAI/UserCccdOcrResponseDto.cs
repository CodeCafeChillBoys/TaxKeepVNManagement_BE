using System.Text.Json.Serialization;

namespace TaxKeepVN.Application.DTOs.OcrAI
{
    public class UserCccdOcrResponseDto
    {
        [JsonPropertyName("fullName")]
        public string? FullName { get; set; }

        [JsonPropertyName("citizenId")]
        public string? CitizenId { get; set; }

        [JsonPropertyName("dateOfBirth")]
        public string? DateOfBirth { get; set; }

        [JsonPropertyName("gender")]
        public string? Gender { get; set; }

        [JsonPropertyName("address")]
        public string? Address { get; set; }

        [JsonPropertyName("isAvailable")]
        public bool IsAvailable { get; set; } = true;

        [JsonPropertyName("warning")]
        public string? Warning { get; set; }
    }
}
