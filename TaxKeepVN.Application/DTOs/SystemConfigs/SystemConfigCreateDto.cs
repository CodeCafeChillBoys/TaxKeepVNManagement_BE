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

        /// <summary>
        /// Năm bắt đầu hiệu lực. Để trống (null) = config chung, áp dụng cho mọi năm.
        /// Ví dụ: 2026 = luật mới hiệu lực từ năm 2026 trở đi.
        /// </summary>
        public int? AppliesFromYear { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
