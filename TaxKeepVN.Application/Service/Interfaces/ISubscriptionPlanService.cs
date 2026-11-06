using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Subscriptions;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface ISubscriptionPlanService
    {
        Task<List<SubscriptionPlanResponse>> GetAllActivePlansAsync();
        Task<List<SubscriptionPlanResponse>> GetAllPlansForAdminAsync();
        Task<SubscriptionPlanResponse> GetPlanByIdAsync(Guid id);
        Task<SubscriptionPlanResponse> CreatePlanAsync(CreateSubscriptionPlanRequest request);
        Task<SubscriptionPlanResponse> UpdatePlanAsync(Guid id, UpdateSubscriptionPlanRequest request);
        Task<bool> DeletePlanAsync(Guid id);
        Task SeedDefaultPlansIfEmptyAsync();
    }
}

