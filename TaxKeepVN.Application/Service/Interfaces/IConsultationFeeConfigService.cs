using System.Collections.Generic;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.ConsultationFeeConfigurations;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface IConsultationFeeConfigService
    {
        Task<IEnumerable<ConsultationFeeConfigDto>> GetAllAsync(ConsultationFeeConfigQueryParameters? query = null);
        Task<ConsultationFeeConfigDto> GetByIdAsync(int id);
        Task<ConsultationFeeConfigDto> CreateAsync(CreateConsultationFeeConfigDto dto);
        Task<ConsultationFeeConfigDto> UpdateAsync(int id, UpdateConsultationFeeConfigDto dto);
        Task<bool> DeleteAsync(int id);
    }
}
