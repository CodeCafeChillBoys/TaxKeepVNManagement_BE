using System.Text.Json.Serialization;

namespace TaxKeepVN.Application.DTOs.OcrAI
{
    public class CertificateExtractedDataDto
    {
        [JsonPropertyName("detectedType")]
        public string? DetectedType { get; set; }

        [JsonPropertyName("isValidType")]
        public bool IsValidType { get; set; }

        [JsonPropertyName("certificateName")]
        public string? CertificateName { get; set; }

        [JsonPropertyName("certificateNumber")]
        public string? CertificateNumber { get; set; }

        [JsonPropertyName("fullName")]
        public string? FullName { get; set; }

        [JsonPropertyName("citizenId")]
        public string? CitizenId { get; set; }

        [JsonPropertyName("dateOfBirth")]
        public string? DateOfBirth { get; set; }

        [JsonPropertyName("issuingAuthority")]
        public string? IssuingAuthority { get; set; }

        [JsonPropertyName("issueDate")]
        public string? IssueDate { get; set; }

        [JsonPropertyName("hasExpiry")]
        public bool HasExpiry { get; set; }

        [JsonPropertyName("expiryDate")]
        public string? ExpiryDate { get; set; }

        [JsonPropertyName("overallConfidence")]
        public double OverallConfidence { get; set; }
    }
}
