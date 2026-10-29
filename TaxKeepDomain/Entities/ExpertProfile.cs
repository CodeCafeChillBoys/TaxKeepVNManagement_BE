using System;
using System.Collections.Generic;

namespace TaxKeepVN.Domain.Entities
{
    /// <summary>
    /// Hồ sơ chuyên gia chính thức sau khi được Admin phê duyệt (Approved - Mục 15)
    /// </summary>
    public class ExpertProfile
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>FK → User.UserId (1-1)</summary>
        public Guid UserId { get; set; }

        /// <summary>FK → ExpertApplication.Id (Hồ sơ đăng ký được duyệt gần nhất)</summary>
        public Guid LatestApplicationId { get; set; }

        public string JobTitle { get; set; } = string.Empty;

        /// <summary>Đơn vị / công ty đang công tác (Nullable nếu tự do)</summary>
        public string? CompanyName { get; set; }

        public string? Bio { get; set; }

        public int YearsOfExperience { get; set; } = 0;

        /// <summary>Điểm đánh giá trung bình (VD: 4.85)</summary>
        public decimal Rating { get; set; } = 0.00m;

        /// <summary>Tổng số lượt đánh giá</summary>
        public int TotalReviews { get; set; } = 0;

        /// <summary>Số ca tư vấn đã hoàn thành</summary>
        public int CompletedConsultationsCount { get; set; } = 0;

        /// <summary>Trạng thái sẵn sàng nhận lịch tư vấn</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>Thời điểm được phê duyệt chính thức</summary>
        public DateTimeOffset ApprovedAt { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        // Navigation properties
        public User? User { get; set; }
        public ExpertApplication? LatestApplication { get; set; }
        public ICollection<ExpertSlot> Slots { get; set; } = new List<ExpertSlot>();
        public ICollection<ExpertReview> Reviews { get; set; } = new List<ExpertReview>();
    }
}
