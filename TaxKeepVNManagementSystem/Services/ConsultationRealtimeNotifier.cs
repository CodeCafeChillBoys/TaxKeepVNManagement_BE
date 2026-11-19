using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using TaxKeepVN.Application.DTOs.Consultations;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVNManagementSystem.Hubs;

namespace TaxKeepVNManagementSystem.Services
{
    public class ConsultationRealtimeNotifier : IConsultationRealtimeNotifier
    {
        private readonly IHubContext<ConsultationHub> _hubContext;
        private readonly ILogger<ConsultationRealtimeNotifier> _logger;

        public ConsultationRealtimeNotifier(IHubContext<ConsultationHub> hubContext, ILogger<ConsultationRealtimeNotifier> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task NotifyParticipantJoinedAsync(Guid bookingId, Guid userId, string role, bool bothPresent)
        {
            try
            {
                await _hubContext.Clients.Group($"Booking_{bookingId}").SendAsync("ParticipantJoined", new
                {
                    BookingId = bookingId,
                    UserId = userId,
                    Role = role,
                    BothPresent = bothPresent,
                    Timestamp = DateTimeOffset.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi gửi SignalR thông báo người tham gia {BookingId}", bookingId);
            }
        }

        public async Task NotifySessionStartedAsync(Guid bookingId, DateTimeOffset startedAt)
        {
            try
            {
                await _hubContext.Clients.Group($"Booking_{bookingId}").SendAsync("SessionStarted", new
                {
                    BookingId = bookingId,
                    Status = "IN_PROGRESS",
                    StartedAt = startedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi gửi SignalR thông báo phiên bắt đầu {BookingId}", bookingId);
            }
        }

        public async Task NotifyNotesUpdatedAsync(Guid bookingId, string notes)
        {
            try
            {
                await _hubContext.Clients.Group($"Booking_{bookingId}").SendAsync("NotesUpdated", new
                {
                    BookingId = bookingId,
                    Notes = notes,
                    Timestamp = DateTimeOffset.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi gửi SignalR thông báo cập nhật ghi chú {BookingId}", bookingId);
            }
        }

        public async Task NotifySessionCompletedAsync(Guid bookingId, int actualDurationMinutes, string sharedNotes)
        {
            try
            {
                await _hubContext.Clients.Group($"Booking_{bookingId}").SendAsync("SessionCompleted", new
                {
                    BookingId = bookingId,
                    Status = "COMPLETED",
                    ActualDurationMinutes = actualDurationMinutes,
                    SharedNotes = sharedNotes,
                    EndedAt = DateTimeOffset.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi gửi SignalR thông báo kết thúc ca {BookingId}", bookingId);
            }
        }

        public async Task NotifyCountdownWarningAsync(Guid bookingId, int remainingMinutes)
        {
            try
            {
                await _hubContext.Clients.Group($"Booking_{bookingId}").SendAsync("CountdownWarning", new
                {
                    BookingId = bookingId,
                    RemainingMinutes = remainingMinutes,
                    Message = $"Phiên tư vấn sắp kết thúc trong vòng {remainingMinutes} phút. Vui lòng thống nhất kết luận."
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi gửi SignalR cảnh báo đếm ngược {BookingId}", bookingId);
            }
        }

        public async Task NotifyChatMessageReceivedAsync(Guid bookingId, ConsultationMessageDto message)
        {
            try
            {
                await _hubContext.Clients.Group($"Booking_{bookingId}").SendAsync("ReceiveChatMessage", message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi gửi SignalR tin nhắn chat mới {BookingId}", bookingId);
            }
        }

        public async Task NotifyTypingAsync(Guid bookingId, Guid userId, string role, bool isTyping)
        {
            try
            {
                await _hubContext.Clients.Group($"Booking_{bookingId}").SendAsync("UserTyping", new
                {
                    BookingId = bookingId,
                    UserId = userId,
                    Role = role,
                    IsTyping = isTyping,
                    Timestamp = DateTimeOffset.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi gửi SignalR trạng thái typing {BookingId}", bookingId);
            }
        }

        public async Task NotifyMessagesReadAsync(Guid bookingId, Guid readerId, System.Collections.Generic.List<Guid> messageIds, DateTimeOffset readAt)
        {
            try
            {
                var payload = new
                {
                    BookingId = bookingId,
                    ReaderId = readerId,
                    MessageIds = messageIds,
                    ReadAt = readAt
                };

                // Phát sự kiện MessagesRead
                await _hubContext.Clients.Group($"Booking_{bookingId}").SendAsync("MessagesRead", payload);
                // Đồng thời phát ReceiveMessagesRead để tương thích mọi naming convention của Mobile
                await _hubContext.Clients.Group($"Booking_{bookingId}").SendAsync("ReceiveMessagesRead", payload);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi gửi SignalR thông báo đã xem {BookingId}", bookingId);
            }
        }
    }
}
