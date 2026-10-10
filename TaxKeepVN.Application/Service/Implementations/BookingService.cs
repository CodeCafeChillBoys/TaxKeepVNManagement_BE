using Microsoft.Extensions.Logging;
using TaxKeepVN.Application.DTOs.Bookings;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Experts;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Helpers;
using TaxKeepVN.Application.Mappers;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    public class BookingService : IBookingService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _fileStorageService;
        private readonly ILogger<BookingService> _logger;

        public BookingService(
            IUnitOfWork unitOfWork,
            IFileStorageService fileStorageService,
            ILogger<BookingService> logger)
        {
            _unitOfWork = unitOfWork;
            _fileStorageService = fileStorageService;
            _logger = logger;
        }

        // ── 1. LẤY LỊCH RẢNH KHẢ DỤNG CHO VIỆC ĐẶT LỊCH ──────────────────────────────
        public async Task<List<ExpertAvailableSlotDto>> GetAvailableSlotsForBookingAsync(Guid expertProfileId, DateOnly? fromDate = null, int days = 14)
        {
            var profile = await _unitOfWork.Repository<ExpertProfile>().GetByIdAsync(expertProfileId);
            if (profile == null)
            {
                throw new NotFoundException($"Không tìm thấy chuyên gia với Id = {expertProfileId}.");
            }
            // 4 tiếng quy định booking lich trước 4 tiếng
            var leadTimeHours = BookingHelper.MinimumLeadTimeHours;
            // lấy lên tg hiện tại 
            var nowUtc = DateTimeOffset.UtcNow;
            // add thêm 4 tiếng vào trước khi đặt lịch
            var minAllowedDateTime = nowUtc.AddHours(leadTimeHours);

            // Thời gian bắt đầu ngày đc chọn
            var start = fromDate ?? DateOnly.FromDateTime(nowUtc.DateTime);
            // giới thời tg chọn trong vòng 30 ngày
            var end = start.AddDays(Math.Max(1, Math.Min(days, 30)));

            // Lấy các slot còn hoạt động, chưa bị đặt chính thức
            var slots = (await _unitOfWork.Repository<ExpertSlot>().FindAsync(s =>
                s.ExpertProfileId == expertProfileId &&
                s.SlotDate >= start &&
                s.SlotDate <= end &&
                s.IsActive &&
                !s.IsBooked)).ToList();

            // Lọc các slot:
            // 1. Không bị tạm giữ (HoldExpiresAt == null hoặc HoldExpiresAt <= nowUtc)
            // 2. Thỏa mãn Lead-time (thời gian bắt đầu slot >= nowUtc + leadTimeHours)
            var availableSlots = slots.Where(s =>
            {
                // kiểm tra xem thời gian này có bị lock 10 phút và && thời gian có lớn hơn tg lấy ra hiện tại hay ko
                var isHeld = s.HoldExpiresAt.HasValue && s.HoldExpiresAt.Value > nowUtc;
                // nếu true nhảy xuống đẩy
                if (isHeld) return false;

                var slotStartUtc = new DateTimeOffset(s.SlotDate.ToDateTime(s.StartTime), TimeSpan.Zero);
                return slotStartUtc >= minAllowedDateTime;
            })
            .OrderBy(s => s.SlotDate)
            .ThenBy(s => s.StartTime)
            .Select(s => new ExpertAvailableSlotDto
            {
                Id = s.Id,
                SlotDate = s.SlotDate,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                SessionType = s.SessionType,
                IsBooked = false,
                IsActive = s.IsActive
            })
            .ToList();

            return availableSlots;
        }

        // ── 2. XEM TRƯỚC TÓM TẮT THÔNG TIN ĐẶT LỊCH (PREVIEW) ─────────────────────────
        public async Task<BookingPreviewResponse> PreviewBookingAsync(Guid userId, BookingPreviewRequest request)
        {
            var slot = await _unitOfWork.Repository<ExpertSlot>().GetByIdAsync(request.ExpertSlotId);
            if (slot == null || !slot.IsActive)
            {
                throw new NotFoundException("Khung giờ được chọn không tồn tại hoặc đã ngừng hoạt động.");
            }

            var profile = await _unitOfWork.Repository<ExpertProfile>().GetByIdAsync(slot.ExpertProfileId);
            if (profile == null)
            {
                throw new NotFoundException("Không tìm thấy thông tin chuyên gia.");
            }

            // Kiểm tra không tự book chính mình
            if (profile.UserId == userId)
            {
                throw new BadRequestException("SELF_BOOKING_NOT_ALLOWED", "Bạn không thể đặt lịch tư vấn với chính mình.");
            }

            var spec = (await _unitOfWork.Repository<Specialization>().FindAsync(s => s.Id == request.SpecializationId && s.IsActive)).FirstOrDefault();
            if (spec == null)
            {
                throw new NotFoundException("Chủ đề tư vấn không tồn tại hoặc đã bị khóa.");
            }

            // Lấy thông tin tài khoản chuyên gia (tên, avatar)
            var expertUser = await _unitOfWork.Repository<User>().GetByIdAsync(profile.UserId);
            var app = await _unitOfWork.Repository<ExpertApplication>().GetByIdAsync(profile.LatestApplicationId);
            var expertFullName = app?.FullName ?? expertUser?.FullName;
            var avatarUrl = app?.AvatarUrl;

            var durationMinutes = (int)(slot.EndTime - slot.StartTime).TotalMinutes;
            var fee = await BookingHelper.CalculateConsultationFeeAsync(_unitOfWork, profile, slot.SessionType, durationMinutes);

            
            var leadTimeHours = BookingHelper.MinimumLeadTimeHours;
            var nowUtc = DateTimeOffset.UtcNow;
            // lấy lên thời gian bắt đầu đc truyền vào
            var slotStartUtc = new DateTimeOffset(slot.SlotDate.ToDateTime(slot.StartTime), TimeSpan.Zero);

            bool isValid = true;
            string? validationMsg = null;

            if (slot.IsBooked || (slot.HoldExpiresAt.HasValue && slot.HoldExpiresAt.Value > nowUtc && slot.HoldUserId != userId))
            {
                isValid = false;
                validationMsg = "Khung giờ này vừa có người khác đặt hoặc đang được giữ chỗ.";
            }
            else if (slotStartUtc < nowUtc.AddHours(leadTimeHours))
            {
                isValid = false;
                validationMsg = $"Khung giờ này quá sát hiện tại (quy định phải đặt trước tối thiểu {leadTimeHours} giờ).";
            }
            else
            {
                var hasConflict = await BookingHelper.CheckUserSlotConflictAsync(_unitOfWork, userId, slot.SlotDate, slot.StartTime, slot.EndTime);
                if (hasConflict)
                {
                    isValid = false;
                    validationMsg = "Bạn đã có một lịch hẹn khác trùng vào khung giờ này.";
                }
            }

            return new BookingPreviewResponse
            {
                ExpertSlotId = slot.Id,
                ExpertProfileId = profile.Id,
                ExpertFullName = expertFullName,
                ExpertAvatarUrl = avatarUrl,
                ExpertJobTitle = profile.JobTitle,
                SlotDate = slot.SlotDate,
                StartTime = slot.StartTime,
                EndTime = slot.EndTime,
                DurationMinutes = durationMinutes,
                SessionType = slot.SessionType,
                SpecializationId = spec.Id,
                SpecializationName = spec.Name,
                TotalFee = fee,
                MinimumLeadTimeHours = leadTimeHours,
                IsValid = isValid,
                ValidationMessage = validationMsg
            };
        }

        // ── 3. KHỞI TẠO LỊCH HẸN & TẠM GIỮ SLOT 10 PHÚT (CREATE BOOKING) ──────────────
        public async Task<BookingDetailResponse> CreateBookingAsync(Guid userId, CreateBookingRequest request)
        {
            var user = await _unitOfWork.Repository<User>().GetByIdAsync(userId);
            if (user == null)
            {
                throw new UnauthorizedException("UNAUTHORIZED", "Người dùng không hợp lệ hoặc chưa đăng nhập.");
            }

            var spec = (await _unitOfWork.Repository<Specialization>().FindAsync(s => s.Id == request.SpecializationId && s.IsActive)).FirstOrDefault();
            if (spec == null)
            {
                throw new NotFoundException("Chủ đề tư vấn không tồn tại hoặc đã ngừng hỗ trợ.");
            }

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var slot = await _unitOfWork.Repository<ExpertSlot>().GetByIdAsync(request.ExpertSlotId);
                if (slot == null || !slot.IsActive)
                {
                    throw new NotFoundException("Khung giờ được chọn không tồn tại hoặc đã bị khóa.");
                }

                var profile = await _unitOfWork.Repository<ExpertProfile>().GetByIdAsync(slot.ExpertProfileId);
                if (profile == null)
                {
                    throw new NotFoundException("Không tìm thấy thông tin chuyên gia.");
                }

                // 1. Kiểm tra không tự đặt lịch với chính mình
                if (profile.UserId == userId)
                {
                    throw new BadRequestException("SELF_BOOKING_NOT_ALLOWED", "Bạn không thể tự đặt lịch tư vấn với chính mình.");
                }

                var nowUtc = DateTimeOffset.UtcNow;
                var leadTimeHours = BookingHelper.MinimumLeadTimeHours;
                var slotStartUtc = new DateTimeOffset(slot.SlotDate.ToDateTime(slot.StartTime), TimeSpan.Zero);

                // 2. Kiểm tra Minimum Lead-time (Đặc tả 6 & 8)
                if (slotStartUtc < nowUtc.AddHours(leadTimeHours))
                {
                    throw new BadRequestException("LEAD_TIME_VIOLATION",
                        $"Bạn phải đặt trước giờ hẹn tối thiểu {leadTimeHours} tiếng theo quy định.");
                }

                // 3. Kiểm tra trùng slot (Atomic Concurrency Check - Đặc tả 6)
                if (slot.IsBooked || (slot.HoldExpiresAt.HasValue && slot.HoldExpiresAt.Value > nowUtc && slot.HoldUserId != userId))
                {
                    throw new ConflictException("SLOT_ALREADY_TAKEN",
                        "Khung giờ này vừa có người đặt, vui lòng chọn khung giờ khác.");
                }

                // 4. Kiểm tra User không bị trùng lịch với chuyên gia khác (Đặc tả 8)
                var hasConflict = await BookingHelper.CheckUserSlotConflictAsync(_unitOfWork, userId, slot.SlotDate, slot.StartTime, slot.EndTime);
                if (hasConflict)
                {
                    throw new ConflictException("USER_SLOT_CONFLICT",
                        "Bạn đã có một lịch hẹn khác trong cùng khung giờ này với chuyên gia khác.");
                }

                var durationMinutes = (int)(slot.EndTime - slot.StartTime).TotalMinutes;
                var fee = await BookingHelper.CalculateConsultationFeeAsync(_unitOfWork, profile, slot.SessionType, durationMinutes);

                // Tạm khóa slot trong 10 phút (Hold slot)
                var holdExpiresAt = nowUtc.AddMinutes(BookingHelper.DefaultHoldMinutes);
                slot.HoldExpiresAt = holdExpiresAt;
                slot.HoldUserId = userId;
                slot.UpdatedAt = nowUtc;
                _unitOfWork.Repository<ExpertSlot>().Update(slot);

                // Tạo đối tượng Booking
                var booking = new Booking
                {
                    Id = Guid.NewGuid(),
                    BookingCode = BookingHelper.GenerateBookingCode(),
                    UserId = userId,
                    ExpertSlotId = slot.Id,
                    SpecializationId = spec.Id,
                    SessionType = slot.SessionType,
                    DurationMinutes = durationMinutes,
                    Fee = fee,
                    TopicTitle = request.TopicTitle.Trim(),
                    ProblemDescription = request.ProblemDescription.Trim(),
                    Status = BookingStatus.PENDING_PAYMENT,
                    HoldExpiresAt = holdExpiresAt,
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc
                };

                await _unitOfWork.Repository<Booking>().AddAsync(booking);

                // Upload tệp đính kèm (nếu có)
                var attachmentDtos = new List<BookingAttachmentDto>();
                if (request.Attachments != null && request.Attachments.Any())
                {
                    foreach (var file in request.Attachments)
                    {
                        var fileUrl = await _fileStorageService.SaveFileAsync(file, "consultation-attachments");
                        var attachment = new BookingAttachment
                        {
                            Id = Guid.NewGuid(),
                            BookingId = booking.Id,
                            FileName = file.FileName,
                            FileUrl = fileUrl,
                            FileSize = file.Length,
                            ContentType = file.ContentType ?? "application/octet-stream",
                            CreatedAt = nowUtc
                        };

                        await _unitOfWork.Repository<BookingAttachment>().AddAsync(attachment);

                        attachmentDtos.Add(attachment.ToDto());
                    }
                }

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransactionAsync();

                _logger.LogInformation("Khởi tạo booking {BookingCode} thành công cho User {UserId}, slot tạm giữ đến {HoldExpiresAt}.",
                    booking.BookingCode, userId, holdExpiresAt);

                var expertUser = await _unitOfWork.Repository<User>().GetByIdAsync(profile.UserId);
                var app = await _unitOfWork.Repository<ExpertApplication>().GetByIdAsync(profile.LatestApplicationId);

                return BookingMapper.MapToDetailResponse(booking, user, profile, slot, spec, expertUser, app, attachmentDtos);
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync();
                throw;
            }
        }

        // ── 4. XEM CHI TIẾT LỊCH HẸN ────────────────────────────────────────────────
        public async Task<BookingDetailResponse> GetBookingByIdAsync(Guid bookingId, Guid currentUserId, string currentUserRole)
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
                throw new NotFoundException("Không tìm thấy thông tin chuyên gia.");
            }

            // Kiểm tra quyền truy cập: Chỉ User tạo booking, Chuyên gia của booking, hoặc Admin mới được xem
            var isClient = booking.UserId == currentUserId;
            var isExpert = profile.UserId == currentUserId;
            var isAdmin = currentUserRole.Equals("admin", StringComparison.OrdinalIgnoreCase);

            if (!isClient && !isExpert && !isAdmin)
            {
                throw new ForbiddenException("Bạn không có quyền truy cập thông tin ca tư vấn này.");
            }

            var user = await _unitOfWork.Repository<User>().GetByIdAsync(booking.UserId);
            var spec = (await _unitOfWork.Repository<Specialization>().FindAsync(s => s.Id == booking.SpecializationId)).FirstOrDefault();
            var expertUser = await _unitOfWork.Repository<User>().GetByIdAsync(profile.UserId);
            var app = await _unitOfWork.Repository<ExpertApplication>().GetByIdAsync(profile.LatestApplicationId);

            var attachments = (await _unitOfWork.Repository<BookingAttachment>().FindAsync(a => a.BookingId == booking.Id)).ToDtoList();

            return BookingMapper.MapToDetailResponse(booking, user!, profile, slot!, spec!, expertUser, app, attachments);
        }

        // ── 5. LẤY DANH SÁCH LỊCH HẸN CỦA KHÁCH HÀNG (MY BOOKINGS) ──────────────────
        public async Task<PagedResult<BookingListItemResponse>> GetMyBookingsAsync(Guid userId, BookingStatus? status = null, int page = 1, int size = 10)
        {
            var allBookings = (await _unitOfWork.Repository<Booking>().FindAsync(b => b.UserId == userId)).AsQueryable();

            if (status.HasValue)
            {
                allBookings = allBookings.Where(b => b.Status == status.Value);
            }

            allBookings = allBookings.OrderByDescending(b => b.CreatedAt);
            var totalItems = allBookings.Count();
            var paged = allBookings.Skip((page - 1) * size).Take(size).ToList();

            var items = await BookingMapper.MapToListItemsAsync(_unitOfWork, paged);

            return new PagedResult<BookingListItemResponse>
            {
                Items = items,
                Pagination = new PaginationMeta
                {
                    Page = page,
                    PageSize = size,
                    TotalItems = totalItems,
                    TotalPages = (int)Math.Ceiling(totalItems / (double)size)
                }
            };
        }

        // ── 6. LẤY DANH SÁCH CA TƯ VẤN CHỜ CHUYÊN GIA DUYỆT (PENDING APPROVAL) ───────
        public async Task<PagedResult<BookingListItemResponse>> GetExpertPendingBookingsAsync(Guid expertUserId, int page = 1, int size = 10)
        {
            var profile = (await _unitOfWork.Repository<ExpertProfile>().FindAsync(p => p.UserId == expertUserId)).FirstOrDefault();
            if (profile == null)
            {
                throw new NotFoundException("Tài khoản này chưa có hồ sơ chuyên gia.");
            }

            var expertSlotIds = (await _unitOfWork.Repository<ExpertSlot>().FindAsync(s => s.ExpertProfileId == profile.Id))
                .Select(s => s.Id).ToList();

            var nowUtc = DateTimeOffset.UtcNow;
            var pendingBookings = (await _unitOfWork.Repository<Booking>().FindAsync(b =>
                expertSlotIds.Contains(b.ExpertSlotId) &&
                b.Status == BookingStatus.AWAITING_EXPERT_APPROVAL &&
                b.ApprovalDeadline.HasValue &&
                b.ApprovalDeadline.Value > nowUtc))
                .OrderBy(b => b.ApprovalDeadline)
                .AsQueryable();

            var totalItems = pendingBookings.Count();
            var paged = pendingBookings.Skip((page - 1) * size).Take(size).ToList();

            var items = await BookingMapper.MapToListItemsAsync(_unitOfWork, paged);

            return new PagedResult<BookingListItemResponse>
            {
                Items = items,
                Pagination = new PaginationMeta
                {
                    Page = page,
                    PageSize = size,
                    TotalItems = totalItems,
                    TotalPages = (int)Math.Ceiling(totalItems / (double)size)
                }
            };
        }

        // ── 7. LẤY TOÀN BỘ LỊCH HẸN CỦA CHUYÊN GIA ────────────────────────────────────
        public async Task<PagedResult<BookingListItemResponse>> GetExpertBookingsAsync(Guid expertUserId, BookingStatus? status = null, int page = 1, int size = 10)
        {
            var profile = (await _unitOfWork.Repository<ExpertProfile>().FindAsync(p => p.UserId == expertUserId)).FirstOrDefault();
            if (profile == null)
            {
                throw new NotFoundException("Tài khoản này chưa có hồ sơ chuyên gia.");
            }

            var expertSlotIds = (await _unitOfWork.Repository<ExpertSlot>().FindAsync(s => s.ExpertProfileId == profile.Id))
                .Select(s => s.Id).ToList();

            var query = (await _unitOfWork.Repository<Booking>().FindAsync(b => expertSlotIds.Contains(b.ExpertSlotId))).AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(b => b.Status == status.Value);
            }

            query = query.OrderByDescending(b => b.CreatedAt);
            var totalItems = query.Count();
            var paged = query.Skip((page - 1) * size).Take(size).ToList();

            var items = await BookingMapper.MapToListItemsAsync(_unitOfWork, paged);

            return new PagedResult<BookingListItemResponse>
            {
                Items = items,
                Pagination = new PaginationMeta
                {
                    Page = page,
                    PageSize = size,
                    TotalItems = totalItems,
                    TotalPages = (int)Math.Ceiling(totalItems / (double)size)
                }
            };
        }

        // ── 8. CHUYÊN GIA ĐỒNG Ý TIẾP NHẬN CA TƯ VẤN (ACCEPT) ────────────────────────
        public async Task<BookingDetailResponse> AcceptBookingAsync(Guid expertUserId, Guid bookingId)
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
            if (profile == null || profile.UserId != expertUserId)
            {
                throw new ForbiddenException("Bạn không phải chuyên gia được phân công cho ca tư vấn này.");
            }

            if (booking.Status != BookingStatus.AWAITING_EXPERT_APPROVAL)
            {
                throw new BadRequestException("INVALID_STATUS",
                    $"Lịch hẹn hiện tại ở trạng thái '{booking.Status}', không thể tiếp nhận.");
            }

            var nowUtc = DateTimeOffset.UtcNow;
            if (booking.ApprovalDeadline.HasValue && booking.ApprovalDeadline.Value <= nowUtc)
            {
                throw new BadRequestException("APPROVAL_TIMEOUT",
                    "Đã quá thời hạn 2 giờ để tiếp nhận ca tư vấn này. Hệ thống đã tự động hủy yêu cầu.");
            }

            booking.Status = BookingStatus.CONFIRMED;
            booking.ApprovedAt = nowUtc;
            booking.UpdatedAt = nowUtc;

            _unitOfWork.Repository<Booking>().Update(booking);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Chuyên gia {ExpertUserId} đã đồng ý tiếp nhận booking {BookingCode}.",
                expertUserId, booking.BookingCode);

            return await GetBookingByIdAsync(booking.Id, expertUserId, "expert");
        }

        // ── 9. CHUYÊN GIA TỪ CHỐI TIẾP NHẬN CA TƯ VẤN (REJECT) ───────────────────────
        public async Task<BookingDetailResponse> RejectBookingAsync(Guid expertUserId, Guid bookingId, RejectBookingRequest request)
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
            if (profile == null || profile.UserId != expertUserId)
            {
                throw new ForbiddenException("Bạn không phải chuyên gia được phân công cho ca tư vấn này.");
            }

            if (booking.Status != BookingStatus.AWAITING_EXPERT_APPROVAL)
            {
                throw new BadRequestException("INVALID_STATUS",
                    $"Lịch hẹn hiện tại ở trạng thái '{booking.Status}', không thể từ chối.");
            }

            var nowUtc = DateTimeOffset.UtcNow;
            booking.Status = BookingStatus.REJECTED_BY_EXPERT;
            booking.RejectedAt = nowUtc;
            booking.RejectionReason = request.Reason.Trim();
            booking.RefundStatus = "PENDING_REFUND";
            booking.CancelledBy = "EXPERT";
            booking.CancelledAt = nowUtc;
            booking.UpdatedAt = nowUtc;

            // Mở lại khung giờ
            if (slot != null)
            {
                slot.IsBooked = false;
                slot.HoldExpiresAt = null;
                slot.HoldUserId = null;
                slot.UpdatedAt = nowUtc;
                _unitOfWork.Repository<ExpertSlot>().Update(slot);
            }

            _unitOfWork.Repository<Booking>().Update(booking);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Chuyên gia {ExpertUserId} đã từ chối booking {BookingCode}. Lý do: {Reason}",
                expertUserId, booking.BookingCode, request.Reason);

            return await GetBookingByIdAsync(booking.Id, expertUserId, "expert");
        }

        // ── 10. KHÁCH HÀNG HỦY LỊCH HẸN (CANCEL) ──────────────────────────────────────
        public async Task<BookingDetailResponse> CancelBookingAsync(Guid userId, Guid bookingId, string reason)
        {
            var booking = await _unitOfWork.Repository<Booking>().GetByIdAsync(bookingId);
            if (booking == null)
            {
                throw new NotFoundException($"Không tìm thấy lịch hẹn với Id = {bookingId}.");
            }

            if (booking.UserId != userId)
            {
                throw new ForbiddenException("Bạn không có quyền hủy lịch hẹn này.");
            }

            if (booking.Status == BookingStatus.COMPLETED ||
                booking.Status == BookingStatus.CANCELLED_BY_USER ||
                booking.Status == BookingStatus.CANCELLED_BY_EXPERT ||
                booking.Status == BookingStatus.EXPIRED_UNPAID ||
                booking.Status == BookingStatus.EXPIRED_NO_RESPONSE ||
                booking.Status == BookingStatus.REJECTED_BY_EXPERT)
            {
                throw new BadRequestException("CANNOT_CANCEL", $"Lịch hẹn đã ở trạng thái '{booking.Status}', không thể hủy.");
            }

            var nowUtc = DateTimeOffset.UtcNow;
            booking.Status = BookingStatus.CANCELLED_BY_USER;
            booking.CancelledBy = "USER";
            booking.CancelledAt = nowUtc;
            booking.CancellationReason = string.IsNullOrWhiteSpace(reason) ? "Khách hàng chủ động hủy." : reason.Trim();
            booking.UpdatedAt = nowUtc;

            // Nếu đã thanh toán -> kích hoạt hoàn tiền
            if (booking.PaidAt.HasValue)
            {
                booking.RefundStatus = "PENDING_REFUND";
            }

            // Giải phóng slot
            var slot = await _unitOfWork.Repository<ExpertSlot>().GetByIdAsync(booking.ExpertSlotId);
            if (slot != null)
            {
                slot.IsBooked = false;
                slot.HoldExpiresAt = null;
                slot.HoldUserId = null;
                slot.UpdatedAt = nowUtc;
                _unitOfWork.Repository<ExpertSlot>().Update(slot);
            }

            _unitOfWork.Repository<Booking>().Update(booking);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("User {UserId} đã hủy booking {BookingCode}.", userId, booking.BookingCode);

            return await GetBookingByIdAsync(booking.Id, userId, "taxpayer");
        }

        // ── 11. TẢI TỆP CHỨNG TỪ AN TOÀN (SECURE DOWNLOAD) ───────────────────────────
        public async Task<string> GetAttachmentDownloadUrlAsync(Guid bookingId, Guid attachmentId, Guid currentUserId, string currentUserRole)
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

            var isClient = booking.UserId == currentUserId;
            var isExpert = profile.UserId == currentUserId;
            var isAdmin = currentUserRole.Equals("admin", StringComparison.OrdinalIgnoreCase);

            if (!isClient && !isExpert && !isAdmin)
            {
                throw new ForbiddenException("Bạn không có quyền truy cập tệp chứng từ này.");
            }

            var attachment = (await _unitOfWork.Repository<BookingAttachment>().FindAsync(a => a.Id == attachmentId && a.BookingId == bookingId)).FirstOrDefault();
            if (attachment == null)
            {
                throw new NotFoundException("Không tìm thấy tệp đính kèm yêu cầu.");
            }

            return attachment.FileUrl;
        }
    }
}
