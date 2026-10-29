using System;
using System.Collections.Generic;

namespace TaxKeepVN.Application.DTOs.TaxSettlement
{
    /// <summary>
    /// Thông tin bổ sung người nộp thuế cung cấp khi xuất Tờ khai Mẫu 02/QTT-TNCN (tùy chọn, fallback vào Profile).
    /// </summary>
    public class TaxSettlementExportPdfRequest
    {
        /// <summary>Tên Cơ quan thuế quản lý quyết toán (VD: Cục Thuế TP. Hồ Chí Minh, Chi cục Thuế Quận 1).</summary>
        public string? TaxOfficeName { get; set; }

        /// <summary>Mã số thuế của người nộp thuế.</summary>
        public string? TaxCode { get; set; }

        /// <summary>Số tài khoản ngân hàng để nhận tiền hoàn thuế (nếu có).</summary>
        public string? BankAccountNumber { get; set; }

        /// <summary>Tên ngân hàng mở tài khoản (VD: Vietcombank, Techcombank, MB Bank).</summary>
        public string? BankName { get; set; }

        /// <summary>Địa chỉ liên hệ / thường trú.</summary>
        public string? ContactAddress { get; set; }

        /// <summary>Số điện thoại liên hệ.</summary>
        public string? PhoneNumber { get; set; }

        /// <summary>Email liên hệ.</summary>
        public string? Email { get; set; }
    }

    /// <summary>
    /// Request đóng gói trọn bộ hồ sơ quyết toán (PDF + chứng từ gốc) thành file ZIP.
    /// </summary>
    public class TaxSettlementExportZipRequest : TaxSettlementExportPdfRequest
    {
        /// <summary>Ghi chú thêm cho gói hồ sơ lưu trữ.</summary>
        public string? Note { get; set; }
    }

    /// <summary>
    /// Kết quả trả về sau khi đóng gói hồ sơ ZIP và sinh Download URL có thời hạn.
    /// </summary>
    public class TaxSettlementPackageZipResponse
    {
        public Guid DossierId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public long FileSizeBytes { get; set; }
        public int TotalDocumentsIncluded { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Payload lưu trong Signed Download Token (HMAC-SHA256).
    /// </summary>
    public class DownloadTokenPayload
    {
        public Guid DossierId { get; set; }
        public Guid TaxpayerId { get; set; }
        public string FileType { get; set; } = "pdf"; // "pdf" hoặc "zip"
        public string RelativeFilePath { get; set; } = string.Empty;
        public string DownloadFileName { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }

    /// <summary>
    /// Dữ liệu toàn diện được chuẩn bị để vẽ Tờ khai Quyết toán thuế bằng QuestPDF.
    /// </summary>
    public class TaxSettlementPdfModel
    {
        public Guid DossierId { get; set; }
        public int TaxYear { get; set; }
        public DateOnly CutoffDate { get; set; }
        public string LawGroupLabel { get; set; } = string.Empty;

        // Thông tin người nộp thuế
        public string FullName { get; set; } = string.Empty;
        public string TaxCode { get; set; } = string.Empty;
        public string CitizenId { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string TaxOfficeName { get; set; } = string.Empty;
        public string BankAccountNumber { get; set; } = string.Empty;
        public string BankName { get; set; } = string.Empty;

        // Số liệu quyết toán
        public decimal TotalGrossIncome { get; set; }
        public decimal TotalTaxWithheld { get; set; }
        public decimal TotalInsuranceDeduction { get; set; }
        public int PersonalDeductionMonths { get; set; }
        public decimal PersonalDeductionAmount { get; set; }
        public int DependentDeductionPersonMonths { get; set; }
        public decimal DependentDeductionAmount { get; set; }
        public decimal CharityDeduction { get; set; }
        public decimal MedicalDeduction { get; set; }
        public decimal EducationDeduction { get; set; }
        public decimal TotalDeductions { get; set; }
        public decimal TaxableIncomeYearly { get; set; }
        public decimal TaxableIncomeMonthly { get; set; }
        public decimal TaxPayable { get; set; }
        public decimal RefundAmount { get; set; }
        public decimal DueAmount { get; set; }
        public int AppliedBracketNo { get; set; }

        // Danh sách nguồn thu nhập
        public List<SettlementIncomeItemDto> IncomeItems { get; set; } = new();

        // Biểu bậc thuế phân bổ
        public List<ProgressiveBracketDetailDto> BracketDetails { get; set; } = new();

        // Phụ lục 1: Danh sách Người phụ thuộc (02-1/BK-QTT)
        public List<PdfDependentItemDto> Dependents { get; set; } = new();

        // Phụ lục 2: Danh sách Chứng từ Y tế & Giáo dục được giảm trừ
        public List<PdfReceiptItemDto> Receipts { get; set; } = new();
    }

    public class PdfDependentItemDto
    {
        public int Index { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string DateOfBirth { get; set; } = string.Empty;
        public string IdNumber { get; set; } = string.Empty; // CCCD hoặc Mã số thuế hoặc Số khai sinh
        public string Relationship { get; set; } = string.Empty;
        public int EligibleMonths { get; set; }
        public decimal DeductionAmount { get; set; }
    }

    public class PdfReceiptItemDto
    {
        public int Index { get; set; }
        public string Category { get; set; } = string.Empty; // "Y Tế" hoặc "Giáo Dục"
        public string IssuerName { get; set; } = string.Empty; // Bệnh viện / Trường học
        public string DocumentNumber { get; set; } = string.Empty;
        public string IssueDate { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal TaxEligibleAmount { get; set; }
    }

    public class ProgressiveBracketDetailDto
    {
        public int BracketNo { get; set; }
        public decimal FromMonthly { get; set; }
        public decimal? ToMonthly { get; set; }
        public decimal Rate { get; set; }
        public decimal QuickDeduct { get; set; }
        public decimal TaxableMonthly { get; set; }
        public decimal TaxMonthly { get; set; }
        public bool IsApplied { get; set; }
    }
}
