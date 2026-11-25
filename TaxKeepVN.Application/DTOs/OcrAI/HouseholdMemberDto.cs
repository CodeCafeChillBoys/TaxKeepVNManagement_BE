using System.Text.Json.Serialization;

namespace TaxKeepVN.Application.DTOs.OcrAI
{
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class HouseholdMemberDto
    {
        [JsonPropertyName("fullName")]
        public string? FullName { get; set; }

        [JsonPropertyName("citizenId")]
        public string? CitizenId { get; set; }

        [JsonPropertyName("birthDate")]
        public string? BirthDate { get; set; }

        [JsonPropertyName("relationship")]
        public string? Relationship { get; set; }
    }
}
