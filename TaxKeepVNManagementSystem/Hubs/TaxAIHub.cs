using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace TaxKeepVNManagementSystem.Hubs
{
    [Authorize]
    public class TaxAIHub : Hub
    {
        private readonly ILogger<TaxAIHub> _logger;

        public TaxAIHub(ILogger<TaxAIHub> logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier ?? "Anonymous";
            _logger.LogInformation("SignalR Client connected: ConnectionId={ConnectionId}, UserId={UserId}",
                Context.ConnectionId, userId);

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.UserIdentifier ?? "Anonymous";
            _logger.LogInformation("SignalR Client disconnected: ConnectionId={ConnectionId}, UserId={UserId}",
                Context.ConnectionId, userId);

            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Cho phép Client đăng ký theo dõi kết quả của một Task cụ thể
        /// </summary>
        public async Task JoinTaskGroup(string taskId)
        {
            if (!string.IsNullOrWhiteSpace(taskId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"task_{taskId}");
                _logger.LogInformation("Connection {ConnectionId} joined group: task_{TaskId}",
                    Context.ConnectionId, taskId);
            }
        }

        /// <summary>
        /// Rời nhóm theo dõi Task
        /// </summary>
        public async Task LeaveTaskGroup(string taskId)
        {
            if (!string.IsNullOrWhiteSpace(taskId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"task_{taskId}");
            }
        }
    }
}
