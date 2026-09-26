using System.ComponentModel.DataAnnotations;

namespace TaxKeepVN.Application.DTOs.SystemConfigs
{
    public class SystemConfigCreateDto
    {
        [Required(ErrorMessage = "ConfigKey không được để trống")]
        [MaxLength(100, ErrorMessage = "ConfigKey không được vượt quá 100 ký tự")]
        public string ConfigKey { get; set; } = null!;

        [Required(ErrorMessage = "ConfigValue không được để trống")]
        public string ConfigValue { get; set; } = null!;

        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
