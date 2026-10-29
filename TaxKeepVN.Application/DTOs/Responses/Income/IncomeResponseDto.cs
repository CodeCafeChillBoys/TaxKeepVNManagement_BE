using System;

namespace TaxKeepVN.Application.DTOs.Responses.Income
{
    public class IncomeResponseDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string OrganizationName { get; set; } = string.Empty;
        public string? TaxIdNumber { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public decimal TotalTaxableIncome { get; set; }
        public decimal InsuranceDeducted { get; set; }
        public decimal TaxAlreadyDeducted { get; set; }
        public string? PayslipFileUrl { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
