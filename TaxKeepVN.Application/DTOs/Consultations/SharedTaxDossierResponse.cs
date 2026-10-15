using System;
using System.Collections.Generic;

namespace TaxKeepVN.Application.DTOs.Consultations
{
    /// <summary>
    /// Bộ hồ sơ thuế được chia sẻ quyền xem (Read-only) cho Chuyên gia trong phòng tư vấn trực tuyến (Đặc tả 3 & 5)
    /// </summary>
    public class SharedTaxDossierResponse
    {
        public Guid BookingId { get; set; }
        public string BookingCode { get; set; } = string.Empty;
        public int TaxYear { get; set; }

        // Thông tin người nộp thuế
        public string TaxpayerFullName { get; set; } = string.Empty;
        public string TaxpayerEmail { get; set; } = string.Empty;
        public string? TaxpayerPhone { get; set; }
        public string? TaxpayerCitizenId { get; set; }
        public string? TaxpayerTaxId { get; set; }

        // Nguồn thu nhập & Bảng lương
        public List<SharedIncomeSourceDto> IncomeSources { get; set; } = new();
        public List<SharedMonthlyIncomeDto> MonthlyIncomes { get; set; } = new();
        public decimal TotalAnnualIncome { get; set; }
        public decimal TotalTaxWithheld { get; set; }

        // Người phụ thuộc (Thông tư 87/2026/TT-BTC)
        public List<SharedDependentDto> Dependents { get; set; } = new();
        public int TotalDependentsCount { get; set; }

        // Chứng từ giảm trừ Y tế & Giáo dục (Luật 109/2025/QH15)
        public List<SharedDocumentDto> DeductibleDocuments { get; set; } = new();
        public decimal TotalMedicalEducationExpenses { get; set; }

        // Dự thảo quyết toán thuế (nếu đã tạo)
        public SharedDraftSettlementDto? DraftSettlement { get; set; }

        // Tài liệu bổ trợ đính kèm riêng cho Booking
        public List<SharedAttachmentDto> BookingAttachments { get; set; } = new();
    }

    public class SharedIncomeSourceDto
    {
        public Guid Id { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string CompanyTaxCode { get; set; } = string.Empty;
        public int TaxYear { get; set; }
        public decimal TotalIncome { get; set; }
        public decimal TaxWithheld { get; set; }
        public decimal InsuranceDeducted { get; set; }
    }

    public class SharedMonthlyIncomeDto
    {
        public Guid Id { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public string OrganizationName { get; set; } = string.Empty;
        public string? TaxIdNumber { get; set; }
        public decimal TotalTaxableIncome { get; set; }
        public decimal InsuranceDeducted { get; set; }
        public decimal TaxAlreadyDeducted { get; set; }
        public string? PayslipFileUrl { get; set; }
    }

    public class SharedDependentDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Relationship { get; set; } = string.Empty;
        public string CurrentGroup { get; set; } = string.Empty;
        public string? CitizenId { get; set; }
        public string? BirthCertNumber { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsProfileComplete { get; set; }
    }

    public class SharedDocumentDto
    {
        public Guid Id { get; set; }
        public string? DocTypeCode { get; set; }
        public string? InvoiceSeries { get; set; }
        public string? InvoiceNumber { get; set; }
        public DateOnly? InvoiceDate { get; set; }
        public string? SellerName { get; set; }
        public string? SellerTaxCode { get; set; }
        public string FileUrl { get; set; } = string.Empty;
    }

    public class SharedDraftSettlementDto
    {
        public Guid DossierId { get; set; }
        public int TaxYear { get; set; }
        public decimal TotalGrossIncome { get; set; }
        public decimal TotalTaxWithheld { get; set; }
        public decimal TotalInsuranceDeduction { get; set; }
        public decimal PersonalDeductionAmount { get; set; }
    }

    public class SharedAttachmentDto
    {
        public Guid Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FileUrl { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
    }
}
