using System;

namespace TaxKeepVN.Domain.Entities
{
    /// <summary>
    /// Đánh giá, số sao và nhận xét từ khách hàng sau các phiên tư vấn với chuyên gia
    /// </summary>
    public class ExpertReview
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>FK → ExpertProfile.Id</summary>
        public Guid ExpertProfileId { get; set; }

        /// <summary>FK → User.UserId (Khách hàng / Người nộp thuế để lại đánh giá)</summary>
        public Guid UserId { get; set; }

        /// <summary>FK tùy chọn tới phiên đặt lịch (BookingId) khi hoàn thành ca tư vấn</summary>
        public Guid? BookingId { get; set; }

        /// <summary>Số sao đánh giá tổng quan (1 đến 5 sao)</summary>
        public int Rating { get; set; } = 5;

        /// <summary>Nội dung nhận xét, phản hồi chi tiết (tối thiểu 10 ký tự)</summary>
        public string? Comment { get; set; }

        /// <summary>
        /// Điểm đánh giá chi tiết theo từng tiêu chí, lưu dạng JSON linh hoạt.
        /// Ví dụ: { "expertise": 5, "punctuality": 4, "communication": 5, "problemSolving": 5 }
        /// Cho phép Admin thêm/bỏ tiêu chí mà không cần thay đổi database schema.
        /// </summary>
        public string? DetailScoresJson { get; set; }

        /// <summary>Có ẩn danh người đánh giá hay không</summary>
        public bool IsAnonymous { get; set; } = false;

        /// <summary>Trạng thái hiển thị công khai (Admin có thể ẩn nếu vi phạm chuẩn mực)</summary>
        public bool IsPublished { get; set; } = true;

        /// <summary>
        /// Đánh cờ đánh giá tiêu cực cần kiểm duyệt.
        /// Ngưỡng flag được đọc từ cấu hình hệ thống (ReviewFlagThresholdRating), không hardcode.
        /// True → hệ thống gửi thông báo cho Admin/CSKH.
        /// </summary>
        public bool IsFlaggedForReview { get; set; } = false;

        /// <summary>Nội dung phản hồi công khai của chuyên gia (chỉ được gửi 1 lần)</summary>
        public string? ExpertReply { get; set; }

        /// <summary>Thời điểm chuyên gia gửi phản hồi</summary>
        public DateTimeOffset? ExpertRepliedAt { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        // Navigation properties
        public ExpertProfile? ExpertProfile { get; set; }
        public User? User { get; set; }
    }
}
