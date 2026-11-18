using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using TaxKeepVN.Application.DTOs.Common;

namespace TaxKeepVN.Application.DTOs.Consultations
{
    /// <summary>
    /// Tham số truy vấn danh sách tin nhắn chat hỗ trợ 5 tính năng chuẩn API Guidelines 2.2
    /// </summary>
    public class ConsultationMessageQueryParameters
    {
        /// <summary>Tìm kiếm từ khóa trong nội dung tin nhắn (?search=hopdong)</summary>
        public string? Search { get; set; }

        /// <summary>Sắp xếp theo trường (?sort=createdAt hoặc ?sort=-createdAt)</summary>
        public string? Sort { get; set; } = "createdAt";

        /// <summary>Số trang (Mặc định 1)</summary>
        public int Page { get; set; } = 1;

        /// <summary>Kích thước trang (Mặc định 50, tối đa 100)</summary>
        public int Size { get; set; } = 50;
    }

    /// <summary>
    /// Request gửi tin nhắn mới trong phòng chat tư vấn 1-1 (Tier 1 Data Annotations)
    /// </summary>
    public class SendConsultationMessageRequest
    {
        /// <summary>
        /// Nội dung tin nhắn trao đổi
        /// </summary>
        [Required(ErrorMessage = "Nội dung tin nhắn không được để trống.")]
        [StringLength(4000, MinimumLength = 1, ErrorMessage = "Nội dung tin nhắn phải từ 1 đến 4000 ký tự.")]
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Đường dẫn tệp tài liệu / chứng từ thuế đính kèm (nếu có)
        /// </summary>
        [MaxLength(1000, ErrorMessage = "Đường dẫn tệp đính kèm không vượt quá 1000 ký tự.")]
        public string? AttachmentUrl { get; set; }

        /// <summary>
        /// Loại tệp đính kèm: "IMAGE", "PDF", "DOCUMENT"
        /// </summary>
        [MaxLength(50, ErrorMessage = "Loại tệp đính kèm không vượt quá 50 ký tự.")]
        public string? AttachmentType { get; set; }
    }

    /// <summary>
    /// Request cập nhật trạng thái đã đọc của tin nhắn
    /// </summary>
    public class UpdateMessageReadStatusRequest
    {
        /// <summary>
        /// Đánh dấu đã đọc toàn bộ tin nhắn đối phương gửi đến
        /// </summary>
        public bool MarkAllAsRead { get; set; } = true;
    }

    /// <summary>
    /// DTO chi tiết một tin nhắn trao đổi
    /// </summary>
    public class ConsultationMessageDto
    {
        public Guid Id { get; set; }
        public Guid BookingId { get; set; }
        public Guid SenderId { get; set; }
        public string SenderRole { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string? SenderAvatarUrl { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? AttachmentUrl { get; set; }
        public string? AttachmentType { get; set; }
        public bool IsRead { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        /// <summary>
        /// Cờ xác định tin nhắn này do chính user hiện tại gửi
        /// </summary>
        public bool IsMine { get; set; }
    }

    /// <summary>
    /// Thông tin đối tác trò chuyện (Chuyên gia hoặc Khách hàng)
    /// </summary>
    public class ConsultationChatPartnerDto
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string Role { get; set; } = string.Empty;
        public string? JobTitle { get; set; }
        public string? SpecializationName { get; set; }
    }

    /// <summary>
    /// Toàn bộ lịch sử trò chuyện và ngữ cảnh phiên tư vấn
    /// </summary>
    public class ConsultationChatHistoryResponse
    {
        public Guid BookingId { get; set; }
        public string BookingCode { get; set; } = string.Empty;
        public string TopicTitle { get; set; } = string.Empty;
        public string SessionType { get; set; } = string.Empty;
        public string BookingStatus { get; set; } = string.Empty;
        public string? RoomUrl { get; set; }
        public ConsultationChatPartnerDto Partner { get; set; } = new ConsultationChatPartnerDto();
        public PagedResult<ConsultationMessageDto> Messages { get; set; } = new PagedResult<ConsultationMessageDto>();
    }
}
