using System;

namespace TaxKeepVN.Domain.Entities
{
    /// <summary>
    /// Bản ghi tin nhắn trao đổi trong phòng tư vấn trực tuyến giữa Người nộp thuế và Chuyên gia (Đặc tả 5 & Flow 4)
    /// Hỗ trợ lưu trữ tin nhắn văn bản, chia sẻ đường dẫn chứng từ/tài liệu thuế và trạng thái đã đọc.
    /// </summary>
    public class ConsultationMessage
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Khóa ngoại liên kết tới phiên đặt lịch Booking
        /// </summary>
        public Guid BookingId { get; set; }

        /// <summary>
        /// ID của người gửi (User ID)
        /// </summary>
        public Guid SenderId { get; set; }

        /// <summary>
        /// Vai trò người gửi: "taxpayer" hoặc "expert"
        /// </summary>
        public string SenderRole { get; set; } = string.Empty;

        /// <summary>
        /// Tên hiển thị người gửi chụp tại thời điểm nhắn
        /// </summary>
        public string SenderName { get; set; } = string.Empty;

        /// <summary>
        /// Ảnh đại diện người gửi (nếu có)
        /// </summary>
        public string? SenderAvatarUrl { get; set; }

        /// <summary>
        /// Nội dung tin nhắn trao đổi
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Đường dẫn tệp tài liệu / chứng từ thuế đính kèm (nếu có)
        /// </summary>
        public string? AttachmentUrl { get; set; }

        /// <summary>
        /// Loại tệp đính kèm: "IMAGE", "PDF", "DOCUMENT"
        /// </summary>
        public string? AttachmentType { get; set; }

        /// <summary>
        /// Đã được phía bên kia đọc hay chưa
        /// </summary>
        public bool IsRead { get; set; } = false;

        /// <summary>
        /// Thời điểm gửi tin nhắn (UTC)
        /// </summary>
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        // Navigation property
        public Booking? Booking { get; set; }
    }
}
