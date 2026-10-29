using System;

namespace TaxKeepVN.Application.DTOs.Consultations
{
    /// <summary>
    /// Thông tin nhắc nhở lịch hẹn meeting trực tuyến sắp tới dành cho User và Expert (Dashboard & Banner)
    /// </summary>
    public class UpcomingMeetingReminderDto
    {
        public bool HasUpcomingMeeting { get; set; }

        public Guid? BookingId { get; set; }

        public string? BookingCode { get; set; }

        public string? TopicTitle { get; set; }

        /// <summary>Họ tên đối tác trong buổi tư vấn (Khách hàng nếu xem bởi Expert, Chuyên gia nếu xem bởi Khách hàng)</summary>
        public string? PartnerName { get; set; }

        /// <summary>Vai trò đối tác: EXPERT hoặc TAXPAYER</summary>
        public string? PartnerRole { get; set; }

        public DateOnly? SlotDate { get; set; }

        public TimeOnly? StartTime { get; set; }

        public TimeOnly? EndTime { get; set; }

        /// <summary>Số phút còn lại trước khi bắt đầu (Âm nếu đã qua giờ bắt đầu nhưng chưa hết ca)</summary>
        public int? MinutesRemaining { get; set; }

        /// <summary>Phòng họp trực tuyến đã mở chưa (Mở trước X phút theo cấu hình SESSION_OPEN_BEFORE_MINUTES)</summary>
        public bool IsRoomOpen { get; set; }

        /// <summary>Đã có thể bấm vào phòng hay chưa</summary>
        public bool CanJoinRoom { get; set; }

        /// <summary>Đường dẫn phòng họp trực tuyến Jitsi Meet</summary>
        public string? RoomUrl { get; set; }

        /// <summary>Mức độ khẩn cấp: NORMAL (> 1h), SOON (<= 1h), OPENED (đã mở phòng), IN_PROGRESS (đang diễn ra)</summary>
        public string? UrgencyLevel { get; set; }

        /// <summary>Thông điệp nhắc nhở thân thiện hiển thị trên giao diện</summary>
        public string? ReminderMessage { get; set; }
    }
}
