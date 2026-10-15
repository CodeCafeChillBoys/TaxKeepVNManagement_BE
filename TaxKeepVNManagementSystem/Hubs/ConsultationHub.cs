using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace TaxKeepVNManagementSystem.Hubs
{
    /// <summary>
    /// SignalR Hub điều phối sự kiện thời gian thực trong phòng tư vấn trực tuyến (Đặc tả 5)
    /// Hỗ trợ thông báo hiện diện, đếm ngược thời gian và đóng phòng đồng thời khi kết ca.
    /// </summary>
    [Authorize]
    public class ConsultationHub : Hub
    {
        private readonly ILogger<ConsultationHub> _logger;

        public ConsultationHub(ILogger<ConsultationHub> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Tham gia vào nhóm phòng họp theo BookingId
        /// </summary>
        public async Task JoinConsultationRoom(string bookingId)
        {
            var userId = Context.User?.FindFirst("userId")?.Value 
                      ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var role = Context.User?.FindFirst(ClaimTypes.Role)?.Value 
                    ?? Context.User?.FindFirst("role")?.Value 
                    ?? "taxpayer";

            await Groups.AddToGroupAsync(Context.ConnectionId, $"Booking_{bookingId}");
            _logger.LogInformation("Người dùng {UserId} ({Role}) đã kết nối SignalR phòng họp {BookingId}.", userId, role, bookingId);

            await Clients.OthersInGroup($"Booking_{bookingId}").SendAsync("ParticipantConnected", new
            {
                BookingId = bookingId,
                UserId = userId,
                Role = role,
                Timestamp = DateTimeOffset.UtcNow
            });
        }

        /// <summary>
        /// Rời khỏi nhóm phòng họp
        /// </summary>
        public async Task LeaveConsultationRoom(string bookingId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Booking_{bookingId}");
        }

        /// <summary>
        /// Đồng bộ bản nháp ghi chú kết luận tức thì giữa 2 bên
        /// </summary>
        public async Task SendLiveNoteUpdate(string bookingId, string notes)
        {
            await Clients.OthersInGroup($"Booking_{bookingId}").SendAsync("NotesUpdated", new
            {
                BookingId = bookingId,
                Notes = notes,
                Timestamp = DateTimeOffset.UtcNow
            });
        }
    }
}
