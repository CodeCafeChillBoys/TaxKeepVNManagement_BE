using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVNManagementSystem.Hubs
{
    /// <summary>
    /// SignalR Hub điều phối sự kiện thời gian thực trong phòng tư vấn trực tuyến (Đặc tả 5).
    /// BẢO MẬT: Kiểm soát quyền thành viên (Taxpayer / Expert / Admin) trước khi cấp phép vào phòng.
    /// Hỗ trợ thông báo hiện diện, đếm ngược thời gian, nhắn tin, trạng thái đã đọc và typing realtime.
    /// </summary>
    [Authorize]
    public class ConsultationHub : Hub
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ConsultationHub> _logger;

        public ConsultationHub(IUnitOfWork unitOfWork, ILogger<ConsultationHub> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        /// <summary>
        /// Tham gia vào nhóm phòng họp theo BookingId (Kiểm tra bảo mật nghiêm ngặt danh tính người gọi)
        /// </summary>
        public async Task JoinConsultationRoom(string bookingId)
        {
            await JoinRoomInternalAsync(bookingId);
        }

        /// <summary>
        /// Alias tương thích cho Mobile / Frontend khi gọi JoinBookingRoom
        /// </summary>
        public async Task JoinBookingRoom(string bookingId)
        {
            await JoinRoomInternalAsync(bookingId);
        }

        /// <summary>
        /// Rời khỏi nhóm phòng họp
        /// </summary>
        public async Task LeaveConsultationRoom(string bookingId)
        {
            await LeaveRoomInternalAsync(bookingId);
        }

        /// <summary>
        /// Alias tương thích cho Mobile / Frontend khi gọi LeaveBookingRoom
        /// </summary>
        public async Task LeaveBookingRoom(string bookingId)
        {
            await LeaveRoomInternalAsync(bookingId);
        }

        /// <summary>
        /// Đồng bộ bản nháp ghi chú kết luận tức thì giữa 2 bên
        /// </summary>
        public async Task SendLiveNoteUpdate(string bookingId, string notes)
        {
            if (!Guid.TryParse(bookingId, out var parsedBookingId)) return;

            var (isValid, _, _) = await VerifyMembershipAsync(parsedBookingId);
            if (!isValid) return;

            await Clients.OthersInGroup($"Booking_{parsedBookingId}").SendAsync("NotesUpdated", new
            {
                BookingId = parsedBookingId,
                Notes = notes,
                Timestamp = DateTimeOffset.UtcNow
            });
        }

        /// <summary>
        /// Bắn tín hiệu đang gõ tin nhắn (Typing Indicator)
        /// </summary>
        public async Task SendTypingIndicator(string bookingId, bool isTyping)
        {
            if (!Guid.TryParse(bookingId, out var parsedBookingId)) return;

            var (isValid, currentUserId, role) = await VerifyMembershipAsync(parsedBookingId);
            if (!isValid) return;

            await Clients.OthersInGroup($"Booking_{parsedBookingId}").SendAsync("UserTyping", new
            {
                BookingId = parsedBookingId,
                UserId = currentUserId,
                Role = role,
                IsTyping = isTyping,
                Timestamp = DateTimeOffset.UtcNow
            });
        }

        /// <summary>
        /// Đánh dấu đã đọc tin nhắn trực tiếp qua SignalR Hub
        /// </summary>
        public async Task MarkMessagesRead(string bookingId)
        {
            if (!Guid.TryParse(bookingId, out var parsedBookingId)) return;

            var (isValid, currentUserId, _) = await VerifyMembershipAsync(parsedBookingId);
            if (!isValid) return;

            var unreadMessages = (await _unitOfWork.Repository<ConsultationMessage>()
                .FindAsync(m => m.BookingId == parsedBookingId && m.SenderId != currentUserId && !m.IsRead))
                .ToList();

            if (unreadMessages.Any())
            {
                var readAt = DateTimeOffset.UtcNow;
                var readIds = new List<Guid>();
                foreach (var msg in unreadMessages)
                {
                    msg.IsRead = true;
                    readIds.Add(msg.Id);
                    _unitOfWork.Repository<ConsultationMessage>().Update(msg);
                }
                await _unitOfWork.SaveChangesAsync();

                var payload = new
                {
                    BookingId = parsedBookingId,
                    ReaderId = currentUserId,
                    MessageIds = readIds,
                    ReadAt = readAt
                };

                await Clients.OthersInGroup($"Booking_{parsedBookingId}").SendAsync("MessagesRead", payload);
                await Clients.OthersInGroup($"Booking_{parsedBookingId}").SendAsync("ReceiveMessagesRead", payload);
            }
        }

        // ── Helper nghiệp vụ & bảo mật ───────────────────────────────────────────

        private async Task JoinRoomInternalAsync(string bookingId)
        {
            if (string.IsNullOrWhiteSpace(bookingId) || !Guid.TryParse(bookingId, out var parsedBookingId))
            {
                throw new HubException("Mã ca tư vấn (bookingId) không đúng định dạng GUID.");
            }

            var (isValid, currentUserId, role) = await VerifyMembershipAsync(parsedBookingId);
            if (!isValid)
            {
                _logger.LogWarning("CẢNH BÁO BẢO MẬT: Người dùng {UserId} ({Role}) không thuộc ca tư vấn {BookingId} nhưng cố tình tham gia nghe lén.", 
                    currentUserId, role, parsedBookingId);
                throw new HubException("BẢO MẬT: Bạn không có quyền tham gia vào phòng chat của ca tư vấn này.");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"Booking_{parsedBookingId}");
            _logger.LogInformation("Người dùng {UserId} ({Role}) đã xác thực bảo mật thành công và vào phòng {BookingId}.", 
                currentUserId, role, parsedBookingId);

            await Clients.OthersInGroup($"Booking_{parsedBookingId}").SendAsync("ParticipantConnected", new
            {
                BookingId = parsedBookingId,
                UserId = currentUserId,
                Role = role,
                Timestamp = DateTimeOffset.UtcNow
            });
        }

        private async Task LeaveRoomInternalAsync(string bookingId)
        {
            if (Guid.TryParse(bookingId, out var parsedBookingId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Booking_{parsedBookingId}");
            }
        }

        private async Task<(bool isValid, Guid userId, string role)> VerifyMembershipAsync(Guid bookingId)
        {
            var userIdStr = Context.User?.FindFirst("userId")?.Value 
                         ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                         ?? Context.UserIdentifier;

            if (string.IsNullOrWhiteSpace(userIdStr) || !Guid.TryParse(userIdStr, out var currentUserId))
            {
                return (false, Guid.Empty, "unknown");
            }

            var tokenRole = Context.User?.FindFirst(ClaimTypes.Role)?.Value 
                         ?? Context.User?.FindFirst("role")?.Value 
                         ?? "taxpayer";

            var booking = await _unitOfWork.Repository<Booking>().GetByIdAsync(bookingId);
            if (booking == null)
            {
                return (false, currentUserId, tokenRole);
            }

            var slot = await _unitOfWork.Repository<ExpertSlot>().GetByIdAsync(booking.ExpertSlotId);
            var profile = slot != null ? await _unitOfWork.Repository<ExpertProfile>().GetByIdAsync(slot.ExpertProfileId) : null;

            bool isTaxpayer = booking.UserId == currentUserId;
            bool isExpert = profile != null && profile.UserId == currentUserId;
            bool isAdmin = string.Equals(tokenRole, "admin", StringComparison.OrdinalIgnoreCase);

            if (!isTaxpayer && !isExpert && !isAdmin)
            {
                return (false, currentUserId, tokenRole);
            }

            var actualRole = isExpert ? "expert" : (isTaxpayer ? "taxpayer" : tokenRole);
            return (true, currentUserId, actualRole);
        }
    }
}
