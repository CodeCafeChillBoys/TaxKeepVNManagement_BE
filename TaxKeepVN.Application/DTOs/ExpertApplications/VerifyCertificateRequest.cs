using System.ComponentModel.DataAnnotations;
using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Application.DTOs.ExpertApplications
{
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class VerifyCertificateRequest
    {
        [Required(ErrorMessage = "Trạng thái xác minh chứng chỉ không được để trống.")]
        public CertificateVerificationStatus VerificationStatus { get; set; }

        public string? VerificationSource { get; set; }

        public string? VerificationNote { get; set; }
    }
}
