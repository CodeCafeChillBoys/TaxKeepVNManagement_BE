using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Subscriptions;
using TaxKeepVN.Domain.Entities;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface IUserSubscriptionService
    {
        Task<UserSubscriptionResponse?> GetActiveSubscriptionAsync(Guid userId);
        Task<UserSubscription> GetOrCreateActiveSubscriptionEntityAsync(Guid userId);
        Task<SubscriptionPurchaseResponse> PurchaseAsync(Guid userId, PurchaseSubscriptionRequest request);
        Task<bool> HandlePaymentCallbackAsync(long orderCode, bool isSuccess);
        Task<List<UserSubscriptionResponse>> GetSubscriptionHistoryAsync(Guid userId);
        Task<UserSubscription> AutoAssignBasicPlanIfNoneAsync(Guid userId);
    }
}

