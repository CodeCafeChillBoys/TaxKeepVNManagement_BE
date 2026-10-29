using System.Collections.Generic;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.ExpertCertificateConfigurations;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface IExpertCertificateConfigService
    {
        Task<IEnumerable<ExpertCertificateConfigDto>> GetAllAsync(ExpertCertificateConfigQueryParameters? query = null);
        Task<ExpertCertificateConfigDto> GetByIdAsync(int id);
        Task<ExpertCertificateConfigDto> CreateAsync(CreateExpertCertificateConfigRequest request);
        Task<ExpertCertificateConfigDto> UpdateAsync(int id, UpdateExpertCertificateConfigRequest request);
        Task<ExpertCertificateConfigDto> ToggleActiveAsync(int id);
        Task DeleteAsync(int id);
    }
}