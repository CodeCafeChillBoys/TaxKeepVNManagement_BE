using System;

namespace TaxKeepVN.Application.DTOs.SystemConfigs
{
    public class SystemConfigResponseDto
    {
        public Guid ConfigId { get; set; }
        public string ConfigKey { get; set; } = string.Empty;
        public string ConfigValue { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
