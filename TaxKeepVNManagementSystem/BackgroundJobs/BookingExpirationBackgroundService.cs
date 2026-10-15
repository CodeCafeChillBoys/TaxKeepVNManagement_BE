using Microsoft.EntityFrameworkCore;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Infrastructure.Contexts;

namespace TaxKeepVNManagementSystem.BackgroundJobs
{
    /// <summary>
    /// Background job định kỳ kiểm tra và giải phóng slot / booking quá hạn (Đặc tả 3)
    /// 1. Quá hạn 10 phút chờ thanh toán (Hold Slot Expiration) -> Hủy booking, mở lại slot trống
    /// 2. Quá hạn 2 giờ chuyên gia không duyệt (Approval Timeout) -> Hủy booking, kích hoạt hoàn tiền, mở lại slot trống
    /// </summary>
    public class BookingExpirationBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BookingExpirationBackgroundService> _logger;

        public BookingExpirationBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<BookingExpirationBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("BookingExpirationBackgroundService started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<TaxKeepDbContext>();
                    var now = DateTimeOffset.UtcNow;

                    await ProcessExpiredHoldBookingsAsync(dbContext, now);
                    await ProcessExpiredApprovalBookingsAsync(dbContext, now);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi xảy ra trong quá trình thực thi BookingExpirationBackgroundService.");
                }

                // Chạy kiểm tra mỗi 1 phút
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        /// <summary>
        /// Xử lý các booking PENDING_PAYMENT quá hạn giữ chỗ chờ thanh toán
        /// </summary>
        private async Task ProcessExpiredHoldBookingsAsync(TaxKeepDbContext dbContext, DateTimeOffset now)
        {
            var expiredBookings = await dbContext.Bookings
                .Include(b => b.ExpertSlot)
                .Include(b => b.EscrowTransaction)
                .Where(b => b.Status == BookingStatus.PENDING_PAYMENT && b.HoldExpiresAt <= now)
                .ToListAsync();

            if (!expiredBookings.Any()) return;

            foreach (var booking in expiredBookings)
            {
                booking.Status = BookingStatus.EXPIRED_UNPAID;
                booking.UpdatedAt = now;

                if (booking.EscrowTransaction != null && booking.EscrowTransaction.EscrowStatus == EscrowStatus.PendingPayment)
                {
                    booking.EscrowTransaction.EscrowStatus = EscrowStatus.Cancelled;
                    booking.EscrowTransaction.RefundReason = "Quá thời hạn giữ chỗ nhưng khách hàng chưa hoàn tất thanh toán.";
                    booking.EscrowTransaction.UpdatedAt = now;
                }

                if (booking.ExpertSlot != null)
                {
                    booking.ExpertSlot.IsBooked = false;
                    booking.ExpertSlot.HoldExpiresAt = null;
                    booking.ExpertSlot.HoldUserId = null;
                    booking.ExpertSlot.UpdatedAt = now;
                }

                _logger.LogInformation("Booking {BookingCode} (Id: {BookingId}) hết hạn giữ chỗ chờ thanh toán. Đã giải phóng slot {SlotId}.",
                    booking.BookingCode, booking.Id, booking.ExpertSlotId);
            }

            // Giải phóng các slot mồ côi (nếu có slot bị hold nhưng không còn booking pending nào)
            var orphanSlots = await dbContext.ExpertSlots
                .Where(s => !s.IsBooked && s.HoldExpiresAt != null && s.HoldExpiresAt <= now)
                .ToListAsync();

            foreach (var slot in orphanSlots)
            {
                slot.HoldExpiresAt = null;
                slot.HoldUserId = null;
                slot.UpdatedAt = now;
            }

            await dbContext.SaveChangesAsync();
        }

        /// <summary>
        /// Xử lý các booking AWAITING_EXPERT_APPROVAL quá hạn chuyên gia không phản hồi -> Hoàn tiền 100% từ Escrow (Đặc tả 4)
        /// </summary>
        private async Task ProcessExpiredApprovalBookingsAsync(TaxKeepDbContext dbContext, DateTimeOffset now)
        {
            var timeoutBookings = await dbContext.Bookings
                .Include(b => b.ExpertSlot)
                .Include(b => b.EscrowTransaction)
                .Where(b => b.Status == BookingStatus.AWAITING_EXPERT_APPROVAL 
                         && b.ApprovalDeadline.HasValue 
                         && b.ApprovalDeadline.Value <= now)
                .ToListAsync();

            if (!timeoutBookings.Any()) return;

            foreach (var booking in timeoutBookings)
            {
                booking.Status = BookingStatus.EXPIRED_NO_RESPONSE;
                booking.RefundStatus = "REFUNDED";
                booking.RefundedAt = now;
                booking.CancellationReason = "Chuyên gia không phản hồi yêu cầu tiếp nhận trong thời hạn quy định.";
                booking.CancelledBy = "SYSTEM";
                booking.CancelledAt = now;
                booking.UpdatedAt = now;

                if (booking.EscrowTransaction != null)
                {
                    booking.EscrowTransaction.EscrowStatus = EscrowStatus.Refunded;
                    booking.EscrowTransaction.RefundAmount = booking.EscrowTransaction.Amount;
                    booking.EscrowTransaction.RefundReason = booking.CancellationReason;
                    booking.EscrowTransaction.RefundedAt = now;
                    booking.EscrowTransaction.UpdatedAt = now;
                }

                if (booking.ExpertSlot != null)
                {
                    booking.ExpertSlot.IsBooked = false;
                    booking.ExpertSlot.HoldExpiresAt = null;
                    booking.ExpertSlot.HoldUserId = null;
                    booking.ExpertSlot.UpdatedAt = now;
                }

                await dbContext.SystemNotifications.AddAsync(new TaxKeepVN.Domain.Entities.SystemNotification
                {
                    NotificationId = Guid.NewGuid(),
                    UserId = booking.UserId,
                    Title = "Lịch hẹn quá hạn chờ chuyên gia xác nhận – Đã hoàn tiền 100%",
                    Message = $"Chuyên gia chưa kịp phản hồi lịch hẹn #{booking.BookingCode}. Hệ thống đã tự động hủy lịch và hoàn trả 100% số tiền {booking.Fee:N0} VNĐ từ ví Escrow.",
                    NotificationType = "BOOKING_APPROVAL_EXPIRED_REFUNDED",
                    IsRead = false,
                    TargetActionUrl = "/experts",
                    CreatedAt = now.UtcDateTime
                });

                _logger.LogInformation("Booking {BookingCode} (Id: {BookingId}) quá hạn phê duyệt từ chuyên gia. Đã hoàn tiền 100% từ Escrow và giải phóng slot.",
                    booking.BookingCode, booking.Id);
            }

            await dbContext.SaveChangesAsync();
        }
    }
}
