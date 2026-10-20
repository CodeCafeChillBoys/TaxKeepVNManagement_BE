using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.SearchConfigs;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface IExpertSearchConfigService
    {
        Task<ExpertSearchCriteriaConfigDto> GetConfigAsync();
        Task<ExpertSearchCriteriaConfigDto> UpdateConfigAsync(ExpertSearchCriteriaConfigDto newConfig);
        Task<ExpertSearchCriteriaConfigDto> ResetToDefaultsAsync();
    }
}
