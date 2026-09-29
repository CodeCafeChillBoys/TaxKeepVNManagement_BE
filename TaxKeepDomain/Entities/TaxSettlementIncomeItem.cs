using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaxKeepVN.Domain.Entities
{
    /// <summary>
    /// Lưu vết từng chứng từ thu nhập được chọn đưa vào hồ sơ quyết toán.
    /// Cho phép người dùng chọn / bỏ chọn (deselect) chứng từ trước khi chốt.
    /// </summary>
    [Table("tax_settlement_income_items")]
    public class TaxSettlementIncomeItem
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("dossier_id")]
        public Guid DossierId { get; set; }

        [Column("income_source_id")]
        public Guid IncomeSourceId { get; set; }

        [Column("company_name")]
        [MaxLength(255)]
        public string CompanyName { get; set; } = string.Empty;

        [Column("company_tax_code")]
        [MaxLength(20)]
        public string CompanyTaxCode { get; set; } = string.Empty;

        [Column("gross_income", TypeName = "decimal(18,2)")]
        public decimal GrossIncome { get; set; } = 0;

        [Column("tax_withheld", TypeName = "decimal(18,2)")]
        public decimal TaxWithheld { get; set; } = 0;

        /// <summary>Khoản bảo hiểm bắt buộc tính được cho chứng từ này (BHXH + BHYT + BHTN).</summary>
        [Column("insurance_deduction", TypeName = "decimal(18,2)")]
        public decimal InsuranceDeduction { get; set; } = 0;

        /// <summary>Người dùng có chọn đưa chứng từ này vào tính toán không.</summary>
        [Column("is_selected")]
        public bool IsSelected { get; set; } = true;

        // ── Navigation ──────────────────────────────────────────────────────────
        [ForeignKey(nameof(DossierId))]
        public TaxSettlementDossier? Dossier { get; set; }
    }
}
