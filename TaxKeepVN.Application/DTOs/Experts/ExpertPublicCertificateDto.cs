using System;

namespace TaxKeepVN.Application.DTOs.Experts
{
    /// <summary>
    /// Chứng chỉ hiển thị cho người dùng (che mờ số chứng chỉ để bảo mật thông tin cá nhân - BR-03)
    /// </summary>
    public class ExpertPublicCertificateDto
    {
        public Guid Id { get; set; }
        public string CertificateType { get; set; } = string.Empty;
        public string CertificateName { get; set; } = string.Empty;

        /// <summary>Số hiệu chứng chỉ đã được che mờ (VD: CPA-****-5678)</summary>
        public string MaskedCertificateNumber { get; set; } = string.Empty;

        public string IssuingAuthority { get; set; } = string.Empty;
        public DateOnly IssueDate { get; set; }
        public bool HasExpiry { get; set; }
        public DateOnly? ExpiryDate { get; set; }
        public string FileUrl { get; set; } = string.Empty;
    }
}
