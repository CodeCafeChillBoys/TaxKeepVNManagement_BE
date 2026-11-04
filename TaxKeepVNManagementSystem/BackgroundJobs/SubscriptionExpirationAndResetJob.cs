using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVNManagementSystem.BackgroundJobs
{
    /// <summary>
    /// Background Job tự động quét kiểm tra các gói dịch vụ hết hạn và gửi thông báo nhắc gia hạn.
    /// Tự động hạ cấp (downgrade) về gói Basic khi hết hạn.
    /// </summary>
    public class SubscriptionExpirationAndResetJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SubscriptionExpirationAndResetJob> _logger;

        public SubscriptionExpirationAndResetJob(
            IServiceProvider serviceProvider,
            ILogger<SubscriptionExpirationAndResetJob> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SubscriptionExpirationAndResetJob is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessExpiringSubscriptionsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi xảy ra trong quá trình xử lý SubscriptionExpirationAndResetJob.");
                }

                // Quét định kỳ mỗi 1 giờ
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }

        private async Task ProcessExpiringSubscriptionsAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var userSubService = scope.ServiceProvider.GetRequiredService<IUserSubscriptionService>();

            var now = DateTimeOffset.UtcNow;
            var subRepo = unitOfWork.Repository<UserSubscription>();
            var notifRepo = unitOfWork.Repository<SystemNotification>();

            // 1. Quét các gói đã hết hạn
            var activeSubs = (await subRepo.FindAsync(s => s.Status == "active" && s.ExpiresAt.HasValue)).ToList();
            var expiredSubs = activeSubs.Where(s => s.ExpiresAt!.Value <= now).ToList();

            foreach (var exp in expiredSubs)
            {
                exp.Status = "expired";
                exp.UpdatedAt = now;
                subRepo.Update(exp);

                // Gửi thông báo cho user
                await notifRepo.AddAsync(new SystemNotification
                {
                    NotificationId = Guid.NewGuid(),
                    UserId = exp.UserId,
                    Title = "Gói dịch vụ đã hết hạn",
                    Message = "Gói dịch vụ của bạn đã hết hạn. Hệ thống đã chuyển tài khoản của bạn về gói Basic miễn phí.",
                    NotificationType = "SUBSCRIPTION_EXPIRED",
                    IsRead = false,
                    TargetActionUrl = "/subscription",
                    CreatedAt = now.UtcDateTime
                });

                // Chuyển về Basic
                await userSubService.AutoAssignBasicPlanIfNoneAsync(exp.UserId);
            }

            if (expiredSubs.Any())
            {
                await unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Đã xử lý hết hạn {Count} gói đăng ký.", expiredSubs.Count);
            }
        }
    }
}
