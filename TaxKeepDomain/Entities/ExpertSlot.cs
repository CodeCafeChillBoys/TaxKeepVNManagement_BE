using System;
using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Domain.Entities
{
    /// <summary>
    /// Khung thời gian rảnh / khả dụng của chuyên gia để khách hàng tìm kiếm và đặt lịch
    /// </summary>
    public class ExpertSlot
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>FK → ExpertProfile.Id</summary>
        public Guid ExpertProfileId { get; set; }

        /// <summary>Ngày rảnh (VD: 2026-10-02)</summary>
        public DateOnly SlotDate { get; set; }

        /// <summary>Thời gian bắt đầu (VD: 09:00)</summary>
        public TimeOnly StartTime { get; set; }

        /// <summary>Thời gian kết thúc (VD: 10:00)</summary>
        public TimeOnly EndTime { get; set; }

        /// <summary>Hình thức tư vấn (ONLINE_MEETING, CHAT, VOICE_CALL)</summary>
        public SessionType SessionType { get; set; } = SessionType.ONLINE_MEETING;

        /// <summary>Đã có người đặt chưa</summary>
        public bool IsBooked { get; set; } = false;

        /// <summary>Chuyên gia có đang mở nhận slot này không (hoặc tạm khóa/nghỉ đột xuất)</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>Thời điểm hết hạn tạm giữ slot 10 phút chờ thanh toán</summary>
        public DateTimeOffset? HoldExpiresAt { get; set; }

        /// <summary>User ID đang tạm giữ slot này</summary>
        public Guid? HoldUserId { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        // Navigation property
        public ExpertProfile? ExpertProfile { get; set; }
        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    }
}
