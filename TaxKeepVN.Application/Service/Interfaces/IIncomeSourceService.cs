using System;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.IncomeSources;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface IIncomeSourceService
    {
        Task<PagedResult<IncomeSourceResponseDto>> GetAllByUserIdAsync(Guid userId, IncomeSourceQueryParameters query);
        Task<IncomeSourceResponseDto> GetByIdAsync(Guid id, Guid userId);
        Task<IncomeSourceSummaryDto> GetSummaryByUserIdAsync(Guid userId, int taxYear);
        Task<IncomeSourceResponseDto> CreateAsync(Guid userId, IncomeSourceCreateDto dto);
        Task<IncomeSourceResponseDto> UpdateAsync(Guid id, Guid userId, IncomeSourceUpdateDto dto);
        Task DeleteAsync(Guid id, Guid userId);
    }
}
