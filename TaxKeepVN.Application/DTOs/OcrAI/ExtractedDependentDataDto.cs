using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TaxKeepVN.Application.DTOs.OcrAI
{
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
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

        [JsonPropertyName("fatherFullName")]
        public string? FatherFullName { get; set; }

        [JsonPropertyName("motherFullName")]
        public string? MotherFullName { get; set; }

        [JsonPropertyName("fatherIdNumber")]
        public string? FatherIdNumber { get; set; }

        [JsonPropertyName("motherIdNumber")]
        public string? MotherIdNumber { get; set; }

        [JsonPropertyName("spouseFullName")]
        public string? SpouseFullName { get; set; }

        [JsonPropertyName("spouseIdNumber")]
        public string? SpouseIdNumber { get; set; }

        [JsonPropertyName("schoolName")]
        public string? SchoolName { get; set; }

        [JsonPropertyName("validTo")]
        public string? ValidTo { get; set; }

        [JsonPropertyName("disabilitySeverity")]
        public string? DisabilitySeverity { get; set; }

        [JsonPropertyName("disabilityLevel")]
        public string? DisabilityLevel { get; set; }

        [JsonPropertyName("laborCapacityLossPercent")]
        public double? LaborCapacityLossPercent { get; set; }

        [JsonPropertyName("householdMembers")]
        public List<HouseholdMemberDto>? HouseholdMembers { get; set; }

        [JsonPropertyName("adoptiveParentFullName")]
        public string? AdoptiveParentFullName { get; set; }

        [JsonPropertyName("adoptiveChildFullName")]
        public string? AdoptiveChildFullName { get; set; }

        [JsonPropertyName("hasSignature")]
        public bool? HasSignature { get; set; }

        [JsonPropertyName("hasStamp")]
        public bool? HasStamp { get; set; }

        [JsonPropertyName("issuingAuthority")]
        public string? IssuingAuthority { get; set; }

        [JsonPropertyName("thresholdValidation")]
        public DependentThresholdValidationResultDto? ThresholdValidation { get; set; }

        [JsonPropertyName("isPassedThreshold")]
        public bool? IsPassedThreshold { get; set; }

        [JsonPropertyName("overallConfidence")]
        public double? OverallConfidence { get; set; }

        [JsonPropertyName("ruleValidation")]
        public RuleValidationResultDto? RuleValidation { get; set; }
    }
}
