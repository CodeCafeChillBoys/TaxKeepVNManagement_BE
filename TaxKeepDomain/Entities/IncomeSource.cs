using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaxKeepVN.Domain.Entities
{
    [Table("income_sources")]
    public class IncomeSource
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Column("taxpayer_id")]
        public Guid TaxpayerId { get; set; }

        [Column("company_name")]
        [MaxLength(255)]
        public string CompanyName { get; set; } = string.Empty;

        [Column("company_tax_code")]
        [MaxLength(20)]
        public string CompanyTaxCode { get; set; } = string.Empty;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
