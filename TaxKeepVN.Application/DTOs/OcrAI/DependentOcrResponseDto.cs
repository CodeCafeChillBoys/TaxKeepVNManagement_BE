using System.Text.Json.Serialization;

namespace TaxKeepVN.Application.DTOs.OcrAI
{
    public class DependentOcrResponseDto
    {
        [JsonPropertyName("documentType")]
        public string? DocumentType { get; set; }

        [JsonPropertyName("fullName")]
        public string? FullName { get; set; }

        [JsonPropertyName("citizenId")]
        public string? CitizenId { get; set; }

        [JsonPropertyName("birthCertNumber")]
        public string? BirthCertNumber { get; set; }

        [JsonPropertyName("birthDate")]
        public string? BirthDate { get; set; }

        [JsonPropertyName("gender")]
        public string? Gender { get; set; }

        [JsonPropertyName("suggestedRelationship")]
        public string? SuggestedRelationship { get; set; }

        [JsonPropertyName("suggestedGroup")]
        public string? SuggestedGroup { get; set; }

        [JsonPropertyName("address")]
        public string? Address { get; set; }

        [JsonPropertyName("isAlreadyRegistered")]
        public bool IsAlreadyRegistered { get; set; } = false;

        [JsonPropertyName("warning")]
        public string? Warning { get; set; }
    }
}
