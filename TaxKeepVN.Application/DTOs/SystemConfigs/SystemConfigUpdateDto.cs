using System.ComponentModel.DataAnnotations;

namespace TaxKeepVN.Application.DTOs.SystemConfigs
{
    public class SystemConfigUpdateDto
    {
        [Required(ErrorMessage = "Giá trị cấu hình không được để trống.")]
        public string ConfigValue { get; set; } = string.Empty;

        public string? Description { get; set; }

        public bool? IsActive { get; set; }
    }
}
