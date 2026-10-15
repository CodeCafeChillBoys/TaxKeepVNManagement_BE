using System;
using System.Collections.Generic;
using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Application.DTOs.Experts
{
    /// <summary>
    /// Request tạo 1 khung giờ rảnh mới của chuyên gia
    /// </summary>
    public class CreateExpertSlotRequest
    {
        /// <summary>Ngày rảnh (VD: 2026-10-15)</summary>
        public DateOnly SlotDate { get; set; }

        /// <summary>Thời gian bắt đầu (VD: 09:00)</summary>
        public TimeOnly StartTime { get; set; }

        /// <summary>Thời gian kết thúc (VD: 10:00)</summary>
        public TimeOnly EndTime { get; set; }

        /// <summary>Hình thức tư vấn (ONLINE_MEETING, CHAT, VOICE_CALL)</summary>
        public SessionType SessionType { get; set; } = SessionType.ONLINE_MEETING;
    }


    /// <summary>
    /// Request cập nhật khung giờ rảnh
    /// </summary>
    public class UpdateExpertSlotRequest
    {
        /// <summary>Ngày rảnh (VD: 2026-10-15)</summary>
        public DateOnly SlotDate { get; set; }

        /// <summary>Thời gian bắt đầu (VD: 09:00)</summary>
        public TimeOnly StartTime { get; set; }

        /// <summary>Thời gian kết thúc (VD: 10:00)</summary>
        public TimeOnly EndTime { get; set; }

        /// <summary>Hình thức tư vấn</summary>
        public SessionType SessionType { get; set; } = SessionType.ONLINE_MEETING;

        /// <summary>Bật / tắt nhận lịch cho khung giờ này</summary>
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// Bộ lọc tìm kiếm danh sách khung giờ của chuyên gia
    /// </summary>
    public class ExpertSlotFilterParameters
    {
        /// <summary>Từ ngày</summary>
        public DateOnly? FromDate { get; set; }

        /// <summary>Đến ngày</summary>
        public DateOnly? ToDate { get; set; }

        /// <summary>Lọc theo hình thức tư vấn</summary>
        public SessionType? SessionType { get; set; }

        /// <summary>Lọc theo trạng thái đã được đặt hay chưa</summary>
        public bool? IsBooked { get; set; }

        /// <summary>Lọc theo trạng thái đang kích hoạt hay tạm tắt</summary>
        public bool? IsActive { get; set; }
    }

    /// <summary>
    /// Chi tiết khung giờ làm việc của chuyên gia
    /// </summary>
    public class ExpertSlotDetailResponse
    {
        public Guid Id { get; set; }
        public Guid ExpertProfileId { get; set; }
        public DateOnly SlotDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public int DurationMinutes { get; set; }
        public SessionType SessionType { get; set; }
        public bool IsBooked { get; set; }
        public bool IsActive { get; set; }
        public DateTimeOffset? HoldExpiresAt { get; set; }
        public bool IsHolding { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
