using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Application.DTOs.ConsultationFeeConfigurations
{
    public class ConsultationFeeConfigQueryParameters
    {
        public SessionType? SessionType { get; set; }
        public bool? IsActive { get; set; }
    }
}
