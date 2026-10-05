using System;
using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Application.DTOs.Experts
{
    /// <summary>
    /// Khung thời gian rảnh khả dụng của chuyên gia
    /// </summary>
    public class ExpertAvailableSlotDto
    {
        public Guid Id { get; set; }
        public DateOnly SlotDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public SessionType SessionType { get; set; }
        public bool IsBooked { get; set; }
        public bool IsActive { get; set; }
    }
}
