using System;

namespace TaxKeepVN.Domain.Entities
{
    public class SystemConfig
    {
        public Guid ConfigId { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Khóa định danh cấu hình.
        /// Ví dụ: PIT_BRACKETS_JSON, PIT_DEDUCTION_PERSONAL_MONTHLY, PIT_INSURANCE_BHXH_RATE
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

        /// <summary>
        /// Năm bắt đầu hiệu lực của cấu hình này.
        /// NULL  = config chung, áp dụng cho mọi năm (fallback khi không tìm thấy config riêng).
        /// 2026  = luật mới, hiệu lực từ năm 2026 trở đi (áp dụng cho 2026, 2027, 2028...).
        /// Logic tra cứu: chọn row có AppliesFromYear lớn nhất mà vẫn &lt;= taxYear đang tính.
        ///                Nếu không tìm được → fallback về row có AppliesFromYear = NULL.
        /// </summary>
        public int? AppliesFromYear { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
