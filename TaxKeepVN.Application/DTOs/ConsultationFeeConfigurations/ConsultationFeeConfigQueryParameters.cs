using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Application.DTOs.ConsultationFeeConfigurations
{
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class ConsultationFeeConfigQueryParameters
    {
        public SessionType? SessionType { get; set; }
        public bool? IsActive { get; set; }
    }
}
