using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Infrastructure.Contexts;
using TaxKeepVN.Application.Helpers;

namespace TaxKeepVNManagementSystem.BackgroundJobs
{
    /// <summary>
    /// Background Service tự động quét và xử lý các ca tư vấn vắng mặt không phép (No-Show quá 15 phút) (Đặc tả 5)
    /// Đảm bảo tự động hoàn tiền hoặc đóng ca giải ngân, ngăn chặn tuyệt đối tình trạng treo tiền trong ví Escrow.
    /// </summary>
    public class ConsultationNoShowBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ConsultationNoShowBackgroundService> _logger;

        public ConsultationNoShowBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<ConsultationNoShowBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ConsultationNoShowBackgroundService đã khởi động.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<TaxKeepDbContext>();
                    var nowUtc = DateTimeOffset.UtcNow;

                    await ProcessNoShowBookingsAsync(dbContext, nowUtc);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi trong quá trình quét No-Show phiên tư vấn.");
                }

                // Quét định kỳ mỗi 2 phút
                await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
            }
        }

        private async Task ProcessNoShowBookingsAsync(TaxKeepDbContext dbContext, DateTimeOffset nowUtc)
        {
            // 1. Đọc ngưỡng No-Show từ bảng consultation_platform_policies (mặc định 15 phút)
            var policy = await dbContext.ConsultationPlatformPolicies
                .Where(p => p.IsActive)
                .OrderByDescending(p => p.Id)
                .FirstOrDefaultAsync();

            var noShowMinutes = policy?.SessionNoShowMinutes ?? 15;

            // 2. Tìm các ca CONFIRMED có khung giờ đã bắt đầu quá noShowMinutes
            var confirmedBookings = await dbContext.Bookings
                .Include(b => b.ExpertSlot)
                .Include(b => b.EscrowTransaction)
                .Where(b => b.Status == BookingStatus.CONFIRMED)
                .ToListAsync();

            if (!confirmedBookings.Any()) return;

            var bookingIds = confirmedBookings.Select(b => b.Id).ToList();
            var sessions = await dbContext.ConsultationSessions
                .Where(s => bookingIds.Contains(s.BookingId))
                .ToDictionaryAsync(s => s.BookingId);

            foreach (var booking in confirmedBookings)
            {
                var slot = booking.ExpertSlot;
                if (slot == null) continue;

                var slotStartUtc = BookingHelper.ConvertSlotToUtc(slot.SlotDate, slot.StartTime);
                var noShowThresholdUtc = slotStartUtc.AddMinutes(noShowMinutes);

                // Chưa quá thời hạn No-Show
                if (nowUtc < noShowThresholdUtc) continue;

                sessions.TryGetValue(booking.Id, out var session);
                var userCheckedIn = session?.UserCheckInAt.HasValue == true;
                var expertCheckedIn = session?.ExpertCheckInAt.HasValue == true;

                // Nếu cả hai bên đều đã check-in thì đã chuyển IN_PROGRESS (bỏ qua)
                if (userCheckedIn && expertCheckedIn) continue;

                if (session == null)
                {
                    session = new ConsultationSession
                    {
                        Id = Guid.NewGuid(),
                        BookingId = booking.Id,
                        RoomName = $"TaxKeepVN_{booking.BookingCode.Replace("-", "_")}",
                        RoomUrl = $"https://meet.jit.si/TaxKeepVN_{booking.BookingCode.Replace("-", "_")}",
                        CreatedAt = nowUtc,
                        UpdatedAt = nowUtc
                    };
                    await dbContext.ConsultationSessions.AddAsync(session);
                }

                session.NoShowReportedBy = "SYSTEM";
                session.NoShowReportedAt = nowUtc;
                session.EndedAt = nowUtc;
                session.UpdatedAt = nowUtc;

                // TRƯỜNG HỢP A: Chuyên gia vắng mặt (hoặc cả 2 cùng không vào) -> Hoàn tiền 100% cho khách
                if (!expertCheckedIn)
                {
                    session.NoShowType = "EXPERT_NO_SHOW";
                    session.ActualDurationMinutes = 0;
                    session.SharedNotes = "Hệ thống tự động hủy ca do Chuyên gia không có mặt sau 15 phút. Đã hoàn tiền 100% cho người nộp thuế.";

                    booking.Status = BookingStatus.CANCELLED_BY_EXPERT;
                    booking.CancelledBy = "SYSTEM_EXPERT_NO_SHOW";
                    booking.CancelledAt = nowUtc;
                    booking.CancellationReason = $"Chuyên gia không điểm danh vào phòng sau {noShowMinutes} phút kể từ giờ hẹn.";
                    booking.RefundStatus = "REFUNDED";
                    booking.RefundedAt = nowUtc;
                    booking.UpdatedAt = nowUtc;

                    if (booking.EscrowTransaction != null)
                    {
                        booking.EscrowTransaction.EscrowStatus = EscrowStatus.Refunded;
                        booking.EscrowTransaction.RefundAmount = booking.EscrowTransaction.Amount;
                        booking.EscrowTransaction.RefundReason = "Hệ thống tự động hoàn tiền do Chuyên gia vắng mặt (No-Show).";
                        booking.EscrowTransaction.RefundedAt = nowUtc;
                        booking.EscrowTransaction.UpdatedAt = nowUtc;
                    }

                    slot.IsBooked = false;
                    slot.HoldExpiresAt = null;
                    slot.HoldUserId = null;
                    slot.UpdatedAt = nowUtc;

                    _logger.LogWarning("Phát hiện EXPERT NO-SHOW ca {BookingCode}. Tự động hoàn tiền 100% cho khách hàng.", booking.BookingCode);
                }
                // TRƯỜNG HỢP B: Chuyên gia có mặt, Khách hàng vắng mặt -> Đóng ca và giải ngân cho chuyên gia
                else if (expertCheckedIn && !userCheckedIn)
                {
                    session.NoShowType = "USER_NO_SHOW";
                    session.ActualDurationMinutes = noShowMinutes;
                    session.SharedNotes = $"Hệ thống tự động đóng ca do Khách hàng không có mặt sau {noShowMinutes} phút. Bảo vệ quyền lợi thù lao của Chuyên gia.";

                    booking.Status = BookingStatus.COMPLETED;
                    booking.UpdatedAt = nowUtc;

                    if (booking.EscrowTransaction != null && booking.EscrowTransaction.EscrowStatus == EscrowStatus.HeldInEscrow)
                    {
                        booking.EscrowTransaction.EscrowStatus = EscrowStatus.CoolingDown;
                        booking.EscrowTransaction.UpdatedAt = nowUtc;
                    }

                    _logger.LogInformation("Phát hiện USER NO-SHOW ca {BookingCode}. Tự động đóng ca và giữ tiền Escrow cho chuyên gia.", booking.BookingCode);
                }
            }

            await dbContext.SaveChangesAsync();
        }
    }
}
