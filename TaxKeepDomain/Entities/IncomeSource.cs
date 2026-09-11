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

        /// <summary>Năm tính thuế (ví dụ: 2026)</summary>
        [Column("tax_year")]
        public int TaxYear { get; set; } = 2026;

        /// <summary>Tổng thu nhập chịu thuế nhận được từ cơ quan/tổ chức này trong năm</summary>
        [Column("total_income", TypeName = "decimal(18,2)")]
        public decimal TotalIncome { get; set; } = 0;

        /// <summary>Số tiền thuế TNCN đã khấu trừ tại nguồn</summary>
        [Column("tax_withheld", TypeName = "decimal(18,2)")]
        public decimal TaxWithheld { get; set; } = 0;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Alias cho CompanyTaxCode để tương thích đặc tả companyTaxId</summary>
        [NotMapped]
        public string CompanyTaxId
        {
            get => CompanyTaxCode;
            set => CompanyTaxCode = value;
        }
    }
}
