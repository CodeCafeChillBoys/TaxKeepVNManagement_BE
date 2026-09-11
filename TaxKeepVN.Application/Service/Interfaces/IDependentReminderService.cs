using System;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Common;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface IDependentReminderService
    {
        Task<PagedResult<DTOs.AgeReminderItemDto>> GetAgeTransitionRemindersAsync(Guid userId, int taxYear, QueryParameters query);
    }
}
