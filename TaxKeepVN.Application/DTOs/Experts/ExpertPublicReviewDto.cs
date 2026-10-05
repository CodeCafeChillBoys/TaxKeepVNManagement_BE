using System;

namespace TaxKeepVN.Application.DTOs.Experts
{
    /// <summary>
    /// Feedback, đánh giá từ khách hàng cũ (ẩn danh nếu khách yêu cầu)
    /// </summary>
    public class ExpertPublicReviewDto
    {
        public Guid Id { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }

        /// <summary>Tên hiển thị người đánh giá (ẩn danh hoặc che bớt họ tên nếu is_anonymous = true)</summary>
        public string ReviewerDisplayName { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; }
    }
}
