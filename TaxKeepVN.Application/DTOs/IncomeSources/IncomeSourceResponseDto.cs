using System;
using System.Text.Json.Serialization;

namespace TaxKeepVN.Application.DTOs.IncomeSources
{
    public class IncomeSourceResponseDto
    {
        public Guid Id { get; set; }
        public string CompanyName { get; set; } = string.Empty;

        [JsonPropertyName("companyTaxId")]
        public string CompanyTaxId { get; set; } = string.Empty;

        public string CompanyTaxCode { get; set; } = string.Empty;

        public int TaxYear { get; set; }
        public decimal TotalIncome { get; set; }
        public decimal TaxWithheld { get; set; }

        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
