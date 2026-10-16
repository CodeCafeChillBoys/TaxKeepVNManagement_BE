using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TaxKeepVN.Application.Helpers;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Infrastructure.Contexts;
using TaxKeepVNManagementSystem.Hubs;

namespace TaxKeepVNManagementSystem.BackgroundJobs
{
    /// <summary>
    /// Background Service tự động quét và gửi thông báo nhắc nhở lịch meeting trực tuyến (Đặc tả 5)
    /// Gửi nhắc nhở cho cả Người nộp thuế (Khách hàng) và Chuyên gia tư vấn tại các mốc:
    /// 1. Trước 24 giờ (1 ngày trước ngày hẹn)
    /// 2. Trước 1 giờ trước khi bắt đầu
    /// 3. Khi phòng họp trực tuyến mở (trước X phút theo SESSION_OPEN_BEFORE_MINUTES)
    /// Hỗ trợ lưu thông báo In-App (SystemNotification) và bắn sự kiện thời gian thực SignalR (ConsultationHub).
    /// </summary>
    public class ConsultationMeetingReminderJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ConsultationMeetingReminderJob> _logger;
        private readonly IHubContext<ConsultationHub> _hubContext;

        public const string NotificationTypeReminder24H = "MEETING_REMINDER_24H";
        public const string NotificationTypeReminder1H = "MEETING_REMINDER_1H";
        public const string NotificationTypeRoomOpened = "MEETING_ROOM_OPENED";

        public ConsultationMeetingReminderJob(
            IServiceProvider serviceProvider,
            ILogger<ConsultationMeetingReminderJob> logger,
            IHubContext<ConsultationHub> hubContext)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _hubContext = hubContext;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ConsultationMeetingReminderJob đã khởi động. Định kỳ quét lịch meeting mỗi 1 phút.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<TaxKeepDbContext>();
                    var nowUtc = DateTimeOffset.UtcNow;

                    await ProcessMeetingRemindersAsync(dbContext, nowUtc, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi xảy ra trong quá trình quét thông báo nhắc lịch meeting.");
                }

                // Quét định kỳ mỗi 1 phút để không bỏ lỡ mốc mở phòng (10-15 phút) hoặc mốc 1 giờ
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task ProcessMeetingRemindersAsync(TaxKeepDbContext dbContext, DateTimeOffset nowUtc, CancellationToken stoppingToken)
        {
            // 1. Đọc chính sách sàn để lấy cấu hình thời gian mở phòng trước giờ hẹn
            var policy = await dbContext.ConsultationPlatformPolicies
                .Where(p => p.IsActive)
                .OrderByDescending(p => p.Id)
                .FirstOrDefaultAsync(stoppingToken);

            int sessionOpenBeforeMinutes = policy?.SessionOpenBeforeMinutes ?? 10;

            // 2. Lấy các ca tư vấn đang ở trạng thái CONFIRMED và có lịch hẹn cụ thể
            var bookings = await dbContext.Bookings
                .Include(b => b.User)
                .Include(b => b.ExpertSlot)
                    .ThenInclude(s => s!.ExpertProfile)
                        .ThenInclude(ep => ep!.User)
                .Where(b => (b.Status == BookingStatus.CONFIRMED || b.Status == BookingStatus.IN_PROGRESS) && b.ExpertSlot != null)
                .ToListAsync(stoppingToken);

            if (!bookings.Any()) return;

            // 3. Đọc danh sách các thông báo nhắc meeting đã gửi để đối soát chống gửi trùng (Deduplication)
            var reminderTypes = new[] { NotificationTypeReminder24H, NotificationTypeReminder1H, NotificationTypeRoomOpened };
            var existingNotifs = await dbContext.SystemNotifications
                .Where(n => reminderTypes.Contains(n.NotificationType))
                .ToListAsync(stoppingToken);

            bool hasChanges = false;

            foreach (var booking in bookings)
            {
                var slot = booking.ExpertSlot;
                if (slot == null || slot.ExpertProfile == null) continue;

                var expertUser = slot.ExpertProfile.User;
                var taxpayerUser = booking.User;

                Guid taxpayerUserId = booking.UserId;
                Guid expertUserId = slot.ExpertProfile.UserId;

                // Tính thời điểm bắt đầu và kết thúc theo UTC
                var slotStartUtc = BookingHelper.ConvertSlotToUtc(slot.SlotDate, slot.StartTime);
                var slotEndUtc = BookingHelper.ConvertSlotToUtc(slot.SlotDate, slot.EndTime);

                // Ca đã kết thúc qua thời gian slot -> bỏ qua
                if (nowUtc > slotEndUtc) continue;

                // Khoảng cách thời gian còn lại đến giờ bắt đầu (tính theo phút)
                var diffMinutes = (slotStartUtc - nowUtc).TotalMinutes;

                string taxpayerName = taxpayerUser?.FullName ?? "Quý khách";
                string expertName = expertUser?.FullName ?? slot.ExpertProfile.JobTitle ?? "Chuyên gia";
                string topicTitle = string.IsNullOrWhiteSpace(booking.TopicTitle) ? "Tư vấn thuế TNCN" : booking.TopicTitle;
                string timeStr = $"{slot.StartTime:HH\\:mm} - {slot.EndTime:HH\\:mm} ngày {slot.SlotDate:dd/MM/yyyy}";
                string targetUrl = $"/consultations/{booking.Id}";

                // ── MỐC 1: NHẮC TRƯỚC 24 GIỜ (Trong khoảng từ 60 phút đến 1440 phút trước giờ hẹn) ──
                if (diffMinutes <= 1440 && diffMinutes > 60)
                {
                    bool sent = await CheckAndSendReminderAsync(
                        dbContext, existingNotifs, NotificationTypeReminder24H, booking, targetUrl, nowUtc,
                        taxpayerUserId,
                        $"Nhắc lịch hẹn tư vấn thuế ngày mai (Mã: {booking.BookingCode})",
                        $"Bạn có buổi tư vấn trực tuyến với Chuyên gia {expertName} về \"{topicTitle}\" vào lúc {timeStr}. Vui lòng chuẩn bị sẵn các câu hỏi và chứng từ liên quan.",
                        expertUserId,
                        $"Nhắc lịch tư vấn với khách hàng ngày mai (Mã: {booking.BookingCode})",
                        $"Bạn có ca tư vấn với khách hàng {taxpayerName} về \"{topicTitle}\" vào lúc {timeStr}. Vui lòng sắp xếp thời gian tham gia.");

                    if (sent) hasChanges = true;
                }

                // ── MỐC 2: NHẮC TRƯỚC 1 GIỜ (Trong khoảng từ sessionOpenBeforeMinutes đến 60 phút trước giờ hẹn) ──
                if (diffMinutes <= 60 && diffMinutes > sessionOpenBeforeMinutes)
                {
                    bool sent = await CheckAndSendReminderAsync(
                        dbContext, existingNotifs, NotificationTypeReminder1H, booking, targetUrl, nowUtc,
                        taxpayerUserId,
                        $"Lịch hẹn tư vấn thuế sẽ bắt đầu sau 1 giờ (Mã: {booking.BookingCode})",
                        $"Buổi tư vấn trực tuyến với Chuyên gia {expertName} về \"{topicTitle}\" sẽ diễn ra lúc {slot.StartTime:HH\\:mm} hôm nay. Vui lòng kiểm tra thiết bị và đường truyền mạng.",
                        expertUserId,
                        $"Ca tư vấn với khách hàng sẽ bắt đầu sau 1 giờ (Mã: {booking.BookingCode})",
                        $"Ca tư vấn với khách hàng {taxpayerName} về \"{topicTitle}\" sẽ diễn ra lúc {slot.StartTime:HH\\:mm} hôm nay. Chúc bạn có buổi tư vấn hiệu quả.");

                    if (sent) hasChanges = true;
                }

                // ── MỐC 3: PHÒNG HỌP ĐÃ MỞ (Từ khi mở phòng trước sessionOpenBeforeMinutes phút cho tới khi hết ca) ──
                if (diffMinutes <= sessionOpenBeforeMinutes && nowUtc <= slotEndUtc)
                {
                    bool sent = await CheckAndSendReminderAsync(
                        dbContext, existingNotifs, NotificationTypeRoomOpened, booking, targetUrl, nowUtc,
                        taxpayerUserId,
                        $"Phòng tư vấn trực tuyến đã mở – Sẵn sàng tham gia! (Mã: {booking.BookingCode})",
                        $"Phòng họp cho ca tư vấn #{booking.BookingCode} với Chuyên gia {expertName} đã mở trước {sessionOpenBeforeMinutes} phút. Bạn có thể nhấn vào đây để vào phòng kiểm tra mic và camera.",
                        expertUserId,
                        $"Phòng tư vấn trực tuyến đã mở – Mời Chuyên gia vào phòng! (Mã: {booking.BookingCode})",
                        $"Phòng họp cho ca tư vấn #{booking.BookingCode} với khách hàng {taxpayerName} đã mở. Vui lòng vào phòng sớm để tiếp đón khách hàng.");

                    if (sent) hasChanges = true;
                }
            }

            if (hasChanges)
            {
                await dbContext.SaveChangesAsync(stoppingToken);
            }
        }

        private async Task<bool> CheckAndSendReminderAsync(
            TaxKeepDbContext dbContext,
            List<SystemNotification> existingNotifs,
            string notificationType,
            Booking booking,
            string targetUrl,
            DateTimeOffset nowUtc,
            Guid taxpayerUserId,
            string taxpayerTitle,
            string taxpayerMessage,
            Guid expertUserId,
            string expertTitle,
            string expertMessage)
        {
            bool anySent = false;

            // 1. Gửi thông báo cho Taxpayer (Người nộp thuế)
            bool taxpayerAlreadyNotified = existingNotifs.Any(n =>
                n.UserId == taxpayerUserId &&
                n.NotificationType == notificationType &&
                n.TargetActionUrl == targetUrl);

            if (!taxpayerAlreadyNotified)
            {
                var notif = new SystemNotification
                {
                    NotificationId = Guid.NewGuid(),
                    UserId = taxpayerUserId,
                    Title = taxpayerTitle,
                    Message = taxpayerMessage,
                    NotificationType = notificationType,
                    IsRead = false,
                    TargetActionUrl = targetUrl,
                    CreatedAt = nowUtc.UtcDateTime
                };

                await dbContext.SystemNotifications.AddAsync(notif);
                existingNotifs.Add(notif);
                anySent = true;

                // Bắn realtime SignalR
                try
                {
                    await _hubContext.Clients.User(taxpayerUserId.ToString()).SendAsync("ReceiveNotification", new
                    {
                        type = notificationType,
                        title = taxpayerTitle,
                        message = taxpayerMessage,
                        bookingId = booking.Id,
                        bookingCode = booking.BookingCode,
                        targetUrl
                    });

                    await _hubContext.Clients.Group($"Booking_{booking.Id}").SendAsync("MeetingReminder", new
                    {
                        type = notificationType,
                        recipientRole = "TAXPAYER",
                        title = taxpayerTitle,
                        message = taxpayerMessage,
                        bookingId = booking.Id
                    });
                }
                catch
                {
                    // SignalR bắn realtime theo best-effort, không làm hỏng lưu trữ notification
                }

                _logger.LogInformation("Đã gửi thông báo nhắc [{Type}] ca {BookingCode} cho Khách hàng {UserId}.",
                    notificationType, booking.BookingCode, taxpayerUserId);
            }

            // 2. Gửi thông báo cho Expert (Chuyên gia)
            bool expertAlreadyNotified = existingNotifs.Any(n =>
                n.UserId == expertUserId &&
                n.NotificationType == notificationType &&
                n.TargetActionUrl == targetUrl);

            if (!expertAlreadyNotified)
            {
                var notif = new SystemNotification
                {
                    NotificationId = Guid.NewGuid(),
                    UserId = expertUserId,
                    Title = expertTitle,
                    Message = expertMessage,
                    NotificationType = notificationType,
                    IsRead = false,
                    TargetActionUrl = targetUrl,
                    CreatedAt = nowUtc.UtcDateTime
                };

                await dbContext.SystemNotifications.AddAsync(notif);
                existingNotifs.Add(notif);
                anySent = true;

                // Bắn realtime SignalR
                try
                {
                    await _hubContext.Clients.User(expertUserId.ToString()).SendAsync("ReceiveNotification", new
                    {
                        type = notificationType,
                        title = expertTitle,
                        message = expertMessage,
                        bookingId = booking.Id,
                        bookingCode = booking.BookingCode,
                        targetUrl
                    });

                    await _hubContext.Clients.Group($"Booking_{booking.Id}").SendAsync("MeetingReminder", new
                    {
                        type = notificationType,
                        recipientRole = "EXPERT",
                        title = expertTitle,
                        message = expertMessage,
                        bookingId = booking.Id
                    });
                }
                catch
                {
                    // SignalR bắn realtime theo best-effort
                }

                _logger.LogInformation("Đã gửi thông báo nhắc [{Type}] ca {BookingCode} cho Chuyên gia {UserId}.",
                    notificationType, booking.BookingCode, expertUserId);
            }

            return anySent;
        }
    }
}
