using System;
using System.Threading.Tasks;

namespace TaxKeepVN.Application.Law.Notification
{
    public interface ILawNotificationService
    {
        Task NotifyChangesetReadyAsync(string userId, Guid taskId, object payload);
        Task NotifyChangesetFailedAsync(string userId, Guid taskId, object payload);
        Task NotifyRevisionMergedAsync(string userId, object payload);
    }
}
