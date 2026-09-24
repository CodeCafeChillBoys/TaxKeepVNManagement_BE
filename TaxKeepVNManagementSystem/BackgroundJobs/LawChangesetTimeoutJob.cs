using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TaxKeepVN.Application.Law.Notification;
using TaxKeepVN.Domain.Constants;
using TaxKeepVN.Infrastructure.Contexts;

namespace TaxKeepVNManagementSystem.BackgroundJobs
{
    public class LawChangesetTimeoutJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<LawChangesetTimeoutJob> _logger;
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(20);

        public LawChangesetTimeoutJob(
            IServiceProvider serviceProvider,
            ILogger<LawChangesetTimeoutJob> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("LawChangesetTimeoutJob started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckTimeoutsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred in LawChangesetTimeoutJob.");
                }

                await Task.Delay(Interval, stoppingToken);
            }
        }

        private async Task CheckTimeoutsAsync(CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TaxKeepDbContext>();
            var notificationService = scope.ServiceProvider.GetRequiredService<ILawNotificationService>();

            var threshold = DateTime.UtcNow.Subtract(Timeout);

            var timedOutChangesets = await db.LawChangesets
                .Where(c => c.Status == LawConstants.ChangesetStatus.EXTRACTING && c.CreatedAt < threshold)
                .ToListAsync(ct);

            foreach (var changeset in timedOutChangesets)
            {
                _logger.LogWarning("Changeset {ChangesetId} timed out in EXTRACTING state (> 20 min). Setting to FAILED.", changeset.Id);

                changeset.Status = LawConstants.ChangesetStatus.FAILED;
                changeset.AiErrorCode = "E-AI_TIMEOUT";
                changeset.AiErrorMessage = "Hết thời gian chờ phản hồi từ AI service (quá 20 phút).";
                changeset.UpdatedAt = DateTime.UtcNow;

                await db.SaveChangesAsync(ct);

                await notificationService.NotifyChangesetFailedAsync(
                    changeset.CreatedBy?.ToString() ?? string.Empty,
                    changeset.AiTaskId ?? Guid.Empty,
                    new
                    {
                        changesetId = changeset.Id,
                        taskId = changeset.AiTaskId,
                        documentId = changeset.DocumentId,
                        status = LawConstants.ChangesetStatus.FAILED,
                        errorCode = "E-AI_TIMEOUT",
                        errorMessage = "Hết thời gian chờ phản hồi từ AI service (quá 20 phút)."
                    });
            }
        }
    }
}
