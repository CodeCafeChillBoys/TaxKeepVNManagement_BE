using System;
using System.ComponentModel.DataAnnotations;

namespace TaxKeepVN.Application.DTOs.Requests.Income
{
    public class UpdateIncomeRequest
    {
        [Required]
        [MaxLength(255)]
        public string OrganizationName { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? TaxIdNumber { get; set; }

        [Required]
        public int Month { get; set; }

        [Required]
        public int Year { get; set; }

        [Required]
        public decimal TotalTaxableIncome { get; set; }

        [Required]
        public decimal InsuranceDeducted { get; set; }

        public decimal TaxAlreadyDeducted { get; set; }

        public string? PayslipFileUrl { get; set; }
    }
}
