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

    /// <summary>
    /// Khung giờ mẫu theo thứ trong tuần
    /// </summary>
    public class WeeklySlotItemDto
    {
        /// <summary>Thứ trong tuần (Sunday = 0, Monday = 1, Tuesday = 2, ..., Saturday = 6)</summary>
        public DayOfWeek DayOfWeek { get; set; }

        /// <summary>Thời gian bắt đầu (VD: 19:00)</summary>
        public TimeOnly StartTime { get; set; }

        /// <summary>Thời gian kết thúc (VD: 20:00)</summary>
        public TimeOnly EndTime { get; set; }

        /// <summary>Hình thức tư vấn</summary>
        public SessionType SessionType { get; set; } = SessionType.ONLINE_MEETING;
    }

    /// <summary>
    /// Request tạo hàng loạt khung giờ làm việc theo tuần cho nhiều tuần liên tiếp
    /// </summary>
    public class CreateRecurringSlotRequest
    {
        /// <summary>Ngày bắt đầu áp dụng (VD: 2026-10-20)</summary>
        public DateOnly StartDate { get; set; }

        /// <summary>Số tuần muốn áp dụng liên tiếp (từ 1 đến 12 tuần, mặc định 4 tuần)</summary>
        public int NumberOfWeeks { get; set; } = 4;

        /// <summary>Danh sách các khung giờ lặp cố định theo thứ trong tuần</summary>
        public List<WeeklySlotItemDto> WeeklySlots { get; set; } = new();
    }

    /// <summary>
    /// Kết quả trả về sau khi tạo hàng loạt khung giờ làm việc
    /// </summary>
    public class RecurringSlotBatchResultResponse
    {
        public int TotalCreated { get; set; }
        public int TotalSkipped { get; set; }
        public DateOnly FromDate { get; set; }
        public DateOnly ToDate { get; set; }
        public List<ExpertSlotDetailResponse> CreatedSlots { get; set; } = new();
    }

    /// <summary>
    /// Request hủy slot khẩn cấp
    /// </summary>
    public class EmergencyCancelSlotRequest
    {
        /// <summary>Lý do hủy khẩn cấp (bắt buộc, tối thiểu 10 ký tự)</summary>
        public string Reason { get; set; } = string.Empty;
    }
    /// <summary>
    /// Kết quả trả về sau khi hủy khẩn cấp
    /// </summary>
    public class EmergencyCancelSlotResponse
    {
        public Guid SlotId { get; set; }
        public DateOnly SlotDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }

        /// <summary>Khung giờ này đã có khách đặt trước đó hay chưa</summary>
        public bool HadBooking { get; set; }
        public Guid? BookingId { get; set; }
        public string? BookingCode { get; set; }

        /// <summary>Có bị tính là hủy sát giờ (dưới 12 tiếng) không</summary>
        public bool IsLateCancel { get; set; }

        /// <summary>Số tiền phạt nếu hủy sát giờ (VNĐ)</summary>
        public decimal PenaltyAmount { get; set; }

        /// <summary>Thông báo tóm tắt</summary>
        public string Message { get; set; } = string.Empty;
    }
}
