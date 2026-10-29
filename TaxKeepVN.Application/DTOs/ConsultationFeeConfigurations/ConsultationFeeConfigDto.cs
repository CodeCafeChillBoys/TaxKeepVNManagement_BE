using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Application.DTOs.ConsultationFeeConfigurations
{
    public class ConsultationFeeConfigDto
    {
        public int Id { get; set; }
        public SessionType SessionType { get; set; }
        public int DurationMinutes { get; set; }
        public decimal MinFee { get; set; }
        public decimal MaxFee { get; set; }
        public bool IsActive { get; set; }
    }
}
