using System.ComponentModel.DataAnnotations;

namespace TaxKeepVN.Application.DTOs.ExpertCertificateConfigurations
{
    public class UpdateExpertCertificateConfigRequest
    {
        [Required(ErrorMessage = "Tên loại chứng chỉ không được để trống")]
        [MaxLength(255, ErrorMessage = "Tên không vượt quá 255 ký tự")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(255)]
        public string? DefaultIssuingAuthority { get; set; }

        public bool RequiresExpiryDate { get; set; }

        [MaxLength(500)]
        public string? AiKeywords { get; set; }

        public int DisplayOrder { get; set; }

        public bool IsActive { get; set; }
    }
}
