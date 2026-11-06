using System;

namespace TaxKeepVN.Domain.Entities
{
    /// <summary>
    /// Theo dõi lượng dùng quota trong chu kỳ hiện tại (reset đầu mỗi tháng bởi Background Job).
    /// </summary>
    public class SubscriptionQuotaUsage
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>FK → users.user_id</summary>
        public Guid UserId { get; set; }

        /// <summary>Tháng theo dõi, dạng "2026-11" (YYYY-MM)</summary>
        public string PeriodMonth { get; set; } = string.Empty;

        /// <summary>Số câu hỏi AI đã dùng trong tháng</summary>
        public int AiQuestionsUsed { get; set; } = 0;

        /// <summary>Số lượt OCR đã dùng trong tháng</summary>
        public int OcrScansUsed { get; set; } = 0;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        // Navigation
        public User? User { get; set; }
    }
}

