using System;
using System.Collections.Generic;
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
    public class TaxSettlementReminderJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TaxSettlementReminderJob> _logger;

        public TaxSettlementReminderJob(
            IServiceProvider serviceProvider,
            ILogger<TaxSettlementReminderJob> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("TaxSettlementReminderJob is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                TimeSpan delayUntilNextRun;

                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                        var configService = scope.ServiceProvider.GetRequiredService<ISystemConfigService>();

                        await ProcessTaxRemindersAsync(unitOfWork, configService);
                    }

                    // Tính thời gian chờ tới 08:00 AM sáng hôm sau
                    delayUntilNextRun = GetDelayUntilNext8AM();
                    _logger.LogInformation("TaxSettlementReminderJob executed successfully. Next run at 08:00 AM (in {TotalHours:F1}h).", delayUntilNextRun.TotalHours);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi xảy ra khi thực thi TaxSettlementReminderJob: {Message}", ex.Message);
                    // Nếu lỗi do thiếu cấu hình hoặc kết nối, thử lại sau 1 giờ thay vì chờ 24h để Admin cấu hình xong là job chạy lại ngay
                    delayUntilNextRun = TimeSpan.FromHours(1);
                }

                await Task.Delay(delayUntilNextRun, stoppingToken);
            }
        }

        private async Task ProcessTaxRemindersAsync(IUnitOfWork unitOfWork, ISystemConfigService configService)
        {
            // ── 1. ĐỌC CẤU HÌNH ĐỘNG TỪ DB (TUYỆT ĐỐI KHÔNG HARDCODE) ───────────────
            List<int> reminderDays;
            int deadlineMonth;
            int deadlineDay;

            try
            {
                string rawReminderDays = await configService.GetRequiredConfigValueAsync("TAX_SETTLEMENT_REMINDER_DAYS");
                string rawMonth = await configService.GetRequiredConfigValueAsync("TAX_SETTLEMENT_DEADLINE_MONTH");
                string rawDay = await configService.GetRequiredConfigValueAsync("TAX_SETTLEMENT_DEADLINE_DAY");

                reminderDays = rawReminderDays
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => int.Parse(s.Trim()))
                    .OrderByDescending(d => d)
                    .ToList();

                deadlineMonth = int.Parse(rawMonth.Trim());
                deadlineDay = int.Parse(rawDay.Trim());
            }
            catch (Exception ex)
            {
                // THÔNG BÁO CHO ADMIN VÀ QUĂNG RA EXCEPTION
                await NotifyAdminConfigErrorAsync(unitOfWork, ex.Message);
                throw new InvalidOperationException($"[TaxSettlementReminderJob] Thiếu cấu hình bắt buộc trong CSDL: {ex.Message}", ex);
            }

            // ── 2. XÁC ĐỊNH MỐC THỜI GIAN NHẮC ──────────────────────────────────────
            var today = DateTime.UtcNow.Date;
            int currentYear = today.Year;
            int taxYear = currentYear - 1; // Quyết toán cho thu nhập năm trước

            var deadlineDate = new DateTime(currentYear, deadlineMonth, deadlineDay);
            int daysRemaining = (deadlineDate - today).Days;

            string? currentMilestone = null;
            if (reminderDays.Contains(daysRemaining))
            {
                currentMilestone = $"{daysRemaining}D";
            }
            else if (daysRemaining < 0 && daysRemaining >= -7)
            {
                currentMilestone = "OVERDUE";
            }

            if (string.IsNullOrEmpty(currentMilestone))
            {
                _logger.LogInformation("Hôm nay (còn {Days} ngày đến hạn {Deadline:dd/MM/yyyy}) không thuộc mốc nhắc nào trong DB ({Milestones}).", 
                    daysRemaining, deadlineDate, string.Join(", ", reminderDays));
                return;
            }

            _logger.LogInformation("Kích hoạt đợt nhắc quyết toán thuế cho mốc: {Milestone} (Kỳ tính thuế: {TaxYear})", currentMilestone, taxYear);

            // ── 3. QUÉT DANH SÁCH NNT VÀ GOM HÀNG ĐỢI (CHỐNG GỬI TRÙNG) ────────────
            var userRepo = unitOfWork.Repository<User>();
            var notificationRepo = unitOfWork.Repository<SystemNotification>();

            var taxpayers = await userRepo.FindAsync(u => u.Status == "active" && u.UserRole == "taxpayer");
            var notificationsQueue = new List<SystemNotification>();

            foreach (var taxpayer in taxpayers)
            {
                string targetUrl = $"/tax-settlement?taxYear={taxYear}&milestone={currentMilestone}";

                // Kiểm tra Deduplication: Đã gửi thông báo cho mốc này trong năm hiện tại chưa?
                var existingNotifs = await notificationRepo.FindAsync(n =>
                    n.UserId == taxpayer.UserId &&
                    n.NotificationType == "TAX_SETTLEMENT_REMINDER" &&
                    n.TargetActionUrl == targetUrl &&
                    n.CreatedAt.Year == currentYear);

                if (existingNotifs.Any())
                {
                    continue; // Đã gửi mốc này rồi -> Bỏ qua chống spam trùng
                }

                var (title, message) = BuildNotificationContent(taxpayer.FullName, taxYear, daysRemaining, currentMilestone, deadlineDate);

                notificationsQueue.Add(new SystemNotification
                {
                    NotificationId = Guid.NewGuid(),
                    UserId = taxpayer.UserId,
                    Title = title,
                    Message = message,
                    NotificationType = "TAX_SETTLEMENT_REMINDER",
                    IsRead = false,
                    TargetActionUrl = targetUrl,
                    CreatedAt = DateTime.UtcNow
                });
            }

            // ── 4. LƯU BATCH HÀNG ĐỢI THÔNG BÁO VÀO CSDL ────────────────────────────
            if (notificationsQueue.Any())
            {
                foreach (var notif in notificationsQueue)
                {
                    await notificationRepo.AddAsync(notif);
                }

                await unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Đã lưu và gửi thành công {Count} thông báo nhắc quyết toán thuế.", notificationsQueue.Count);
            }
        }

        /// <summary>
        /// Gửi thông báo cảnh báo lỗi cấu hình trực tiếp vào hộp thư hệ thống của Admin
        /// </summary>
        private async Task NotifyAdminConfigErrorAsync(IUnitOfWork unitOfWork, string errorMessage)
        {
            try
            {
                var userRepo = unitOfWork.Repository<User>();
                var notificationRepo = unitOfWork.Repository<SystemNotification>();

                var admins = await userRepo.FindAsync(u => u.UserRole.ToLower() == "admin");
                var today = DateTime.UtcNow;

                foreach (var admin in admins)
                {
                    // Chống spam cảnh báo cho admin (tối đa 1 lần/ngày)
                    var existingAlerts = await notificationRepo.FindAsync(n =>
                        n.UserId == admin.UserId &&
                        n.NotificationType == "ADMIN_CONFIG_ALERT" &&
                        n.CreatedAt.Date == today.Date);

                    if (!existingAlerts.Any())
                    {
                        await notificationRepo.AddAsync(new SystemNotification
                        {
                            NotificationId = Guid.NewGuid(),
                            UserId = admin.UserId,
                            Title = "CẢNH BÁO: Lỗi cấu hình Quyết toán thuế",
                            Message = $"Tiến trình nhắc quyết toán thuế bị gián đoạn do thiếu hoặc sai định dạng cấu hình trong CSDL: {errorMessage}. Vui lòng vào API / CSDL để cập nhật lại cấu hình.",
                            NotificationType = "ADMIN_CONFIG_ALERT",
                            IsRead = false,
                            TargetActionUrl = "/admin/system-configs",
                            CreatedAt = today
                        });
                    }
                }

                await unitOfWork.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể gửi cảnh báo cấu hình tới Admin: {Message}", ex.Message);
            }
        }

        private static (string Title, string Message) BuildNotificationContent(string fullName, int taxYear, int daysRemaining, string milestone, DateTime deadlineDate)
        {
            string formattedDeadline = deadlineDate.ToString("dd/MM");

            if (milestone == "OVERDUE")
            {
                return (
                    "Hồ sơ quyết toán thuế đã quá hạn",
                    $"Chào {fullName}, thời hạn nộp quyết toán thuế TNCN năm {taxYear} (hạn {formattedDeadline}) đã kết thúc. Vui lòng rà soát và nộp bổ sung hồ sơ sớm để tránh phát sinh tiền chậm nộp."
                );
            }

            if (daysRemaining <= 3)
            {
                return (
                    "Khẩn cấp: Chỉ còn 3 ngày quyết toán thuế",
                    $"Chào {fullName}, hạn chót nộp hồ sơ quyết toán thuế TNCN năm {taxYear} là ngày {formattedDeadline} (chỉ còn {daysRemaining} ngày). Hãy kiểm tra lại chứng từ và nộp hồ sơ ngay!"
                );
            }

            if (daysRemaining <= 15)
            {
                return (
                    $"Sắp đến hạn quyết toán thuế năm {taxYear}",
                    $"Chào {fullName}, còn {daysRemaining} ngày nữa là đến hạn chót quyết toán thuế TNCN ({formattedDeadline}). Bạn hãy kiểm tra lại các khoản giảm trừ người phụ thuộc và y tế/học phí."
                );
            }

            return (
                $"Nhắc chuẩn bị hồ sơ quyết toán thuế {taxYear}",
                $"Chào {fullName}, còn {daysRemaining} ngày nữa là đến hạn quyết toán thuế TNCN năm {taxYear} ({formattedDeadline}). Hãy tải lên các chứng từ y tế, giáo dục để kịp thời tính giảm trừ."
            );
        }

        private static TimeSpan GetDelayUntilNext8AM()
        {
            var now = DateTime.UtcNow.AddHours(7); // Múi giờ Việt Nam UTC+7
            var nextRun = now.Date.AddHours(8);    // 08:00 AM hôm nay

            if (now >= nextRun)
            {
                nextRun = nextRun.AddDays(1);     // Nếu đã qua 8h sáng thì hẹn 8h sáng mai
            }

            return nextRun - now;
        }
    }
}
