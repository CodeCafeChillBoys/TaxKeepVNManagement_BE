using System;

namespace TaxKeepVN.Domain.Entities
{
    public class SystemConfig
    {
        public Guid ConfigId { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Khóa định danh cấu hình (Unique).
        /// Ví dụ: TAX_SETTLEMENT_REMINDER_DAYS, TAX_SETTLEMENT_DEADLINE_MONTH, TAX_SETTLEMENT_DEADLINE_DAY
        /// </summary>
        public string ConfigKey { get; set; } = string.Empty;

        /// <summary>
        /// Giá trị cấu hình được Admin thiết lập động trong DB.
        /// </summary>
        public string ConfigValue { get; set; } = string.Empty;

        /// <summary>
        /// Mô tả ý nghĩa của tham số cấu hình.
        /// </summary>
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
