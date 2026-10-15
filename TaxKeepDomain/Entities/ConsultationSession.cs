using System;

namespace TaxKeepVN.Domain.Entities
{
    /// <summary>
    /// Bản ghi phiên tư vấn trực tuyến 1-1 (Live Consultation Session) giữa Khách hàng và Chuyên gia (Đặc tả 5)
    /// Lưu trữ Room URL Jitsi Meet, nhật ký hiện diện Check-in, thời lượng thực tế và Biên bản kết luận Shared Notes.
    /// </summary>
    public class ConsultationSession
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Khóa ngoại 1-1 liên kết tới phiên đặt lịch Booking
        /// </summary>
        public Guid BookingId { get; set; }

        /// <summary>
        /// Tên định danh duy nhất của phòng họp Jitsi Meet
        /// </summary>
        public string RoomName { get; set; } = string.Empty;

        /// <summary>
        /// Đường dẫn URL phòng họp trực tuyến Jitsi Meet
        /// </summary>
        public string RoomUrl { get; set; } = string.Empty;

        /// <summary>
        /// Thời điểm Khách hàng (User) bấm điểm danh vào phòng tư vấn
        /// </summary>
        public DateTimeOffset? UserCheckInAt { get; set; }

        /// <summary>
        /// Thời điểm Chuyên gia (Expert) bấm điểm danh vào phòng tư vấn
        /// </summary>
        public DateTimeOffset? ExpertCheckInAt { get; set; }

        /// <summary>
        /// Thời điểm phiên tư vấn chính thức bắt đầu (khi cả 2 bên cùng có mặt trong phòng - Chuyển sang IN_PROGRESS)
        /// </summary>
        public DateTimeOffset? StartedAt { get; set; }

        /// <summary>
        /// Thời điểm kết thúc phiên tư vấn
        /// </summary>
        public DateTimeOffset? EndedAt { get; set; }

        /// <summary>
        /// Tổng thời lượng thực tế hai bên làm việc trong phòng họp (tính theo phút)
        /// </summary>
        public int? ActualDurationMinutes { get; set; }

        /// <summary>
        /// Biên bản tóm tắt kết luận và giải pháp tư vấn do Chuyên gia lập (Bắt buộc theo BR-5.3 trước khi kết thúc ca)
        /// </summary>
        public string? SharedNotes { get; set; }

        /// <summary>
        /// Trạng thái vắng mặt nếu xảy ra No-Show: "NONE", "USER_NO_SHOW", "EXPERT_NO_SHOW"
        /// </summary>
        public string NoShowType { get; set; } = "NONE";

        /// <summary>
        /// Đối tượng báo cáo vắng mặt: "USER", "EXPERT", "SYSTEM"
        /// </summary>
        public string? NoShowReportedBy { get; set; }

        /// <summary>
        /// Thời điểm ghi nhận báo cáo vắng mặt
        /// </summary>
        public DateTimeOffset? NoShowReportedAt { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
