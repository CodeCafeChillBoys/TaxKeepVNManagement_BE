using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TaxKeepVN.Application.DTOs.TaxSettlement
{
    /// <summary>Một bậc thuế lũy tiến — đọc từ PIT_BRACKETS_JSON_{year} trong system_configs.</summary>
    public class PitBracketConfigDto
    {
        [JsonPropertyName("bracketNo")]
        public int BracketNo { get; set; }

        [JsonPropertyName("fromMonthly")]
        public decimal FromMonthly { get; set; }

        [JsonPropertyName("toMonthly")]
        public decimal? ToMonthly { get; set; }

        [JsonPropertyName("rate")]
        public decimal Rate { get; set; }

        /// <summary>Khấu trừ nhanh (triệu VNĐ) — Thuế = TNTT * Rate - QuickDeduct.</summary>
        [JsonPropertyName("quickDeduct")]
        public decimal QuickDeduct { get; set; }
    }

    /// <summary>Snapshot mức giảm trừ đã dùng khi tính — lưu audit trail.</summary>
    public class DeductionConfigSnapshotDto
    {
        [JsonPropertyName("personalMonthly")]
        public decimal PersonalMonthly { get; set; }

        [JsonPropertyName("dependentMonthly")]
        public decimal DependentMonthly { get; set; }

        [JsonPropertyName("bhxhRate")]
        public decimal BhxhRate { get; set; }

        [JsonPropertyName("bhytRate")]
        public decimal BhytRate { get; set; }

        [JsonPropertyName("bhtnRate")]
        public decimal BhtnRate { get; set; }

        [JsonPropertyName("smallAmountExemption")]
        public decimal SmallAmountExemption { get; set; }

        [JsonPropertyName("medicalMaxYearly")]
        public decimal MedicalMaxYearly { get; set; } // Tối đa 23 triệu/năm (từ 2026)

        [JsonPropertyName("educationMaxYearly")]
        public decimal EducationMaxYearly { get; set; } // Tối đa 24 triệu/năm (từ 2026)

        [JsonPropertyName("lawGroup")]
        public string LawGroup { get; set; } = string.Empty; // "2025" hoặc "2026"
    }

    // ── Request DTOs ─────────────────────────────────────────────────────────────

    /// <summary>Request xem trước (preview) quyết toán thuế — Bước 1-3 sơ đồ.</summary>
    public class TaxSettlementPreviewRequest
    {
        /// <summary>Năm quyết toán. Ví dụ: 2025 hoặc 2026.</summary>
        public int TaxYear { get; set; }

        /// <summary>
        /// Ngày chốt số liệu (ISO date). Mặc định 31/12/TaxYear nếu không truyền.
        /// Dùng khi người dùng nghỉ việc giữa năm hoặc tính tạm.
        /// </summary>
        public string? CutoffDate { get; set; }

        /// <summary>Các khoản đóng góp từ thiện bổ sung (nếu có). Đơn vị: VNĐ.</summary>
        public decimal CharityDeduction { get; set; } = 0;
    }

    /// <summary>Request chốt và xuất hồ sơ quyết toán — Bước 4-5 sơ đồ.</summary>
    /// <summary>Request chốt và xuất hồ sơ quyết toán — Bước 4-5 sơ đồ.</summary>
    public class TaxSettlementExportRequest
    {
        public int TaxYear { get; set; }
        public string? CutoffDate { get; set; }
        public decimal CharityDeduction { get; set; } = 0;

        /// <summary>
        /// Danh sách ID của income_sources người dùng chọn đưa vào hồ sơ.
        /// Nếu để trống → chọn tất cả income_sources hợp lệ của năm.
        /// </summary>
        public List<Guid>? SelectedIncomeSourceIds { get; set; }

        public string? Note { get; set; }
    }

    // ── Response DTOs ─────────────────────────────────────────────────────────────

    /// <summary>Thông tin từng chứng từ thu nhập trong hồ sơ quyết toán.</summary>
    public class SettlementIncomeItemDto
    {
        public Guid IncomeSourceId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string CompanyTaxCode { get; set; } = string.Empty;
        public decimal GrossIncome { get; set; }
        public decimal TaxWithheld { get; set; }
        public decimal InsuranceDeduction { get; set; }
        public bool IsSelected { get; set; }
    }

    /// <summary>Thông tin người phụ thuộc hợp lệ trong năm tính thuế.</summary>
    public class SettlementDependentItemDto
    {
        public Guid DependentId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Relationship { get; set; } = string.Empty;
        /// <summary>Số tháng được tính giảm trừ trong năm (đã chốt theo CutoffDate).</summary>
        public int ValidMonths { get; set; }
        public decimal DeductionAmount { get; set; }
        public string EffectiveFromMonth { get; set; } = string.Empty;
        public string EffectiveToMonth { get; set; } = string.Empty;
    }

    /// <summary>Chi tiết tính thuế theo từng bậc lũy tiến (để FE hiển thị "Show your work").</summary>
    public class BracketCalculationDetailDto
    {
        public int BracketNo { get; set; }
        public decimal FromMonthly { get; set; }
        public decimal? ToMonthly { get; set; }
        public decimal Rate { get; set; }
        /// <summary>Thu nhập tính thuế theo tháng rơi vào bậc này.</summary>
        public decimal TaxableMonthly { get; set; }
        /// <summary>Thuế phát sinh tại bậc này (tháng).</summary>
        public decimal TaxMonthly { get; set; }
        public bool IsApplied { get; set; }
    }

    /// <summary>Kết quả xem trước / chi tiết hồ sơ quyết toán thuế TNCN.</summary>
    public class TaxSettlementPreviewDto
    {
        public Guid? DossierId { get; set; }
        public int TaxYear { get; set; }
        public string CutoffDate { get; set; } = string.Empty;

        /// <summary>Nhóm luật đang áp dụng: "Luật cũ (≤2025) - 7 bậc" / "Luật 109/2025/QH15 (≥2026) - 5 bậc".</summary>
        public string LawGroupLabel { get; set; } = string.Empty;

        // ── Nguồn thu nhập ─────────────────────────────────────────────────────
        public List<SettlementIncomeItemDto> IncomeItems { get; set; } = new();
        public decimal TotalGrossIncome { get; set; }
        public decimal TotalTaxWithheld { get; set; }
        public decimal TotalInsuranceDeduction { get; set; }

        // ── Người phụ thuộc ────────────────────────────────────────────────────
        public List<SettlementDependentItemDto> DependentItems { get; set; } = new();

        // ── Bảng giảm trừ ─────────────────────────────────────────────────────
        public decimal PersonalDeductionMonthlyRate { get; set; }
        public int PersonalDeductionMonths { get; set; }
        public decimal PersonalDeductionAmount { get; set; }
        public decimal DependentMonthlyRate { get; set; }
        public int DependentDeductionPersonMonths { get; set; }
        public decimal DependentDeductionAmount { get; set; }
        public decimal CharityDeduction { get; set; }
        /// <summary>
        /// Chi phí y tế đã được xác nhận (CONFIRMED) từ biên lai OCR — Tối đa 23 triệu/năm, chỉ áp dụng từ 2026.
        /// </summary>
        public decimal MedicalDeduction { get; set; }
        /// <summary>
        /// Chi phí giáo dục đã được xác nhận (CONFIRMED) từ biên lai OCR — Tối đa 24 triệu/năm, chỉ áp dụng từ 2026.
        /// </summary>
        public decimal EducationDeduction { get; set; }
        public decimal TotalDeductions { get; set; }

        // ── Kết quả tính toán ──────────────────────────────────────────────────
        public decimal TaxableIncomeYearly { get; set; }
        public decimal TaxableIncomeMonthly { get; set; }
        public List<BracketCalculationDetailDto> BracketDetails { get; set; } = new();
        public int AppliedBracketNo { get; set; }
        public decimal TaxPayableMonthly { get; set; }
        public decimal TaxPayable { get; set; }

        // ── Kết luận ──────────────────────────────────────────────────────────
        /// <summary>Số thuế nộp thừa — được hoàn (RefundAmount > 0).</summary>
        public decimal RefundAmount { get; set; }
        /// <summary>Số thuế nộp thiếu — phải nộp thêm (DueAmount > 0).</summary>
        public decimal DueAmount { get; set; }

        /// <summary>
        /// Kết luận cuối cùng cho người dùng.
        /// VD: "Bạn nộp thừa 3.200.000 VNĐ — được hoàn thuế!"
        /// </summary>
        public string SummaryMessage { get; set; } = string.Empty;

        public string Status { get; set; } = "DRAFT";
    }

    /// <summary>Response danh sách hồ sơ đã tạo (rút gọn).</summary>
    public class TaxSettlementListItemDto
    {
        public Guid Id { get; set; }
        public int TaxYear { get; set; }
        public string CutoffDate { get; set; } = string.Empty;
        public decimal TaxPayable { get; set; }
        public decimal RefundAmount { get; set; }
        public decimal DueAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string LawGroupLabel { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? LockedAt { get; set; }
    }

    // ── Admin Config DTOs ─────────────────────────────────────────────────────────

    /// <summary>Request Admin cập nhật biểu thuế lũy tiến cho một năm nhất định.</summary>
    public class UpdatePitBracketsRequest
    {
        /// <summary>Năm luật áp dụng. Ví dụ: 2025 (7 bậc) hoặc 2026 (5 bậc).</summary>
        public int TaxYear { get; set; }
        public List<PitBracketConfigDto> Brackets { get; set; } = new();
    }

    /// <summary>Request Admin cập nhật mức giảm trừ cho một năm nhất định.</summary>
    public class UpdateDeductionRatesRequest
    {
        public int TaxYear { get; set; }
        public decimal PersonalMonthly { get; set; }
        public decimal DependentMonthly { get; set; }
        public decimal BhxhRate { get; set; }
        public decimal BhytRate { get; set; }
        public decimal BhtnRate { get; set; }
        public decimal SmallAmountExemption { get; set; }
    }
}
