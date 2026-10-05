using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace TaxKeepVN.Domain.Entities
{
    /// <summary>
    /// Hồ sơ quyết toán thuế TNCN theo năm của người nộp thuế.
    /// Lưu toàn bộ số liệu tính toán, snapshot bộ luật đã áp dụng (audit trail) và trạng thái xuất file.
    /// </summary>
    [Table("tax_settlement_dossiers")]
    public class TaxSettlementDossier
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("taxpayer_id")]
        public Guid TaxpayerId { get; set; }

        /// <summary>Năm quyết toán thuế. Ví dụ: 2025, 2026.</summary>
        [Column("tax_year")]
        public int TaxYear { get; set; }

        /// <summary>Ngày chốt số liệu. Mặc định 31/12 của năm, có thể tùy chỉnh (VD: nghỉ việc giữa năm).</summary>
        [Column("cutoff_date")]
        public DateOnly CutoffDate { get; set; }

        // ── Thu nhập & thuế đã khấu trừ ────────────────────────────────────────
        [Column("total_gross_income", TypeName = "decimal(18,2)")]
        public decimal TotalGrossIncome { get; set; } = 0;

        [Column("total_tax_withheld", TypeName = "decimal(18,2)")]
        public decimal TotalTaxWithheld { get; set; } = 0;

        [Column("total_insurance_deduction", TypeName = "decimal(18,2)")]
        public decimal TotalInsuranceDeduction { get; set; } = 0;

        // ── Các khoản giảm trừ ──────────────────────────────────────────────────
        [Column("personal_deduction_months")]
        public int PersonalDeductionMonths { get; set; } = 0;

        [Column("personal_deduction_amount", TypeName = "decimal(18,2)")]
        public decimal PersonalDeductionAmount { get; set; } = 0;

        /// <summary>Tổng số (người × tháng) cho tất cả người phụ thuộc hợp lệ.</summary>
        [Column("dependent_deduction_person_months")]
        public int DependentDeductionPersonMonths { get; set; } = 0;

        [Column("dependent_deduction_amount", TypeName = "decimal(18,2)")]
        public decimal DependentDeductionAmount { get; set; } = 0;

        [Column("charity_deduction", TypeName = "decimal(18,2)")]
        public decimal CharityDeduction { get; set; } = 0;

        /// <summary>
        /// Chi phí y tế từ biên lai OCR đã xác nhận (CONFIRMED) — Tối đa 23 triệu/năm, chỉ áp dụng từ năm 2026.
        /// </summary>
        [Column("medical_deduction", TypeName = "decimal(18,2)")]
        public decimal MedicalDeduction { get; set; } = 0;

        /// <summary>
        /// Chi phí giáo dục từ biên lai OCR đã xác nhận (CONFIRMED) — Tối đa 24 triệu/năm, chỉ áp dụng từ năm 2026.
        /// </summary>
        [Column("education_deduction", TypeName = "decimal(18,2)")]
        public decimal EducationDeduction { get; set; } = 0;

        [Column("total_deductions", TypeName = "decimal(18,2)")]
        public decimal TotalDeductions { get; set; } = 0;

        // ── Kết quả tính toán ───────────────────────────────────────────────────
        [Column("taxable_income_yearly", TypeName = "decimal(18,2)")]
        public decimal TaxableIncomeYearly { get; set; } = 0;

        [Column("taxable_income_monthly", TypeName = "decimal(18,2)")]
        public decimal TaxableIncomeMonthly { get; set; } = 0;

        /// <summary>Thuế TNCN thực tế phải nộp theo luật (áp biểu thuế lũy tiến).</summary>
        [Column("tax_payable", TypeName = "decimal(18,2)")]
        public decimal TaxPayable { get; set; } = 0;

        /// <summary>Số thuế nộp thừa — được hoàn lại (nếu TaxPayable &lt; TotalTaxWithheld).</summary>
        [Column("refund_amount", TypeName = "decimal(18,2)")]
        public decimal RefundAmount { get; set; } = 0;

        /// <summary>Số thuế nộp thiếu — phải nộp thêm (nếu TaxPayable &gt; TotalTaxWithheld, vượt ngưỡng miễn).</summary>
        [Column("due_amount", TypeName = "decimal(18,2)")]
        public decimal DueAmount { get; set; } = 0;

        /// <summary>Bậc thuế đã áp dụng (1–7 cũ / 1–5 mới). Lưu để hiển thị cho người dùng.</summary>
        [Column("applied_bracket_no")]
        public int AppliedBracketNo { get; set; } = 0;

        // ── Snapshot cấu hình đã dùng khi tính (Audit Trail pháp lý) ────────────
        /// <summary>Biểu thuế đã áp dụng tại thời điểm tính — JSON snapshot từ system_configs.</summary>
        [Column("pit_brackets_snapshot", TypeName = "text")]
        public string? PitBracketsSnapshot { get; set; }

        /// <summary>Mức giảm trừ đã áp dụng tại thời điểm tính — JSON snapshot từ system_configs.</summary>
        [Column("deduction_config_snapshot", TypeName = "text")]
        public string? DeductionConfigSnapshot { get; set; }

        // ── Quản lý hồ sơ ──────────────────────────────────────────────────────
        /// <summary>DRAFT: đang xem trước, LOCKED: đã xuất chính thức và chốt.</summary>
        [Column("status")]
        [MaxLength(50)]
        public string Status { get; set; } = "DRAFT";

        [Column("note")]
        public string? Note { get; set; }

        [Column("zip_file_url")]
        public string? ZipFileUrl { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("locked_at")]
        public DateTime? LockedAt { get; set; }

        // ── Navigation ──────────────────────────────────────────────────────────
        public ICollection<TaxSettlementIncomeItem> IncomeItems { get; set; } = new List<TaxSettlementIncomeItem>();
    }
}
