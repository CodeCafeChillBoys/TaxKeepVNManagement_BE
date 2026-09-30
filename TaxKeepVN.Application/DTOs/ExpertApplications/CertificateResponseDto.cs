using System;
using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Application.DTOs.ExpertApplications
{
    /// <summary>
    /// Thông tin chứng chỉ trả về trong hồ sơ chuyên gia
    /// </summary>
    public class CertificateResponseDto
    {
        public Guid Id { get; set; }
        public string CertificateType { get; set; } = string.Empty;
        public string CertificateName { get; set; } = string.Empty;
        public string CertificateNumber { get; set; } = string.Empty;
        public string IssuingAuthority { get; set; } = string.Empty;
        public DateOnly IssueDate { get; set; }
        public bool HasExpiry { get; set; }
        public DateOnly? ExpiryDate { get; set; }
        public string FileUrl { get; set; } = string.Empty;
        public string? FileName { get; set; }
        public CertificateVerificationStatus VerificationStatus { get; set; }
        public string? VerificationNote { get; set; }
    }
}
