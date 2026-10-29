using System;
using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Application.DTOs.Experts
{
    /// <summary>
    /// Bảng biểu phí tư vấn chính thức của chuyên gia
    /// </summary>
    public class ExpertPublicFeeDto
    {
        public Guid Id { get; set; }
        public SessionType SessionType { get; set; }
        public int DurationMinutes { get; set; }
        public decimal Fee { get; set; }
    }
}
