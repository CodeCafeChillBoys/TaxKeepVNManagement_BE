using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Domain.Entities
{
    /// <summary>
    /// Phiên đặt lịch tư vấn giữa người nộp thuế và chuyên gia (Đặc tả 3)
    /// </summary>
    public class Booking
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>Mã định danh đặt lịch thân thiện với người dùng (VD: BK-20261008-AB12CD)</summary>
        public string BookingCode { get; set; } = string.Empty;

        /// <summary>FK trỏ tới User (Khách hàng đặt lịch)</summary>
        public Guid UserId { get; set; }

        /// <summary>FK trỏ tới ExpertSlot (Khung giờ làm việc)</summary>
        public Guid ExpertSlotId { get; set; }

        /// <summary>FK trỏ tới Specialization (Chủ đề cần tư vấn)</summary>
        public int SpecializationId { get; set; }

        /// <summary>Hình thức tư vấn (ONLINE_MEETING, CHAT, VOICE_CALL)</summary>
        public SessionType SessionType { get; set; } = SessionType.ONLINE_MEETING;

        /// <summary>Thời lượng tư vấn (tính theo phút: 45, 60...)</summary>
        public int DurationMinutes { get; set; }

        /// <summary>Tổng chi phí phiên tư vấn (VNĐ)</summary>
        public decimal Fee { get; set; }

        /// <summary>Chủ đề tóm tắt buổi tư vấn</summary>
        public string TopicTitle { get; set; } = string.Empty;

        /// <summary>Mô tả chi tiết câu hỏi / vấn đề trọng tâm cần tư vấn</summary>
        public string ProblemDescription { get; set; } = string.Empty;

        /// <summary>Trạng thái hiện tại của buổi hẹn</summary>
        public BookingStatus Status { get; set; } = BookingStatus.PENDING_PAYMENT;

        /// <summary>Hạn chót tạm giữ slot 10 phút chờ thanh toán (UtcNow + 10 phút)</summary>
        public DateTimeOffset HoldExpiresAt { get; set; }

        /// <summary>Hạn chót 2 giờ để Chuyên gia xét duyệt tiếp nhận (UtcNow + 2 giờ tính từ khi thanh toán thành công)</summary>
        public DateTimeOffset? ApprovalDeadline { get; set; }

        /// <summary>Thời điểm chuyên gia chấp nhận tiếp nhận</summary>
        public DateTimeOffset? ApprovedAt { get; set; }

        /// <summary>Thời điểm chuyên gia từ chối hoặc hết hạn</summary>
        public DateTimeOffset? RejectedAt { get; set; }

        /// <summary>Lý do chuyên gia từ chối tiếp nhận ca tư vấn</summary>
        public string? RejectionReason { get; set; }

        /// <summary>Thời điểm khách hàng thanh toán thành công</summary>
        public DateTimeOffset? PaidAt { get; set; }

        /// <summary>Mã giao dịch từ cổng thanh toán</summary>
        public string? PaymentReference { get; set; }

        /// <summary>Trạng thái hoàn tiền: NONE, PENDING_REFUND, REFUNDED</summary>
        public string RefundStatus { get; set; } = "NONE";

        /// <summary>Thời điểm hoàn tiền thành công</summary>
        public DateTimeOffset? RefundedAt { get; set; }

        /// <summary>Thời điểm hủy lịch (nếu có)</summary>
        public DateTimeOffset? CancelledAt { get; set; }

        /// <summary>Lý do hủy lịch</summary>
        public string? CancellationReason { get; set; }

        /// <summary>Bên thực hiện hủy: USER, EXPERT, SYSTEM</summary>
        public string? CancelledBy { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        // Navigation properties
        public User? User { get; set; }
        public ExpertSlot? ExpertSlot { get; set; }
        public Specialization? Specialization { get; set; }
        public ICollection<BookingAttachment> Attachments { get; set; } = new List<BookingAttachment>();
    }
}
