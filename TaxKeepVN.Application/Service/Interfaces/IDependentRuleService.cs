using System;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Rules;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface IDependentRuleService
    {
        Task<PagedResult<DependentRuleResponseDto>> GetAllAsync(DependentRuleQueryParameters query);
        Task<DependentRuleResponseDto> GetByIdAsync(Guid ruleId);
        Task<DependentRuleResponseDto> CreateAsync(CreateDependentRuleDto dto);
        Task<DependentRuleResponseDto> UpdateAsync(Guid ruleId, UpdateDependentRuleDto dto);
        Task DeleteAsync(Guid ruleId);
    }
}
