namespace TaxKeepVN.Application.DTOs.IncomeSources
{
    public class IncomeSourceSummaryDto
    {
        public int TaxYear { get; set; }
        public decimal TotalIncome { get; set; }
        public decimal TotalTaxWithheld { get; set; }
        public decimal TotalInsuranceDeducted { get; set; }
        public int TotalSources { get; set; }
    }
}
