using System;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface IDependentReminderService
    {
        Task<DependentAgeReminderResponseDto> GetAgeTransitionRemindersAsync(Guid userId, int taxYear);
    }
}
