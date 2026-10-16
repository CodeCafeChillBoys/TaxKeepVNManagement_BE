using System;

namespace TaxKeepVN.Domain.Entities
{
    public class TaxBracket
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>Thứ tự bậc thuế (1, 2, 3, 4, 5...)</summary>
        public int BracketNo { get; set; }

        /// <summary>Ngưỡng thu nhập tính thuế tối thiểu của bậc (VNĐ/tháng)</summary>
        public decimal FromMonthly { get; set; }

        /// <summary>Ngưỡng thu nhập tính thuế tối đa của bậc (VNĐ/tháng, NULL = vô cùng)</summary>
        public decimal? ToMonthly { get; set; }

        /// <summary>Thuế suất tương ứng (VD: 0.05, 0.10, 0.20, 0.30, 0.35)</summary>
        public decimal Rate { get; set; }

        /// <summary>Số khấu trừ nhanh (VNĐ/tháng) theo công thức tính nhanh</summary>
        public decimal QuickDeduct { get; set; }

        /// <summary>Năm bắt đầu áp dụng biểu thuế này (VD: 2025 cho 7 bậc cũ, 2026 cho 5 bậc mới theo Luật 109/2025/QH15)</summary>
        public int AppliesFromYear { get; set; }

        /// <summary>Mô tả chi tiết bậc thuế</summary>
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
