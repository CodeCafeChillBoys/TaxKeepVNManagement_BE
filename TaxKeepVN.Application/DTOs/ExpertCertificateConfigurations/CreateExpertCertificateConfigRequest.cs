using System.ComponentModel.DataAnnotations;

namespace TaxKeepVN.Application.DTOs.ExpertCertificateConfigurations
{
    public class CreateExpertCertificateConfigRequest
    {
        [Required(ErrorMessage = "Mã chứng chỉ không được để trống")]
        [MaxLength(50, ErrorMessage = "Mã chứng chỉ không vượt quá 50 ký tự")]
        [RegularExpression(@"^[A-Z0-9_]+$", ErrorMessage = "Mã chỉ bao gồm chữ in hoa, số và dấu gạch dưới (VD: TAX_AGENT)")]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên loại chứng chỉ không được để trống")]
        [MaxLength(255, ErrorMessage = "Tên không vượt quá 255 ký tự")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(255)]
        public string? DefaultIssuingAuthority { get; set; }

        public bool RequiresExpiryDate { get; set; } = false;

        [MaxLength(500)]
        public string? AiKeywords { get; set; }

        public int DisplayOrder { get; set; } = 0;

        public bool IsActive { get; set; } = true;
    }
}