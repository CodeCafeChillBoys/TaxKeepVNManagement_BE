using System;
using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Application.DTOs.Consultations
{
    /// <summary>
    /// Thông tin chi tiết phiên tư vấn trực tuyến 1-1 (Live Consultation Session) (Đặc tả 5)
    /// </summary>
    public class ConsultationSessionResponse
    {
        public Guid Id { get; set; }
        public Guid BookingId { get; set; }
        public string BookingCode { get; set; } = string.Empty;
        public string TopicTitle { get; set; } = string.Empty;
        public string? ProblemDescription { get; set; }
        public string SessionType { get; set; } = "ONLINE_MEETING";
        public int ScheduledDurationMinutes { get; set; } = 60;
        public DateOnly SlotDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }

        public string RoomName { get; set; } = string.Empty;
        public string RoomUrl { get; set; } = string.Empty;

        public DateTimeOffset? UserCheckInAt { get; set; }
        public DateTimeOffset? ExpertCheckInAt { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? EndedAt { get; set; }
        public int? ActualDurationMinutes { get; set; }
        public int RemainingMinutes { get; set; }

        public BookingStatus Status { get; set; }
        public string? SharedNotes { get; set; }
        public string NoShowType { get; set; } = "NONE";

        public bool IsRoomOpen { get; set; }
        public bool IsUserPresent { get; set; }
        public bool IsExpertPresent { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    /// <summary>
    /// Kết quả phản hồi khi Khách hàng hoặc Chuyên gia điểm danh vào phòng tư vấn Jitsi Meet
    /// </summary>
    public class JoinConsultationResponse
    {
        public Guid SessionId { get; set; }
        public Guid BookingId { get; set; }
        public string BookingCode { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public string RoomUrl { get; set; } = string.Empty;
        public DateTimeOffset CheckedInAt { get; set; }
        public string ParticipantRole { get; set; } = string.Empty;
        public bool BothPresent { get; set; }
        public BookingStatus BookingStatus { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
