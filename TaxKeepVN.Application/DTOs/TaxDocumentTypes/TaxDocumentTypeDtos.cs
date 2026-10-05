using System.ComponentModel.DataAnnotations;

namespace TaxKeepVN.Application.DTOs.TaxDocumentTypes
{
    public class TaxDocumentTypeDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsTaxEligible { get; set; } = true;
    }

    public class CreateTaxDocumentTypeDto
    {
        [Required(ErrorMessage = "Mã loại chứng từ không được để trống.")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Mã loại chứng từ phải từ 2 đến 50 ký tự.")]
        [RegularExpression(@"^[A-Z0-9_]+$", ErrorMessage = "Mã loại chứng từ chỉ được chứa chữ in hoa, số và dấu gạch dưới (VD: MEDICAL_EXPENSE_INVOICE).")]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên loại chứng từ không được để trống.")]
        [StringLength(255, MinimumLength = 2, ErrorMessage = "Tên loại chứng từ phải từ 2 đến 255 ký tự.")]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public bool IsTaxEligible { get; set; } = true;
    }

    public class UpdateTaxDocumentTypeDto
    {
        [Required(ErrorMessage = "Tên loại chứng từ không được để trống.")]
        [StringLength(255, MinimumLength = 2, ErrorMessage = "Tên loại chứng từ phải từ 2 đến 255 ký tự.")]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public bool IsTaxEligible { get; set; } = true;
    }

    public class TaxDocumentTypeQueryParameters
    {
        public string? Search { get; set; }
        public bool? IsTaxEligible { get; set; }
    }
}
