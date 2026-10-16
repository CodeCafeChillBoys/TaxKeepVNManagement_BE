using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TaxKeepVN.Application.Helpers;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Infrastructure.Contexts;

namespace TaxKeepVNManagementSystem.BackgroundJobs
{
    /// <summary>
    /// Background Service tự động quét và giải ngân thù lao cho chuyên gia sau khi hết 24 giờ Cool-down (Đặc tả 7 - Mục 5)
    /// Đảm bảo tính minh bạch, tự động quyết toán khi không phát sinh khiếu nại tranh chấp.
    /// </summary>
    public class EscrowCooldownDisbursementBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<EscrowCooldownDisbursementBackgroundService> _logger;

        public EscrowCooldownDisbursementBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<EscrowCooldownDisbursementBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("EscrowCooldownDisbursementBackgroundService đã khởi động.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<TaxKeepDbContext>();
                    var disbursementService = scope.ServiceProvider.GetRequiredService<IDisbursementDisputeService>();
                    var nowUtc = DateTimeOffset.UtcNow;

                    await ProcessCoolDownBookingsAsync(dbContext, disbursementService, nowUtc);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi trong quá trình quét giải ngân Escrow Cool-down tự động.");
                }

                // Quét định kỳ mỗi 5 phút
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        private async Task ProcessCoolDownBookingsAsync(
            TaxKeepDbContext dbContext,
            IDisbursementDisputeService disbursementService,
            DateTimeOffset nowUtc)
        {
            // 1. Đọc ngưỡng thời gian Cool-down từ bảng consultation_platform_policies hoặc system_configs (mặc định 24 giờ)
            var policy = await dbContext.ConsultationPlatformPolicies
                .AsNoTracking()
                .Where(p => p.IsActive)
                .OrderByDescending(p => p.Id)
                .FirstOrDefaultAsync();

            int coolDownHours = (policy != null && policy.EscrowCooldownHours > 0)
                ? policy.EscrowCooldownHours
                : 24;

            if (coolDownHours == 24)
            {
                var config = await dbContext.SystemConfigs
                    .AsNoTracking()
                    .Where(c => c.ConfigKey == BookingHelper.ConfigKeyEscrowCooldownHours && c.IsActive)
                    .OrderByDescending(c => c.AppliesFromYear ?? int.MinValue)
                    .FirstOrDefaultAsync();

                if (config != null && int.TryParse(config.ConfigValue, out var val) && val > 0)
                {
                    coolDownHours = val;
                }
            }

            var thresholdUtc = nowUtc.AddHours(-coolDownHours);

            // 2. Tìm các Booking hoàn thành (COMPLETED) đã vượt quá 24h kể từ khi kết thúc
            var candidateBookings = await dbContext.Bookings
                .AsNoTracking()
                .Where(b => b.Status == BookingStatus.COMPLETED &&
                            ((b.CompletedAt.HasValue && b.CompletedAt.Value <= thresholdUtc) ||
                             (!b.CompletedAt.HasValue && b.UpdatedAt <= thresholdUtc)))
                .Select(b => b.Id)
                .ToListAsync();

            if (!candidateBookings.Any())
            {
                return;
            }

            foreach (var bookingId in candidateBookings)
            {
                try
                {
                    var settled = await disbursementService.ProcessCoolDownDisbursementAsync(bookingId);
                    if (settled)
                    {
                        _logger.LogInformation("Đã tự động quyết toán giải ngân Escrow cho booking {BookingId}.", bookingId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi xử lý giải ngân tự động cho booking {BookingId}.", bookingId);
                }
            }
        }
    }
}
