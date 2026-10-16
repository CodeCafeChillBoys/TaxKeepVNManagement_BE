using System;

namespace TaxKeepVN.Domain.Entities
{
    public class TaxDeductionPolicy
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>Năm áp dụng (Duy nhất, VD: 2025, 2026)</summary>
        public int AppliesFromYear { get; set; }

        /// <summary>Tên căn cứ pháp lý (VD: Luật Thuế TNCN cũ, Luật 109/2025/QH15)</summary>
        public string LawName { get; set; } = string.Empty;

        /// <summary>Mức giảm trừ bản thân (VNĐ/tháng)</summary>
        public decimal PersonalMonthly { get; set; }

        /// <summary>Mức giảm trừ người phụ thuộc (VNĐ/người/tháng)</summary>
        public decimal DependentMonthly { get; set; }

        /// <summary>Tỷ lệ đóng BHXH của người lao động (VD: 0.08)</summary>
        public decimal BhxhRate { get; set; } = 0.08m;

        /// <summary>Tỷ lệ đóng BHYT của người lao động (VD: 0.015)</summary>
        public decimal BhytRate { get; set; } = 0.015m;

        /// <summary>Tỷ lệ đóng BHTN của người lao động (VD: 0.01)</summary>
        public decimal BhtnRate { get; set; } = 0.01m;

        /// <summary>Ngưỡng thuế nộp thiếu được miễn phạt (VNĐ, mặc định 50.000)</summary>
        public decimal SmallAmountExemption { get; set; } = 50000m;

        /// <summary>Mức trần chi phí y tế được giảm trừ tối đa trong năm (VNĐ/năm)</summary>
        public decimal MedicalMaxYearly { get; set; } = 0m;

        /// <summary>Mức trần chi phí giáo dục (học phí) được giảm trừ tối đa trong năm (VNĐ/năm)</summary>
        public decimal EducationMaxYearly { get; set; } = 0m;

        public bool IsActive { get; set; } = true;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
