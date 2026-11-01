using System.Collections.Generic;

namespace TaxKeepVN.Application.DTOs.IncomeSources
{
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class IncomeSourceCrossCheckRequestDto
    {
        public string CompanyName { get; set; } = string.Empty;
        public int TaxYear { get; set; }
        public decimal CertificateTotalIncome { get; set; }
        public decimal CertificateTaxWithheld { get; set; }
        public decimal CertificateInsuranceDeducted { get; set; }
    }
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]

    public class IncomeSourceCrossCheckResponseDto
    {
        public bool IsMatch { get; set; }
        public decimal SummedTotalIncome { get; set; }
        public decimal SummedTaxWithheld { get; set; }
        public decimal SummedInsuranceDeducted { get; set; }
        public decimal DiffTotalIncome { get; set; }
        public decimal DiffTaxWithheld { get; set; }
        public decimal DiffInsuranceDeducted { get; set; }
        public List<string> MismatchMessages { get; set; } = new List<string>();
    }
}
