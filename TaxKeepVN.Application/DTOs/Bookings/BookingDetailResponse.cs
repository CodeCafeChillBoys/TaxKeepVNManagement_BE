using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Application.DTOs.Bookings
{
    /// <summary>
    /// Thông tin chi tiết một ca tư vấn (Đặc tả 3)
    /// </summary>
    public class BookingDetailResponse
    {
        public Guid Id { get; set; }
        public string BookingCode { get; set; } = string.Empty;

        // Thông tin khách hàng
        public Guid UserId { get; set; }
        public string UserFullName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string? UserPhoneNumber { get; set; }

        // Thông tin chuyên gia
        public Guid ExpertProfileId { get; set; }
        public string ExpertFullName { get; set; } = string.Empty;
        public string? ExpertAvatarUrl { get; set; }
        public string ExpertJobTitle { get; set; } = string.Empty;

        // Khung giờ & ca tư vấn
        public Guid ExpertSlotId { get; set; }
        public DateOnly SlotDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public SessionType SessionType { get; set; }
        public int DurationMinutes { get; set; }
        public decimal Fee { get; set; }

        // Chủ đề & nội dung
        public int SpecializationId { get; set; }
        public string SpecializationName { get; set; } = string.Empty;
        public string TopicTitle { get; set; } = string.Empty;
        public string ProblemDescription { get; set; } = string.Empty;

        // Trạng thái & Countdown
        public BookingStatus Status { get; set; }
        public DateTimeOffset HoldExpiresAt { get; set; }
        public int HoldCountdownSeconds { get; set; }

        public DateTimeOffset? ApprovalDeadline { get; set; }
        public int? ApprovalCountdownSeconds { get; set; }

        public DateTimeOffset? ApprovedAt { get; set; }
        public DateTimeOffset? RejectedAt { get; set; }
        public string? RejectionReason { get; set; }

        // Thanh toán & Hoàn tiền
        public DateTimeOffset? PaidAt { get; set; }
        public string? PaymentReference { get; set; }
        public string RefundStatus { get; set; } = "NONE";
        public DateTimeOffset? RefundedAt { get; set; }

        // Hủy lịch
        public DateTimeOffset? CancelledAt { get; set; }
        public string? CancellationReason { get; set; }
        public string? CancelledBy { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        // Danh sách tệp đính kèm
        public List<BookingAttachmentDto> Attachments { get; set; } = new();
    }
}
