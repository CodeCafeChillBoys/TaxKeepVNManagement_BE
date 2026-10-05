using System.ComponentModel.DataAnnotations;

namespace TaxKeepVN.Application.DTOs.Specializations
{
    public class SpecializationDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
    }

    public class CreateSpecializationDto
    {
        [Required(ErrorMessage = "Mã lĩnh vực chuyên môn không được để trống.")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Mã lĩnh vực phải từ 2 đến 50 ký tự.")]
        [RegularExpression(@"^[A-Z0-9_]+$", ErrorMessage = "Mã lĩnh vực chỉ được chứa chữ in hoa, số và dấu gạch dưới (VD: TAX_AGENT, PIT).")]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên lĩnh vực chuyên môn không được để trống.")]
        [StringLength(150, MinimumLength = 2, ErrorMessage = "Tên lĩnh vực phải từ 2 đến 150 ký tự.")]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public class UpdateSpecializationDto
    {
        [Required(ErrorMessage = "Tên lĩnh vực chuyên môn không được để trống.")]
        [StringLength(150, MinimumLength = 2, ErrorMessage = "Tên lĩnh vực phải từ 2 đến 150 ký tự.")]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public class SpecializationQueryParameters
    {
        public string? Search { get; set; }
        public bool? IsActive { get; set; }
    }
}
