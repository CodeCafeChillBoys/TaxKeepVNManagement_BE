using System.Text.Json.Serialization;

namespace TaxKeepVN.Application.DTOs.OcrAI
{
    public class IncomeExtractedDataDto
    {
        [JsonPropertyName("organizationName")]
        public string OrganizationName { get; set; } = string.Empty;

        [JsonPropertyName("taxIdNumber")]
        public string? TaxIdNumber { get; set; }

        [JsonPropertyName("month")]
        public int Month { get; set; }

        [JsonPropertyName("year")]
        public int Year { get; set; }

        [JsonPropertyName("totalTaxableIncome")]
        public decimal? TotalTaxableIncome { get; set; }

        [JsonPropertyName("insuranceDeducted")]
        public decimal InsuranceDeducted { get; set; }

        [JsonPropertyName("taxAlreadyDeducted")]
        public decimal TaxAlreadyDeducted { get; set; }

        [JsonPropertyName("payslipFileUrl")]
        public string? PayslipFileUrl { get; set; }

        [JsonPropertyName("employeeName")]
        public string? EmployeeName { get; set; }

        [JsonPropertyName("grossSalary")]
        public decimal? GrossSalary { get; set; }

        [JsonPropertyName("netSalary")]
        public decimal? NetSalary { get; set; }

        [JsonPropertyName("thresholdValidation")]
        public IncomeThresholdValidationResultDto ThresholdValidation { get; set; } = new IncomeThresholdValidationResultDto();
    }
}
