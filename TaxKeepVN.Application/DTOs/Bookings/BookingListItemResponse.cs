using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Application.DTOs.Bookings
{
    /// <summary>
    /// Thông tin tóm tắt một ca tư vấn hiển thị trong danh sách (My Bookings hoặc Expert Pending Bookings)
    /// </summary>
    public class BookingListItemResponse
    {
        public Guid Id { get; set; }
        public string BookingCode { get; set; } = string.Empty;

        public Guid UserId { get; set; }
        public string UserFullName { get; set; } = string.Empty;

        public Guid ExpertProfileId { get; set; }
        public string ExpertFullName { get; set; } = string.Empty;
        public string? ExpertAvatarUrl { get; set; }

        public DateOnly SlotDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public SessionType SessionType { get; set; }
        public int DurationMinutes { get; set; }
        public decimal Fee { get; set; }

        public string SpecializationName { get; set; } = string.Empty;
        public string TopicTitle { get; set; } = string.Empty;

        public BookingStatus Status { get; set; }
        public DateTimeOffset HoldExpiresAt { get; set; }
        public DateTimeOffset? ApprovalDeadline { get; set; }
        public int AttachmentCount { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
    }
}
