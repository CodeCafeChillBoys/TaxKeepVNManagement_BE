using System;
using System.Collections.Generic;

namespace TaxKeepVN.Application.DTOs.MedicalClaims
{
    public class MedicalClaimDto
    {
        public Guid ReceiptDocumentId { get; set; }
        public string ReceiptStatus { get; set; } = string.Empty;
        public string? InvoiceSeries { get; set; }
        public string? InvoiceNumber { get; set; }
        public DateOnly? InvoiceDate { get; set; }
        public string? SellerName { get; set; }
        public decimal? TotalAmount { get; set; }
        public bool? IsNotReimbursed { get; set; }

        // Bảng kê chi phí KCB kèm theo
        public Guid? StatementDocumentId { get; set; }
        public bool HasStatement => StatementDocumentId.HasValue;
        public string? StatementOriginalFilename { get; set; }
        public string? StatementFileUrl { get; set; }

        // Thông tin người bệnh & cơ sở KCB
        public string? PatientName { get; set; }
        public Guid? PatientDependentId { get; set; }
        public string? PatientDependentName { get; set; }
        public string? FacilityName { get; set; }
        public bool? HasInsuranceCard { get; set; }
        public decimal? InsuranceBenefitRate { get; set; }
        public DateOnly? ServiceDate { get; set; }

        // Kết quả thẩm định giảm trừ y tế
        public decimal? EligibleAmount { get; set; }
        public string MedicalClaimStatus { get; set; } = string.Empty;
        public string? RejectReason { get; set; }
        public List<string> Warnings { get; set; } = new();
        public List<string> CrossCheckIssues { get; set; } = new();

        public MedicalStatementSummaryDto? Summary { get; set; }
        public List<MedicalClaimItemDto> Items { get; set; } = new();
    }

    public class MedicalClaimItemDto
    {
        public long Id { get; set; }
        public int ItemOrder { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string? Unit { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }

        public int? SectionIndex { get; set; }
        public bool? HasInsuranceCard { get; set; }
        public decimal? BenefitRate { get; set; }
        public string? GroupCode { get; set; }
        public string? GroupName { get; set; }
        public bool IsPackageSubLine { get; set; }

        public decimal? UnitPriceInsurance { get; set; }
        public decimal? ServiceRate { get; set; }
        public decimal? HospitalAmount { get; set; }
        public decimal? InsuranceRate { get; set; }
        public decimal? InsuranceAmount { get; set; }
        public decimal? InsuranceFundPaid { get; set; }
        public decimal? PatientCoPay { get; set; }
        public decimal? OtherSourcePaid { get; set; }
        public decimal? PatientSelfPay { get; set; }

        public string? DeductionStatus { get; set; }
        public string? DeductionReason { get; set; }
        public decimal? EligibleAmount { get; set; }
        public decimal? SelfPayAmount { get; set; }
        public string? SelfPayReason { get; set; }
        public bool SelfPayOptIn { get; set; }
        public bool IsUserOverridden { get; set; }
    }

    public class MedicalStatementSummaryDto
    {
        public decimal? TotalHospitalAmount { get; set; }
        public decimal? InsuranceFundPaid { get; set; }
        public decimal? PatientPaid { get; set; }
        public decimal? CoPayWithinInsurance { get; set; }
        public decimal? OtherPatientPayments { get; set; }
        public decimal? OtherSourcePaid { get; set; }
    }

    public class LinkMedicalStatementRequestDto
    {
        public Guid StatementDocumentId { get; set; }
    }

    public class UpdateItemDeductionRequestDto
    {
        public string? DeductionStatus { get; set; }
        public bool? SelfPayOptIn { get; set; }
        public string? Note { get; set; }
    }

    public class CandidateReceiptDto
    {
        public Guid DocumentId { get; set; }
        public string? InvoiceSeries { get; set; }
        public string? InvoiceNumber { get; set; }
        public DateOnly? InvoiceDate { get; set; }
        public string? SellerName { get; set; }
        public decimal? TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? PatientName { get; set; }
    }
}
