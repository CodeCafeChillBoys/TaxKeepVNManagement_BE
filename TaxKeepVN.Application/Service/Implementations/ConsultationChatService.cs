using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Consultations;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Helpers;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    /// <summary>
    /// Service xử lý trò chuyện trực tiếp (Live Chat) giữa Khách hàng và Chuyên gia trong ca tư vấn (Đặc tả 5 & Flow 4)
    /// Tuân thủ: Zero-Hardcode (Toàn bộ tham số cấu hình: độ dài tin nhắn, tệp đính kèm, phân trang, URL phòng họp, thời gian mở phòng
    /// đều được cấu hình động bởi Admin trong CSDL qua ConsultationPlatformPolicy và SystemConfig), Zero-Mock Data và API Guidelines.
    /// </summary>
    public class ConsultationChatService : IConsultationChatService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConsultationRealtimeNotifier? _realtimeNotifier;
        private readonly ILogger<ConsultationChatService> _logger;

        public ConsultationChatService(
            IUnitOfWork unitOfWork,
            ILogger<ConsultationChatService> logger,
            IConsultationRealtimeNotifier? realtimeNotifier = null)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _realtimeNotifier = realtimeNotifier;
        }

        public async Task<ConsultationMessageDto> SendMessageAsync(
            Guid senderId,
            string senderRole,
            Guid bookingId,
            SendConsultationMessageRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Content))
            {
                throw new BadRequestException("INVALID_CONTENT", "Nội dung tin nhắn không được để trống.");
            }

            // Đọc cấu hình động do Admin quản trị (Zero-Hardcode)
            var policy = await GetChatEffectivePolicyAsync();

            var maxContentLength = policy.ChatMessageMaxLength > 0 ? policy.ChatMessageMaxLength : 4000;
            if (request.Content.Length > maxContentLength)
            {
                throw new BadRequestException("CONTENT_TOO_LONG", 
                    $"Nội dung tin nhắn không vượt quá {maxContentLength} ký tự theo cấu hình hệ thống.");
            }

            var maxUrlLength = policy.ChatMaxAttachmentUrlLength > 0 ? policy.ChatMaxAttachmentUrlLength : 1000;
            if (!string.IsNullOrWhiteSpace(request.AttachmentUrl) && request.AttachmentUrl.Length > maxUrlLength)
            {
                throw new BadRequestException("ATTACHMENT_URL_TOO_LONG", 
                    $"Đường dẫn tệp đính kèm không vượt quá {maxUrlLength} ký tự.");
            }

            if (!string.IsNullOrWhiteSpace(request.AttachmentType))
            {
                var allowedTypesRaw = policy.ChatAllowedAttachmentTypes ?? "IMAGE,PDF,DOCUMENT";
                var allowedList = allowedTypesRaw
                    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (!allowedList.Contains(request.AttachmentType.Trim(), StringComparer.OrdinalIgnoreCase))
                {
                    throw new BadRequestException("INVALID_ATTACHMENT_TYPE",
                        $"Loại tệp đính kèm '{request.AttachmentType}' không hợp lệ. Hệ thống chỉ chấp nhận: {allowedTypesRaw}.");
                }
            }

            var (booking, slot, profile, isTaxpayer, isExpert) = await ValidateBookingAndMembershipAsync(bookingId, senderId, senderRole);

            // Kiểm tra trạng thái Booking: chỉ cho phép gửi tin nhắn khi ca đã xác nhận hoặc đang diễn ra
            if (booking.Status != BookingStatus.CONFIRMED &&
                booking.Status != BookingStatus.IN_PROGRESS)
            {
                throw new BadRequestException("INVALID_STATUS",
                    $"Không thể gửi tin nhắn khi lịch hẹn đang ở trạng thái '{booking.Status}'. Phòng chat chỉ mở khi lịch hẹn đã xác nhận (CONFIRMED) hoặc đang diễn ra (IN_PROGRESS).");
            }

            var nowUtc = DateTimeOffset.UtcNow;

            var slotStartUtc = BookingHelper.ConvertSlotToUtc(slot.SlotDate, slot.StartTime);
            if (nowUtc >= slotStartUtc)
            {
                // Tin nhắn trước giờ hẹn không được tính là check-in cho phiên tư vấn.
                await SynchronizeAttendanceOnMessageAsync(booking, isTaxpayer, isExpert, nowUtc, policy);
            }

            // Lấy thông tin người gửi
            string senderName = "Người dùng";
            string? senderAvatarUrl = null;
            string actualRole = isExpert ? "expert" : "taxpayer";

            if (isExpert)
            {
                var expertUser = await _unitOfWork.Repository<User>().GetByIdAsync(profile.UserId);
                senderName = expertUser?.FullName ?? profile.JobTitle;
                var latestApp = await _unitOfWork.Repository<ExpertApplication>().GetByIdAsync(profile.LatestApplicationId);
                senderAvatarUrl = latestApp?.AvatarUrl;
            }
            else
            {
                var taxpayerUser = await _unitOfWork.Repository<User>().GetByIdAsync(booking.UserId);
                senderName = taxpayerUser?.FullName ?? "Khách hàng";
            }

            var messageEntity = new ConsultationMessage
            {
                Id = Guid.NewGuid(),
                BookingId = bookingId,
                SenderId = senderId,
                SenderRole = actualRole,
                SenderName = senderName,
                SenderAvatarUrl = senderAvatarUrl,
                Content = request.Content.Trim(),
                AttachmentUrl = string.IsNullOrWhiteSpace(request.AttachmentUrl) ? null : request.AttachmentUrl.Trim(),
                AttachmentType = string.IsNullOrWhiteSpace(request.AttachmentType) ? null : request.AttachmentType.Trim(),
                IsRead = false,
                CreatedAt = nowUtc
            };

            await _unitOfWork.Repository<ConsultationMessage>().AddAsync(messageEntity);
            await _unitOfWork.SaveChangesAsync();

            var messageDto = new ConsultationMessageDto
            {
                Id = messageEntity.Id,
                BookingId = messageEntity.BookingId,
                SenderId = messageEntity.SenderId,
                SenderRole = messageEntity.SenderRole,
                SenderName = messageEntity.SenderName,
                SenderAvatarUrl = messageEntity.SenderAvatarUrl,
                Content = messageEntity.Content,
                AttachmentUrl = messageEntity.AttachmentUrl,
                AttachmentType = messageEntity.AttachmentType,
                IsRead = messageEntity.IsRead,
                CreatedAt = messageEntity.CreatedAt,
                IsMine = true
            };

            // Bắn SignalR realtime tới tất cả client trong room Booking_{bookingId}
            // LƯU Ý BẢO MẬT/UX: Client đối phương nhận tin qua SignalR luôn nhận IsMine = false
            if (_realtimeNotifier != null)
            {
                var broadcastDto = new ConsultationMessageDto
                {
                    Id = messageEntity.Id,
                    BookingId = messageEntity.BookingId,
                    SenderId = messageEntity.SenderId,
                    SenderRole = messageEntity.SenderRole,
                    SenderName = messageEntity.SenderName,
                    SenderAvatarUrl = messageEntity.SenderAvatarUrl,
                    Content = messageEntity.Content,
                    AttachmentUrl = messageEntity.AttachmentUrl,
                    AttachmentType = messageEntity.AttachmentType,
                    IsRead = messageEntity.IsRead,
                    CreatedAt = messageEntity.CreatedAt,
                    IsMine = false // Người nhận qua SignalR luôn là false
                };
                await _realtimeNotifier.NotifyChatMessageReceivedAsync(bookingId, broadcastDto);
            }

            _logger.LogInformation("Đã lưu và phát tin nhắn chat trong ca tư vấn {BookingId} bởi {SenderId} ({Role}).", bookingId, senderId, actualRole);

            return messageDto;
        }

        public async Task<ConsultationChatHistoryResponse> GetChatHistoryAsync(
            Guid userId,
            string userRole,
            Guid bookingId,
            ConsultationMessageQueryParameters query)
        {
            var (booking, slot, profile, isTaxpayer, isExpert) = await ValidateBookingAndMembershipAsync(bookingId, userId, userRole);

            var partnerDto = new ConsultationChatPartnerDto();
            if (isTaxpayer)
            {
                var expertUser = await _unitOfWork.Repository<User>().GetByIdAsync(profile.UserId);
                var latestApp = await _unitOfWork.Repository<ExpertApplication>().GetByIdAsync(profile.LatestApplicationId);
                var specs = await _unitOfWork.Repository<Specialization>().FindAsync(s => s.Id == booking.SpecializationId);
                var spec = specs.FirstOrDefault();

                partnerDto.UserId = profile.UserId;
                partnerDto.FullName = expertUser?.FullName ?? profile.JobTitle;
                partnerDto.AvatarUrl = latestApp?.AvatarUrl;
                partnerDto.Role = "expert";
                partnerDto.JobTitle = profile.JobTitle;
                partnerDto.SpecializationName = spec?.Name;
            }
            else
            {
                var taxpayerUser = await _unitOfWork.Repository<User>().GetByIdAsync(booking.UserId);
                partnerDto.UserId = booking.UserId;
                partnerDto.FullName = taxpayerUser?.FullName ?? "Khách hàng";
                partnerDto.Role = "taxpayer";
            }

            var sessions = await _unitOfWork.Repository<ConsultationSession>().FindAsync(s => s.BookingId == bookingId);
            var session = sessions.FirstOrDefault();

            // Truy vấn tin nhắn kèm Searching, Sorting và Paging (Mục 2.2 API Guidelines)
            var allMessagesQuery = (await _unitOfWork.Repository<ConsultationMessage>()
                .FindAsync(m => m.BookingId == bookingId)).AsQueryable();

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var keyword = query.Search.Trim().ToLowerInvariant();
                allMessagesQuery = allMessagesQuery.Where(m => m.Content.ToLower().Contains(keyword));
            }

            // Sorting
            bool isDesc = string.Equals(query.Sort, "-createdAt", StringComparison.OrdinalIgnoreCase);
            allMessagesQuery = isDesc 
                ? allMessagesQuery.OrderByDescending(m => m.CreatedAt) 
                : allMessagesQuery.OrderBy(m => m.CreatedAt);

            // Cấu hình phân trang động do Admin quản trị (Zero-Hardcode)
            var policy = await GetChatEffectivePolicyAsync();
            var defaultPageSize = policy.ChatHistoryDefaultPageSize > 0 ? policy.ChatHistoryDefaultPageSize : 50;
            var maxPageSize = policy.ChatHistoryMaxPageSize > 0 ? policy.ChatHistoryMaxPageSize : 100;

            var totalItems = allMessagesQuery.Count();
            var page = query.Page <= 0 ? 1 : query.Page;
            var pageSize = query.Size <= 0 ? defaultPageSize : Math.Min(query.Size, maxPageSize);
            var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            var pagedEntities = allMessagesQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var messageDtos = pagedEntities.Select(m => new ConsultationMessageDto
            {
                Id = m.Id,
                BookingId = m.BookingId,
                SenderId = m.SenderId,
                SenderRole = m.SenderRole,
                SenderName = m.SenderName,
                SenderAvatarUrl = m.SenderAvatarUrl,
                Content = m.Content,
                AttachmentUrl = m.AttachmentUrl,
                AttachmentType = m.AttachmentType,
                IsRead = m.IsRead,
                CreatedAt = m.CreatedAt,
                IsMine = m.SenderId == userId
            }).ToList();

            return new ConsultationChatHistoryResponse
            {
                BookingId = booking.Id,
                BookingCode = booking.BookingCode,
                TopicTitle = booking.TopicTitle,
                SessionType = booking.SessionType.ToString(),
                BookingStatus = booking.Status.ToString(),
                RoomUrl = session?.RoomUrl,
                Partner = partnerDto,
                Messages = new PagedResult<ConsultationMessageDto>
                {
                    Items = messageDtos,
                    Pagination = new PaginationMeta
                    {
                        Page = page,
                        PageSize = pageSize,
                        TotalItems = totalItems,
                        TotalPages = totalPages
                    }
                }
            };
        }

        public async Task<ConsultationMessageDto> GetMessageByIdAsync(Guid userId, string userRole, Guid bookingId, Guid messageId)
        {
            await ValidateBookingAndMembershipAsync(bookingId, userId, userRole);

            var message = await _unitOfWork.Repository<ConsultationMessage>().GetByIdAsync(messageId);
            if (message == null || message.BookingId != bookingId)
            {
                throw new NotFoundException($"Không tìm thấy tin nhắn với Id = {messageId} trong ca tư vấn này.");
            }

            return new ConsultationMessageDto
            {
                Id = message.Id,
                BookingId = message.BookingId,
                SenderId = message.SenderId,
                SenderRole = message.SenderRole,
                SenderName = message.SenderName,
                SenderAvatarUrl = message.SenderAvatarUrl,
                Content = message.Content,
                AttachmentUrl = message.AttachmentUrl,
                AttachmentType = message.AttachmentType,
                IsRead = message.IsRead,
                CreatedAt = message.CreatedAt,
                IsMine = message.SenderId == userId
            };
        }

        public async Task<bool> MarkMessagesAsReadAsync(Guid userId, string userRole, Guid bookingId)
        {
            await ValidateBookingAndMembershipAsync(bookingId, userId, userRole);

            var unreadMessages = (await _unitOfWork.Repository<ConsultationMessage>()
                .FindAsync(m => m.BookingId == bookingId && m.SenderId != userId && !m.IsRead))
                .ToList();

            if (!unreadMessages.Any())
            {
                return true;
            }

            var readAt = DateTimeOffset.UtcNow;
            var readMessageIds = new List<Guid>();

            foreach (var msg in unreadMessages)
            {
                msg.IsRead = true;
                readMessageIds.Add(msg.Id);
                _unitOfWork.Repository<ConsultationMessage>().Update(msg);
            }

            await _unitOfWork.SaveChangesAsync();

            // Bắn SignalR realtime thông báo trạng thái "Đã xem" cho người gửi
            if (_realtimeNotifier != null)
            {
                await _realtimeNotifier.NotifyMessagesReadAsync(bookingId, userId, readMessageIds, readAt);
            }

            return true;
        }

        public async Task<bool> SendTypingIndicatorAsync(Guid userId, string userRole, Guid bookingId, bool isTyping)
        {
            var (_, _, _, _, isExpert) = await ValidateBookingAndMembershipAsync(bookingId, userId, userRole);
            var role = isExpert ? "expert" : "taxpayer";

            if (_realtimeNotifier != null)
            {
                await _realtimeNotifier.NotifyTypingAsync(bookingId, userId, role, isTyping);
            }
            return true;
        }

        /// <summary>
        /// Đọc cấu hình hiệu lực cho Chat từ ConsultationPlatformPolicy kết hợp SystemConfig (Admin toàn quyền cấu hình động)
        /// </summary>
        private async Task<ConsultationPlatformPolicy> GetChatEffectivePolicyAsync()
        {
            var policy = await BookingHelper.GetPlatformPolicyAsync(_unitOfWork);

            try
            {
                var sysConfigRepo = _unitOfWork.Repository<SystemConfig>();
                var configs = await sysConfigRepo.FindAsync(c => c.IsActive && c.ConfigKey.StartsWith("CHAT_"));
                var configMap = configs.ToDictionary(c => c.ConfigKey.ToUpperInvariant(), c => c.ConfigValue);

                if (configMap.TryGetValue("CHAT_MESSAGE_MAX_LENGTH", out var maxLenStr) && int.TryParse(maxLenStr, out var maxLen) && maxLen > 0)
                {
                    policy.ChatMessageMaxLength = maxLen;
                }
                if (configMap.TryGetValue("CHAT_ALLOWED_ATTACHMENT_TYPES", out var allowedTypes) && !string.IsNullOrWhiteSpace(allowedTypes))
                {
                    policy.ChatAllowedAttachmentTypes = allowedTypes;
                }
                if (configMap.TryGetValue("CHAT_MAX_ATTACHMENT_URL_LENGTH", out var maxUrlLenStr) && int.TryParse(maxUrlLenStr, out var maxUrlLen) && maxUrlLen > 0)
                {
                    policy.ChatMaxAttachmentUrlLength = maxUrlLen;
                }
                if (configMap.TryGetValue("CHAT_HISTORY_DEFAULT_PAGE_SIZE", out var defPageStr) && int.TryParse(defPageStr, out var defPage) && defPage > 0)
                {
                    policy.ChatHistoryDefaultPageSize = defPage;
                }
                if (configMap.TryGetValue("CHAT_HISTORY_MAX_PAGE_SIZE", out var maxPageStr) && int.TryParse(maxPageStr, out var maxPage) && maxPage > 0)
                {
                    policy.ChatHistoryMaxPageSize = maxPage;
                }
                if (configMap.TryGetValue("CONSULTATION_ROOM_URL_BASE", out var roomBase) && !string.IsNullOrWhiteSpace(roomBase))
                {
                    policy.ChatRoomUrlBase = roomBase;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể đọc cấu hình ghi đè từ SystemConfig cho Chat, sử dụng ConsultationPlatformPolicy.");
            }

            return policy;
        }

        private async Task SynchronizeAttendanceOnMessageAsync(Booking booking, bool isTaxpayer, bool isExpert, DateTimeOffset nowUtc, ConsultationPlatformPolicy policy)
        {
            var existingSessions = await _unitOfWork.Repository<ConsultationSession>().FindAsync(s => s.BookingId == booking.Id);
            var session = existingSessions.FirstOrDefault();
            var isNew = false;

            if (session == null)
            {
                isNew = true;
                var cleanCode = booking.BookingCode.Replace("-", "_");
                var roomBase = string.IsNullOrWhiteSpace(policy.ChatRoomUrlBase) ? "https://meet.jit.si" : policy.ChatRoomUrlBase.TrimEnd('/');
                session = new ConsultationSession
                {
                    Id = Guid.NewGuid(),
                    BookingId = booking.Id,
                    RoomName = $"TaxKeepVN_{cleanCode}",
                    RoomUrl = $"{roomBase}/TaxKeepVN_{cleanCode}",
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc
                };
            }

            if (isTaxpayer)
            {
                session.UserCheckInAt ??= nowUtc;
            }
            if (isExpert)
            {
                session.ExpertCheckInAt ??= nowUtc;
            }

            var bothPresent = session.UserCheckInAt.HasValue && session.ExpertCheckInAt.HasValue;
            if (bothPresent && booking.Status == BookingStatus.CONFIRMED)
            {
                booking.Status = BookingStatus.IN_PROGRESS;
                booking.UpdatedAt = nowUtc;
                session.StartedAt ??= nowUtc;
                _unitOfWork.Repository<Booking>().Update(booking);

                if (_realtimeNotifier != null)
                {
                    await _realtimeNotifier.NotifySessionStartedAsync(booking.Id, session.StartedAt.Value);
                }
            }

            session.UpdatedAt = nowUtc;
            if (isNew)
            {
                await _unitOfWork.Repository<ConsultationSession>().AddAsync(session);
            }
            else
            {
                _unitOfWork.Repository<ConsultationSession>().Update(session);
            }

            await _unitOfWork.SaveChangesAsync();
        }

        private async Task<(Booking booking, ExpertSlot slot, ExpertProfile profile, bool isTaxpayer, bool isExpert)>
            ValidateBookingAndMembershipAsync(Guid bookingId, Guid userId, string userRole)
        {
            var booking = await _unitOfWork.Repository<Booking>().GetByIdAsync(bookingId);
            if (booking == null)
            {
                throw new NotFoundException($"Không tìm thấy lịch hẹn với Id = {bookingId}.");
            }

            var slot = await _unitOfWork.Repository<ExpertSlot>().GetByIdAsync(booking.ExpertSlotId);
            if (slot == null)
            {
                throw new NotFoundException("Không tìm thấy khung giờ của lịch hẹn.");
            }

            var profile = await _unitOfWork.Repository<ExpertProfile>().GetByIdAsync(slot.ExpertProfileId);
            if (profile == null)
            {
                throw new NotFoundException("Không tìm thấy hồ sơ chuyên gia.");
            }

            var isTaxpayer = booking.UserId == userId;
            var isExpert = profile.UserId == userId;
            var isAdmin = string.Equals(userRole, "admin", StringComparison.OrdinalIgnoreCase);

            if (!isTaxpayer && !isExpert && !isAdmin)
            {
                throw new ForbiddenException("FORBIDDEN", "Bạn không có quyền tham gia cuộc trò chuyện của ca tư vấn này.");
            }

            return (booking, slot, profile, isTaxpayer, isExpert);
        }
    }
}
