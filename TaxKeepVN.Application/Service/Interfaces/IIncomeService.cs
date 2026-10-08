using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Requests.Income;
using TaxKeepVN.Application.DTOs.Responses.Income;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface IIncomeService
    {
        Task<IncomeResponseDto> CreateIncomeAsync(Guid userId, CreateIncomeRequest request);
        Task<IncomeResponseDto> GetIncomeByIdAsync(Guid userId, Guid id);
        Task<List<IncomeGroupedResponseDto>> GetMyIncomesAsync(Guid userId, int year);
        Task<IncomeResponseDto> UpdateIncomeAsync(Guid userId, Guid id, UpdateIncomeRequest request);
        Task DeleteIncomeAsync(Guid userId, Guid id);
    }
}
