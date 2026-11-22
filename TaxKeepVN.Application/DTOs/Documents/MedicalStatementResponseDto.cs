using System;
using System.Collections.Generic;

namespace TaxKeepVN.Application.DTOs.Documents
{
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class MedicalStatementResponseDto
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }
        public string FileUrl { get; set; } = string.Empty;
        public string? OriginalFilename { get; set; }
        public string? FormCode { get; set; }
        public string? StatementNumber { get; set; }
        public string? PatientName { get; set; }
        public DateOnly? PatientDob { get; set; }
        public string? PatientGender { get; set; }
        public string? PatientIdCard { get; set; }
        public string? InsuranceNumber { get; set; }
        public decimal? BenefitRate { get; set; }
        public string? HospitalName { get; set; }
        public string? HospitalCode { get; set; }
        public string? Department { get; set; }
        public DateOnly? AdmissionDate { get; set; }
        public DateOnly? DischargeDate { get; set; }
        public string? TreatmentType { get; set; }
        public decimal? TotalHospitalAmount { get; set; }
        public decimal? InsurancePaidAmount { get; set; }
        public decimal? PatientPaidAmount { get; set; }
        public decimal? PatientSelfPayAmount { get; set; }
        public decimal? TotalEligibleAmount { get; set; }
        public decimal? OverallConfidence { get; set; }
        public decimal? AppliedThreshold { get; set; }
        public bool? IsPassedThreshold { get; set; }
        public string Status { get; set; } = "UPLOADED";
        public List<string> ValidationErrors { get; set; } = new();
        public List<MedicalStatementLineResponseDto> Lines { get; set; } = new();
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class MedicalStatementLineResponseDto
    {
        public long Id { get; set; }
        public int LineOrder { get; set; }
        public string? SectionCode { get; set; }
        public string? SectionName { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string? Unit { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal InsurancePaid { get; set; }
        public decimal PatientCoPay { get; set; }
        public decimal PatientSelfPay { get; set; }
        public string DeductionStatus { get; set; } = "ELIGIBLE";
        public bool IsEligibleDeduction { get; set; }
        public string? DeductionReason { get; set; }
        public decimal EligibleAmount { get; set; }
    }
}
