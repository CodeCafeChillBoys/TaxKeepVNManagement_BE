using System;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Subscriptions;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface ISubscriptionQuotaService
    {
        Task<SubscriptionQuotaStatusResponse> GetQuotaStatusAsync(Guid userId);
        Task CheckAndConsumeAiQuestionAsync(Guid userId);
        Task CheckAndConsumeOcrScanAsync(Guid userId);
        Task CheckCanExportPdfAsync(Guid userId);
        Task CheckCanExportZipAsync(Guid userId);
        Task<int> GetBookingDiscountPercentAsync(Guid userId);
    }
}

