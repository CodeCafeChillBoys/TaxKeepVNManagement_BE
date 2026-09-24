using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using TaxKeepVN.Application.Law.Notification;
using TaxKeepVNManagementSystem.Hubs;

namespace TaxKeepVNManagementSystem.Services
{
    public class LawNotificationService : ILawNotificationService
    {
        private readonly IHubContext<TaxAIHub> _hubContext;
        private readonly ILogger<LawNotificationService> _logger;

        public LawNotificationService(IHubContext<TaxAIHub> hubContext, ILogger<LawNotificationService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task NotifyChangesetReadyAsync(string userId, Guid taskId, object payload)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(userId))
                {
                    await _hubContext.Clients.User(userId).SendAsync("OnLawChangesetReady", payload);
                }
                await _hubContext.Clients.Group($"task_{taskId}").SendAsync("OnLawChangesetReady", payload);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send SignalR OnLawChangesetReady notification for task {TaskId}", taskId);
            }
        }

        public async Task NotifyChangesetFailedAsync(string userId, Guid taskId, object payload)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(userId))
                {
                    await _hubContext.Clients.User(userId).SendAsync("OnLawChangesetFailed", payload);
                }
                await _hubContext.Clients.Group($"task_{taskId}").SendAsync("OnLawChangesetFailed", payload);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send SignalR OnLawChangesetFailed notification for task {TaskId}", taskId);
            }
        }

        public async Task NotifyRevisionMergedAsync(string userId, object payload)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(userId))
                {
                    await _hubContext.Clients.User(userId).SendAsync("OnLawRevisionMerged", payload);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send SignalR OnLawRevisionMerged notification to user {UserId}", userId);
            }
        }
    }
}
