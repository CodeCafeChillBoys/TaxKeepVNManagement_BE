using System.Text.Json.Serialization;

namespace TaxKeepVN.Application.DTOs.OcrAI
{
    public class ExtractedDependentDataDto
    {
        [JsonPropertyName("documentType")]
        public string? DocumentType { get; set; }

        [JsonPropertyName("citizenId")]
        public string? CitizenId { get; set; }

        [JsonPropertyName("fullName")]
        public string? FullName { get; set; }

        [JsonPropertyName("birthDate")]
        public string? BirthDate { get; set; }

        [JsonPropertyName("gender")]
        public string? Gender { get; set; }

        [JsonPropertyName("nationality")]
        public string? Nationality { get; set; }

        [JsonPropertyName("originPlace")]
        public string? OriginPlace { get; set; }

        [JsonPropertyName("residencePlace")]
        public string? ResidencePlace { get; set; }

        [JsonPropertyName("expiryDate")]
        public string? ExpiryDate { get; set; }

        [JsonPropertyName("issueDate")]
        public string? IssueDate { get; set; }

        [JsonPropertyName("suggestedGroup")]
        public string? SuggestedGroup { get; set; }

        [JsonPropertyName("documentNumber")]
        public string? DocumentNumber { get; set; }

        [JsonPropertyName("isReadable")]
        public bool IsReadable { get; set; } = true;

        [JsonPropertyName("unreadableReason")]
        public string? UnreadableReason { get; set; }
    }
}
