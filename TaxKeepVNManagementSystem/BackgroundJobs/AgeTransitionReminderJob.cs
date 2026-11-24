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
            var groupRepo = unitOfWork.Repository<DependentGroupCatalog>();

            var allGroups = groupRepo != null
                ? (await groupRepo.FindAsync(g => g.IsActive)).ToList()
                : new List<DependentGroupCatalog>();

            if (allGroups.Count == 0)
            {
                allGroups = new List<DependentGroupCatalog>
                {
                    new() { Code = "CHILD_UNDER_18", Name = "Con dưới 18 tuổi", RelationshipCode = "CHILD", MaxAge = 18, Level = 0, IsActive = true }
                };
            }

            var groupsWithMaxAge = allGroups
                .Where(g => g.MaxAge.HasValue)
                .ToDictionary(g => g.Code, StringComparer.OrdinalIgnoreCase);

            var allDeps = await dependentRepo.FindAsync(d => !d.IsDeleted);
            var dependents = allDeps.Where(d => groupsWithMaxAge.ContainsKey(d.CurrentGroup)).ToList();
            var currentDate = DateTime.UtcNow;
            int currentYear = currentDate.Year;

            foreach (var dep in dependents)
            {
                var curGroup = groupsWithMaxAge[dep.CurrentGroup];
                var maxAge = curGroup.MaxAge!.Value;
                var turningDate = dep.BirthDate.AddYears(maxAge);
                var daysRemaining = (turningDate - currentDate).Days;

                bool isTurningSoon = daysRemaining >= 0 && daysRemaining <= 60;
                bool isAlreadyAge = daysRemaining < 0 && turningDate.Year <= currentYear;

                if (isTurningSoon || isAlreadyAge)
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
                            ? $"Người phụ thuộc {dep.FullName} sẽ tròn {maxAge} tuổi vào ngày {turningDate:dd/MM/yyyy}. Vui lòng cập nhật hồ sơ sinh viên nếu tiếp tục theo học."
                            : $"Người phụ thuộc {dep.FullName} đã tròn {maxAge} tuổi. Khoản giảm trừ sẽ bị tạm dừng nếu không bổ sung giấy tờ sinh viên.";

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
