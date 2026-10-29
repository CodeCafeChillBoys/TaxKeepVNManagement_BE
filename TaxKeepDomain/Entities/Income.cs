using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaxKeepVN.Domain.Entities
{
    [Table("incomes")]
    public class Income
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("user_id")]
        public Guid UserId { get; set; }

        [Column("organization_name")]
        [MaxLength(255)]
        public string OrganizationName { get; set; } = string.Empty;

        [Column("tax_id_number")]
        [MaxLength(20)]
        public string? TaxIdNumber { get; set; }

        [Column("month")]
        public int Month { get; set; }

        [Column("year")]
        public int Year { get; set; }

        [Column("total_taxable_income")]
        public decimal TotalTaxableIncome { get; set; } = 0;

        [Column("insurance_deducted")]
        public decimal InsuranceDeducted { get; set; } = 0;

        [Column("tax_already_deducted")]
        public decimal TaxAlreadyDeducted { get; set; } = 0;

        [Column("payslip_file_url")]
        public string? PayslipFileUrl { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        [ForeignKey("UserId")]
        public User? User { get; set; }
    }
}
