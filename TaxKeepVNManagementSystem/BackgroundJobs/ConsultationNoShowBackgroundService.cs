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
                    .ThenInclude(s => s!.ExpertProfile)
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

                        // Tự động hoàn tiền vào ví người dùng
                        var userWallet = await dbContext.UserWallets.FirstOrDefaultAsync(w => w.UserId == booking.UserId && w.WalletType == "USER");
                        if (userWallet == null)
                        {
                            userWallet = new UserWallet
                            {
                                Id = Guid.NewGuid(),
                                UserId = booking.UserId,
                                WalletType = "USER",
                                AvailableBalance = 0m,
                                CreatedAt = nowUtc,
                                UpdatedAt = nowUtc
                            };
                            await dbContext.UserWallets.AddAsync(userWallet);
                        }
                        var beforeBal = userWallet.AvailableBalance;
                        userWallet.AvailableBalance += booking.EscrowTransaction.Amount;
                        userWallet.UpdatedAt = nowUtc;

                        await dbContext.WalletTransactions.AddAsync(new WalletTransaction
                        {
                            Id = Guid.NewGuid(),
                            WalletId = userWallet.Id,
                            UserId = booking.UserId,
                            Amount = booking.EscrowTransaction.Amount,
                            BalanceBefore = beforeBal,
                            BalanceAfter = userWallet.AvailableBalance,
                            TransactionType = "REFUND",
                            ReferenceId = booking.Id,
                            Description = $"Hoàn tiền ca tư vấn #{booking.BookingCode} (Chuyên gia vắng mặt)",
                            CreatedAt = nowUtc
                        });
                    }

                    slot.IsBooked = false;
                    slot.HoldExpiresAt = null;
                    slot.HoldUserId = null;
                    slot.UpdatedAt = nowUtc;

                    // Gửi thông báo hoàn tiền cho Khách hàng
                    await dbContext.SystemNotifications.AddAsync(new SystemNotification
                    {
                        NotificationId = Guid.NewGuid(),
                        UserId = booking.UserId,
                        Title = "Hoàn tiền do Chuyên gia vắng mặt",
                        Message = $"Chuyên gia đã không có mặt trong ca tư vấn #{booking.BookingCode} sau {noShowMinutes} phút. Hệ thống đã tự động hủy ca và hoàn trả 100% tiền cọc ({(booking.EscrowTransaction?.Amount ?? booking.Fee):N0} đ) vào ví của bạn.",
                        NotificationType = "EXPERT_NO_SHOW_REFUND",
                        IsRead = false,
                        TargetActionUrl = $"/consultation/bookings/{booking.Id}",
                        CreatedAt = nowUtc.DateTime
                    });

                    // Gửi cảnh báo vi phạm cho Chuyên gia
                    if (slot.ExpertProfile != null)
                    {
                        await dbContext.SystemNotifications.AddAsync(new SystemNotification
                        {
                            NotificationId = Guid.NewGuid(),
                            UserId = slot.ExpertProfile.UserId,
                            Title = "Cảnh báo vắng mặt phiên tư vấn đã nhận",
                            Message = $"Bạn đã không tham gia ca tư vấn #{booking.BookingCode} sau {noShowMinutes} phút kể từ giờ hẹn. Hệ thống đã tự động hủy ca và hoàn tiền 100% cho khách hàng.",
                            NotificationType = "EXPERT_NO_SHOW_PENALTY",
                            IsRead = false,
                            TargetActionUrl = $"/expert/bookings/{booking.Id}",
                            CreatedAt = nowUtc.DateTime
                        });
                    }

                    _logger.LogWarning("Phát hiện EXPERT NO-SHOW ca {BookingCode}. Tự động hoàn tiền 100% cho khách hàng và gửi thông báo.", booking.BookingCode);
                }
                // TRƯỜNG HỢP B: Chuyên gia có mặt, Khách hàng vắng mặt -> Đóng ca và giải ngân cho chuyên gia
                else if (expertCheckedIn && !userCheckedIn)
                {
                    session.NoShowType = "USER_NO_SHOW";
                    session.ActualDurationMinutes = noShowMinutes;
                    session.SharedNotes = $"Hệ thống tự động đóng ca do Khách hàng không có mặt sau {noShowMinutes} phút. Bảo vệ quyền lợi thù lao của Chuyên gia.";

                    booking.Status = BookingStatus.COMPLETED;
                    booking.CompletedAt = nowUtc;
                    booking.ReviewDeadline = nowUtc.AddDays(7);
                    booking.UpdatedAt = nowUtc;

                    if (booking.EscrowTransaction != null && booking.EscrowTransaction.EscrowStatus == EscrowStatus.HeldInEscrow)
                    {
                        booking.EscrowTransaction.EscrowStatus = EscrowStatus.CoolingDown;
                        booking.EscrowTransaction.UpdatedAt = nowUtc;
                    }

                    // Gửi thông báo vắng mặt cho Khách hàng
                    await dbContext.SystemNotifications.AddAsync(new SystemNotification
                    {
                        NotificationId = Guid.NewGuid(),
                        UserId = booking.UserId,
                        Title = "Thông báo vắng mặt phiên tư vấn trực tuyến",
                        Message = $"Bạn đã không tham gia phiên tư vấn #{booking.BookingCode} (vắng mặt quá {noShowMinutes} phút kể từ giờ hẹn). Ca tư vấn đã tự động kết thúc và thù lao được quyết toán cho Chuyên gia theo chính sách bảo vệ thời gian của Chuyên gia.",
                        NotificationType = "USER_NO_SHOW",
                        IsRead = false,
                        TargetActionUrl = $"/consultation/bookings/{booking.Id}",
                        CreatedAt = nowUtc.DateTime
                    });

                    // Gửi thông báo cho Chuyên gia biết thù lao đã được bảo vệ
                    if (slot.ExpertProfile != null)
                    {
                        await dbContext.SystemNotifications.AddAsync(new SystemNotification
                        {
                            NotificationId = Guid.NewGuid(),
                            UserId = slot.ExpertProfile.UserId,
                            Title = "Khách hàng vắng mặt phiên tư vấn",
                            Message = $"Khách hàng đã không có mặt trong ca tư vấn #{booking.BookingCode} sau {noShowMinutes} phút. Hệ thống đã tự động đóng ca và bảo vệ thù lao của bạn trong trạng thái chờ giải ngân.",
                            NotificationType = "USER_NO_SHOW_EXPERT_NOTIFIED",
                            IsRead = false,
                            TargetActionUrl = $"/expert/bookings/{booking.Id}",
                            CreatedAt = nowUtc.DateTime
                        });
                    }

                    _logger.LogInformation("Phát hiện USER NO-SHOW ca {BookingCode}. Tự động đóng ca, giữ tiền Escrow cho chuyên gia và gửi thông báo.", booking.BookingCode);
                }
            }

            await dbContext.SaveChangesAsync();
        }
    }
}
