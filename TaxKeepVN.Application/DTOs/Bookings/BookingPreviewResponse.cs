using System;
using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Application.DTOs.Bookings
{
    /// <summary>
    /// Thông tin tóm tắt buổi hẹn trước khi xác nhận đặt (Đặc tả 3 - Mục 5 Luồng chính)
    /// </summary>
    public class BookingPreviewResponse
    {
        public Guid ExpertSlotId { get; set; }
        public Guid ExpertProfileId { get; set; }
        public string ExpertFullName { get; set; } = string.Empty;
        public string? ExpertAvatarUrl { get; set; }
        public string ExpertJobTitle { get; set; } = string.Empty;

        public DateOnly SlotDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public int DurationMinutes { get; set; }
        public SessionType SessionType { get; set; }

        public int SpecializationId { get; set; }
        public string SpecializationName { get; set; } = string.Empty;

        /// <summary>Tổng chi phí dự kiến (VNĐ)</summary>
        public decimal TotalFee { get; set; }

        /// <summary>Quy định thời gian đặt trước tối thiểu (Minimum Lead-time tính bằng giờ)</summary>
        public int MinimumLeadTimeHours { get; set; }

        /// <summary>Khung giờ có thỏa mãn điều kiện đặt không</summary>
        public bool IsValid { get; set; } = true;

        /// <summary>Lý do không hợp lệ nếu có</summary>
        public string? ValidationMessage { get; set; }
    }
}
