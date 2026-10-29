using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVNManagementSystem.BackgroundJobs
{
    public class AgeTransitionReminderJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AgeTransitionReminderJob> _logger;

        public AgeTransitionReminderJob(IServiceProvider serviceProvider, ILogger<AgeTransitionReminderJob> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AgeTransitionReminderJob is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                        await ProcessRemindersAsync(unitOfWork);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred executing AgeTransitionReminderJob.");
                }

                // Run every 24 hours. For testing, we can set it to smaller value.
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }

        private async Task ProcessRemindersAsync(IUnitOfWork unitOfWork)
        {
            var dependentRepo = unitOfWork.Repository<Dependent>();
            var notificationRepo = unitOfWork.Repository<SystemNotification>();

            var dependents = await dependentRepo.FindAsync(d => !d.IsDeleted && d.CurrentGroup == DependentGroup.CHILD_UNDER_18);
            var currentDate = DateTime.UtcNow;
            int currentYear = currentDate.Year;

            foreach (var dep in dependents)
            {
                var turning18Date = dep.BirthDate.AddYears(18);
                var daysRemaining = (turning18Date - currentDate).Days;

                bool isTurningSoon = daysRemaining >= 0 && daysRemaining <= 60;
                bool isAlready18 = daysRemaining < 0 && turning18Date.Year <= currentYear;

                if (isTurningSoon || isAlready18)
                {
                    string targetUrl = $"/dependents/{dep.Id}";
                    
                    var existingNotifs = await notificationRepo.FindAsync(n => 
                        n.UserId == dep.TaxpayerId && 
                        n.NotificationType == "AGE_TRANSITION" && 
                        n.TargetActionUrl == targetUrl &&
                        n.CreatedAt.Year == currentYear);

                    if (!existingNotifs.Any())
                    {
                        string title = isTurningSoon ? "Sắp đến tuổi trưởng thành" : "Đã đến tuổi trưởng thành";
                        string message = isTurningSoon 
                            ? $"Người phụ thuộc {dep.FullName} sẽ tròn 18 tuổi vào ngày {turning18Date:dd/MM/yyyy}. Vui lòng cập nhật hồ sơ sinh viên nếu tiếp tục theo học."
                            : $"Người phụ thuộc {dep.FullName} đã tròn 18 tuổi. Khoản giảm trừ sẽ bị tạm dừng nếu không bổ sung giấy tờ sinh viên.";

                        var notification = new SystemNotification
                        {
                            NotificationId = Guid.NewGuid(),
                            UserId = dep.TaxpayerId,
                            Title = title,
                            Message = message,
                            NotificationType = "AGE_TRANSITION",
                            IsRead = false,
                            TargetActionUrl = targetUrl,
                            CreatedAt = DateTime.UtcNow
                        };

                        await notificationRepo.AddAsync(notification);
                    }
                }
            }

            await unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Processed age transition reminders.");
        }
    }
}
