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
            // Đọc thời gian đặt trước tối thiểu từ chính sách sàn tư vấn (ConsultationPlatformPolicy)
            var policy = await BookingHelper.GetPlatformPolicyAsync(_unitOfWork);
            var leadTimeHours = policy.BookingMinLeadTimeHours;
            // lấy lên tg hiện tại 
            var nowUtc = DateTimeOffset.UtcNow;
            // add thêm thời gian đặt trước tối thiểu vào trước khi đặt lịch
            var minAllowedDateTime = nowUtc.AddHours(leadTimeHours);

            // Thời gian bắt đầu ngày đc chọn (theo giờ Việt Nam)
            var nowVietnam = BookingHelper.GetVietnamNow();
            var start = fromDate ?? DateOnly.FromDateTime(nowVietnam.DateTime);
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
                // kiểm tra xem thời gian này có bị lock và && thời gian có lớn hơn tg lấy ra hiện tại hay ko
                var isHeld = s.HoldExpiresAt.HasValue && s.HoldExpiresAt.Value > nowUtc;
                // nếu true nhảy xuống đẩy
                if (isHeld) return false;

                var slotStartUtc = BookingHelper.ConvertSlotToUtc(s.SlotDate, s.StartTime);
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

            var policy = await BookingHelper.GetPlatformPolicyAsync(_unitOfWork);
            var leadTimeHours = policy.BookingMinLeadTimeHours;
            var nowUtc = DateTimeOffset.UtcNow;
            var slotStartUtc = BookingHelper.ConvertSlotToUtc(slot.SlotDate, slot.StartTime);

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
                ExpertFullName = expertFullName ?? string.Empty,
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
                // thời gian chuyên gia bị lố ngày người dùng đặt sẽ ko hợp lệ
                var profile = await _unitOfWork.Repository<ExpertProfile>().GetByIdAsync(slot.ExpertProfileId);
                if (profile == null)
                {
                    throw new NotFoundException("Không tìm thấy thông tin chuyên gia.");
                }

                // 1. Kiểm tra không tự đặt lịch với chính mình
                // chuyên gia tự booking chính mình
                if (profile.UserId == userId)
                {
                    throw new BadRequestException("SELF_BOOKING_NOT_ALLOWED", "Bạn không thể tự đặt lịch tư vấn với chính mình.");
                }
                // lấy ngày giờ thời gian hiện tại
                var nowUtc = DateTimeOffset.UtcNow;
                var policy = await BookingHelper.GetPlatformPolicyAsync(_unitOfWork);
                var leadTimeHours = policy.BookingMinLeadTimeHours;
                var holdMinutes = policy.BookingSlotHoldMinutes;
                // Chuyển đổi giờ slot (lưu theo giờ VN) sang UTC để so sánh chuẩn xác với nowUtc
                var slotStartUtc = BookingHelper.ConvertSlotToUtc(slot.SlotDate, slot.StartTime);


                // 2. Kiểm tra Minimum Lead-time (Đặc tả 6 & 8)
                // đặt mà thời gian nó sát vs tk gian chuyên gia chuẩn bị làm
                if (slotStartUtc < nowUtc.AddHours(leadTimeHours))
                {
                    throw new BadRequestException("LEAD_TIME_VIOLATION",
                        $"Bạn phải đặt trước giờ hẹn tối thiểu {leadTimeHours} tiếng theo quy định.");
                }

                // 3. Kiểm tra trùng slot (Atomic Concurrency Check - Đặc tả 6)
                // cùng 1 chuyên gia sẽ có 2 tk đặt nhưng sẽ có 1 tk đặt trước nếu ko thanh toán trong 10 phút tk kia sẽ đặt đc
                if (slot.IsBooked || (slot.HoldExpiresAt.HasValue && slot.HoldExpiresAt.Value > nowUtc && slot.HoldUserId != userId))
                {
                    throw new ConflictException("SLOT_ALREADY_TAKEN",
                        "Khung giờ này vừa có người đặt, vui lòng chọn khung giờ khác.");
                }

                // 4. Kiểm tra User không bị trùng lịch với chuyên gia khác (Đặc tả 8)
                // cùng 1 tk người nộp thuế nhưng mà nó đặt hai chuyên gia khác nha vs khung giờ khác nhau
                var hasConflict = await BookingHelper.CheckUserSlotConflictAsync(_unitOfWork, userId, slot.SlotDate, slot.StartTime, slot.EndTime);
                if (hasConflict)
                {
                    throw new ConflictException("USER_SLOT_CONFLICT",
                        "Bạn đã có một lịch hẹn khác trong cùng khung giờ này với chuyên gia khác.");
                }

                var durationMinutes = (int)(slot.EndTime - slot.StartTime).TotalMinutes;
                var fee = await BookingHelper.CalculateConsultationFeeAsync(_unitOfWork, profile, slot.SessionType, durationMinutes);

                // Tạm khóa slot theo cấu hình động BOOKING_SLOT_HOLD_MINUTES (mặc định 10 phút)
                var holdExpiresAt = nowUtc.AddMinutes(holdMinutes);
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
                // khỏi tạo một cái list lưu lại file trên ram
                var attachmentDtos = new List<BookingAttachmentDto>();
                // kiểm tra xem có file ko 
                if (request.Attachments != null && request.Attachments.Any())
                {
                    // duyệt vào qua từng file đc tuyền vào
                    foreach (var file in request.Attachments)
                    {
                        // lưu trên superbase
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
                        // lưu vào DB
                        await _unitOfWork.Repository<BookingAttachment>().AddAsync(attachment);
                        // lưu trên ram
                        attachmentDtos.Add(attachment.ToDto());
                    }
                }

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransactionAsync();

                _logger.LogInformation("Khởi tạo booking {BookingCode} thành công cho User {UserId}, slot tạm giữ đến {HoldExpiresAt}.",
                    booking.BookingCode, userId, holdExpiresAt);
                
                // sau khi kết thúc phiên check kiểm tra
                var expertUser = await _unitOfWork.Repository<User>().GetByIdAsync(profile.UserId);
                // kiểm tra hồ sơ tuyển dụng của chuyên gia
                var app = await _unitOfWork.Repository<ExpertApplication>().GetByIdAsync(profile.LatestApplicationId);

                // trả BookingDetailResponse
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
            // check có phải chuyên gia ko 
            var profile = (await _unitOfWork.Repository<ExpertProfile>().FindAsync(p => p.UserId == expertUserId)).FirstOrDefault();
            if (profile == null)
            {
                throw new NotFoundException("Tài khoản này chưa có hồ sơ chuyên gia.");
            }
            // check chuyên gia đó có những khung giờ này ko
            var expertSlotIds = (await _unitOfWork.Repository<ExpertSlot>().FindAsync(s => s.ExpertProfileId == profile.Id))
                .Select(s => s.Id).ToList();
            // lấy lên thời gian hiện tại
            var nowUtc = DateTimeOffset.UtcNow;
            // lấy lên danh sách người nộp thuế đang với trạng thái là chờ phê duyệt
            // check slot, check status.
            // check xem tg mà chhuyeen gia phải phê duyệt
            // check thời gian 
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

            // Gửi thông báo cho khách hàng khi chuyên gia chính thức xác nhận lịch (Đặc tả 4)
            await _unitOfWork.Repository<SystemNotification>().AddAsync(new SystemNotification
            {
                NotificationId = Guid.NewGuid(),
                UserId = booking.UserId,
                Title = "Chuyên gia đã xác nhận lịch tư vấn của bạn!",
                Message = $"Chuyên gia đã đồng ý tiếp nhận ca tư vấn #{booking.BookingCode} vào ngày {slot.SlotDate:dd/MM/yyyy} ({slot.StartTime:HH\\:mm} - {slot.EndTime:HH\\:mm}).",
                NotificationType = "BOOKING_CONFIRMED_BY_EXPERT",
                IsRead = false,
                TargetActionUrl = $"/bookings/{booking.Id}",
                CreatedAt = nowUtc.UtcDateTime
            });

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Chuyên gia {ExpertUserId} đã đồng ý tiếp nhận booking {BookingCode}.",
                expertUserId, booking.BookingCode);

            return await GetBookingByIdAsync(booking.Id, expertUserId, "expert");
        }

        // ── 9. CHUYÊN GIA TỪ CHỐI TIẾP NHẬN CA TƯ VẤN (REJECT) ───────────────────────
        public async Task<BookingDetailResponse> RejectBookingAsync(Guid expertUserId, Guid bookingId, RejectBookingRequest request,string? cancelledBy = null)
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
            var cleanReason = request.Reason.Trim();

            booking.Status = BookingStatus.REJECTED_BY_EXPERT;
            booking.RejectedAt = nowUtc;
            booking.RejectionReason = request.Reason.Trim();
            booking.RefundStatus = "PENDING_REFUND";
            booking.CancelledBy = !string.IsNullOrWhiteSpace(cancelledBy) ? cancelledBy : "EXPERT";
            booking.CancelledAt = nowUtc;
            booking.UpdatedAt = nowUtc;

            // Hoàn tiền 100% từ Ví tạm giữ Escrow về cho khách hàng (Đặc tả 4 - Mục 5.3 Trường hợp 2)
            var escrows = await _unitOfWork.Repository<EscrowTransaction>().FindAsync(e => e.BookingId == booking.Id);
            var escrow = escrows.FirstOrDefault();
            if (escrow != null)
            {
                escrow.EscrowStatus = EscrowStatus.Refunded;
                escrow.RefundAmount = escrow.Amount;
                escrow.RefundReason = $"Chuyên gia từ chối tiếp nhận lịch hẹn: {cleanReason}";
                escrow.RefundedAt = nowUtc;
                escrow.UpdatedAt = nowUtc;
                _unitOfWork.Repository<EscrowTransaction>().Update(escrow);

                await CreditRefundToUserWalletAsync(booking.UserId, booking.Id, booking.BookingCode, escrow.RefundAmount, cleanReason, nowUtc);
            }

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

            // Gửi thông báo hoàn tiền cho khách hàng
            await _unitOfWork.Repository<SystemNotification>().AddAsync(new SystemNotification
            {
                NotificationId = Guid.NewGuid(),
                UserId = booking.UserId,
                Title = "Chuyên gia không thể tiếp nhận lịch hẹn – Đã hoàn tiền 100%",
                Message = $"Chuyên gia đã từ chối lịch hẹn #{booking.BookingCode} (Lý do: {cleanReason}). Khoản tiền {booking.Fee:N0} VNĐ trong ví Escrow đã được hoàn trả 100%.",
                NotificationType = "BOOKING_REJECTED_AND_REFUNDED",
                IsRead = false,
                TargetActionUrl = "/experts",
                CreatedAt = nowUtc.UtcDateTime
            });

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Chuyên gia {ExpertUserId} đã từ chối booking {BookingCode}. Đã hoàn tiền 100% từ Escrow. Lý do: {Reason}",
                expertUserId, booking.BookingCode, cleanReason);

            return await GetBookingByIdAsync(booking.Id, expertUserId, "expert");
        }

        // ── 10. KHÁCH HÀNG HỦY LỊCH HẸN (CANCEL) ──────────────────────────────────────
        public async Task<BookingDetailResponse> CancelBookingAsync(Guid userId, Guid bookingId, string reason, string? cancelledBy = null)
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

            if (booking.Status == BookingStatus.IN_PROGRESS ||
                booking.Status == BookingStatus.COMPLETED ||
                booking.Status == BookingStatus.SETTLED ||
                booking.Status == BookingStatus.CANCELLED_BY_USER ||
                booking.Status == BookingStatus.CANCELLED_BY_EXPERT ||
                booking.Status == BookingStatus.EXPIRED_UNPAID ||
                booking.Status == BookingStatus.EXPIRED_NO_RESPONSE ||
                booking.Status == BookingStatus.REJECTED_BY_EXPERT)
            {
                throw new BadRequestException("CANNOT_CANCEL", $"Lịch hẹn đã ở trạng thái '{booking.Status}', không thể hủy.");
            }

            var previousStatus = booking.Status;
            var nowUtc = DateTimeOffset.UtcNow;
            var cleanReason = string.IsNullOrWhiteSpace(reason) ? "Khách hàng chủ động hủy." : reason.Trim();

            booking.Status = BookingStatus.CANCELLED_BY_USER;
            booking.CancelledBy = !string.IsNullOrWhiteSpace(cancelledBy) ? cancelledBy : "USER";
            booking.CancelledAt = nowUtc;
            booking.CancellationReason = cleanReason;
            booking.UpdatedAt = nowUtc;

            var slot = await _unitOfWork.Repository<ExpertSlot>().GetByIdAsync(booking.ExpertSlotId);
            var escrows = await _unitOfWork.Repository<EscrowTransaction>().FindAsync(e => e.BookingId == booking.Id);
            var escrow = escrows.FirstOrDefault();

            // Nếu đã thanh toán vào Escrow -> Tính toán tỷ lệ hoàn tiền theo chính sách sàn tư vấn (ConsultationPlatformPolicy)
            if (booking.PaidAt.HasValue)
            {
                var policy = await BookingHelper.GetPlatformPolicyAsync(_unitOfWork);
                var fullRefundHours = policy.CancelFullRefundHours;
                var partialRefundHours = policy.CancelPartialRefundHours;
                var partialRefundPercent = policy.CancelPartialRefundPercent;

                var slotStartUtc = slot != null
                    ? BookingHelper.ConvertSlotToUtc(slot.SlotDate, slot.StartTime)
                    : nowUtc;
                var hoursBeforeSlot = (slotStartUtc - nowUtc).TotalHours;

                // Nếu chuyên gia chưa xác nhận (AWAITING_EXPERT_APPROVAL) hoặc hủy sớm >= CANCEL_FULL_REFUND_HOURS (24h) -> Hoàn 100%
                if (previousStatus == BookingStatus.AWAITING_EXPERT_APPROVAL || hoursBeforeSlot >= fullRefundHours)
                {
                    booking.RefundStatus = "REFUNDED";
                    booking.RefundedAt = nowUtc;

                    if (escrow != null)
                    {
                        escrow.EscrowStatus = EscrowStatus.Refunded;
                        escrow.RefundAmount = escrow.Amount;
                        escrow.RefundReason = $"Khách hàng hủy lịch hợp lệ (hoàn 100%): {cleanReason}";
                        escrow.RefundedAt = nowUtc;
                        escrow.UpdatedAt = nowUtc;
                        _unitOfWork.Repository<EscrowTransaction>().Update(escrow);
                    }
                }
                else if (hoursBeforeSlot >= partialRefundHours)
                {
                    // Hủy trong khoảng [CANCEL_PARTIAL_REFUND_HOURS, CANCEL_FULL_REFUND_HOURS) -> Hoàn một phần (mặc định 50%)
                    booking.RefundStatus = "REFUNDED";
                    booking.RefundedAt = nowUtc;

                    if (escrow != null)
                    {
                        var refundAmt = Math.Round(escrow.Amount * (partialRefundPercent / 100m), 2);
                        escrow.EscrowStatus = EscrowStatus.PartiallyRefunded;
                        escrow.RefundAmount = refundAmt;
                        escrow.RefundReason = $"Khách hàng hủy lịch cận giờ (hoàn {partialRefundPercent}%): {cleanReason}";
                        escrow.RefundedAt = nowUtc;
                        escrow.UpdatedAt = nowUtc;
                        _unitOfWork.Repository<EscrowTransaction>().Update(escrow);
                    }
                }
                else
                {
                    // Hủy sát giờ (< CANCEL_PARTIAL_REFUND_HOURS) đối với lịch đã CONFIRMED -> Không hoàn tiền
                    booking.RefundStatus = "NONE";
                    if (escrow != null)
                    {
                        escrow.RefundAmount = 0m;
                        escrow.RefundReason = $"Khách hàng hủy lịch quá sát giờ hẹn (< {partialRefundHours} giờ, không hoàn tiền): {cleanReason}";
                        escrow.UpdatedAt = nowUtc;
                        _unitOfWork.Repository<EscrowTransaction>().Update(escrow);
                    }
                }
            }
            else if (escrow != null && escrow.EscrowStatus == EscrowStatus.PendingPayment)
            {
                escrow.EscrowStatus = EscrowStatus.Cancelled;
                escrow.RefundReason = "Khách hàng hủy lịch trước khi thanh toán.";
                escrow.UpdatedAt = nowUtc;
                _unitOfWork.Repository<EscrowTransaction>().Update(escrow);
            }

            // Giải phóng slot
            if (slot != null)
            {
                slot.IsBooked = false;
                slot.HoldExpiresAt = null;
                slot.HoldUserId = null;
                slot.UpdatedAt = nowUtc;
                _unitOfWork.Repository<ExpertSlot>().Update(slot);
            }

            if (escrow != null && escrow.RefundAmount > 0)
            {
                await CreditRefundToUserWalletAsync(booking.UserId, booking.Id, booking.BookingCode, escrow.RefundAmount, cleanReason, nowUtc);
            }

            _unitOfWork.Repository<Booking>().Update(booking);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("User {UserId} đã hủy booking {BookingCode} (RefundStatus = {RefundStatus}).",
                userId, booking.BookingCode, booking.RefundStatus);

            return await GetBookingByIdAsync(booking.Id, userId, "taxpayer");
        }

        // ── 10.B CHUYÊN GIA HỦY LỊCH HẸN ĐÃ XÁC NHẬN (EXPERT CANCEL) ───────────────
        public async Task<BookingDetailResponse> ExpertCancelBookingAsync(Guid expertUserId, Guid bookingId, string reason, string? cancelledBy = null)
        {
            var booking = await _unitOfWork.Repository<Booking>().GetByIdAsync(bookingId);
            if (booking == null)
            {
                throw new NotFoundException($"Không tìm thấy lịch hẹn với Id = {bookingId}.");
            }

            var slot = await _unitOfWork.Repository<ExpertSlot>().GetByIdAsync(booking.ExpertSlotId);
            var profile = slot != null ? await _unitOfWork.Repository<ExpertProfile>().GetByIdAsync(slot.ExpertProfileId) : null;

            if (profile == null || profile.UserId != expertUserId)
            {
                throw new ForbiddenException("Bạn không phải chuyên gia phụ trách ca tư vấn này.");
            }

            if (booking.Status != BookingStatus.CONFIRMED)
            {
                throw new BadRequestException("CANNOT_CANCEL", $"Chỉ có thể hủy ca tư vấn đang ở trạng thái đã xác nhận (CONFIRMED). Hiện tại: '{booking.Status}'.");
            }

            var nowUtc = DateTimeOffset.UtcNow;
            var cleanReason = string.IsNullOrWhiteSpace(reason) ? "Chuyên gia hủy lịch do sự cố đột xuất." : reason.Trim();

            booking.Status = BookingStatus.CANCELLED_BY_EXPERT;
            booking.CancelledBy = !string.IsNullOrWhiteSpace(cancelledBy) ? cancelledBy : "EXPERT";
            booking.CancelledAt = nowUtc;
            booking.CancellationReason = cleanReason;
            booking.RefundStatus = "REFUNDED";
            booking.RefundedAt = nowUtc;
            booking.UpdatedAt = nowUtc;

            // 1. Hoàn tiền 100% cho người nộp thuế
            var escrows = await _unitOfWork.Repository<EscrowTransaction>().FindAsync(e => e.BookingId == booking.Id);
            var escrow = escrows.FirstOrDefault();
            if (escrow != null)
            {
                escrow.EscrowStatus = EscrowStatus.Refunded;
                escrow.RefundAmount = escrow.Amount;
                escrow.RefundReason = $"Chuyên gia hủy lịch hẹn: {cleanReason}";
                escrow.RefundedAt = nowUtc;
                escrow.UpdatedAt = nowUtc;
                _unitOfWork.Repository<EscrowTransaction>().Update(escrow);

                await CreditRefundToUserWalletAsync(booking.UserId, booking.Id, booking.BookingCode, escrow.RefundAmount, cleanReason, nowUtc);
            }

            // 2. Kiểm tra phạt hủy sát giờ (< ExpertLateCancelHours, mặc định 12 giờ) từ chính sách sàn ConsultationPlatformPolicy
            var policy = await BookingHelper.GetPlatformPolicyAsync(_unitOfWork);
            var lateCancelHours = policy.ExpertLateCancelHours;
            var penaltyVnd = policy.ExpertLateCancelPenaltyVnd;

            var slotStartUtc = slot != null ? BookingHelper.ConvertSlotToUtc(slot.SlotDate, slot.StartTime) : nowUtc;
            var hoursBefore = (slotStartUtc - nowUtc).TotalHours;

            if (hoursBefore < lateCancelHours && penaltyVnd > 0)
            {
                var expertWallets = await _unitOfWork.Repository<UserWallet>().FindAsync(w => w.UserId == expertUserId && w.WalletType == "EXPERT");
                var expertWallet = expertWallets.FirstOrDefault();
                if (expertWallet == null)
                {
                    expertWallet = new UserWallet
                    {
                        Id = Guid.NewGuid(),
                        UserId = expertUserId,
                        WalletType = "EXPERT",
                        AvailableBalance = 0m,
                        CreatedAt = nowUtc,
                        UpdatedAt = nowUtc
                    };
                    await _unitOfWork.Repository<UserWallet>().AddAsync(expertWallet);
                }

                var beforeBal = expertWallet.AvailableBalance;
                expertWallet.AvailableBalance = Math.Max(0m, expertWallet.AvailableBalance - penaltyVnd);
                expertWallet.UpdatedAt = nowUtc;
                _unitOfWork.Repository<UserWallet>().Update(expertWallet);

                await _unitOfWork.Repository<WalletTransaction>().AddAsync(new WalletTransaction
                {
                    Id = Guid.NewGuid(),
                    WalletId = expertWallet.Id,
                    UserId = expertUserId,
                    Amount = -penaltyVnd,
                    BalanceBefore = beforeBal,
                    BalanceAfter = expertWallet.AvailableBalance,
                    TransactionType = "PENALTY",
                    ReferenceId = booking.Id,
                    Description = $"Phạt vi phạm hủy lịch sát giờ hẹn (< {lateCancelHours} giờ) ca #{booking.BookingCode}",
                    CreatedAt = nowUtc
                });

                _logger.LogWarning("Chuyên gia {ExpertUserId} hủy sát giờ booking {BookingCode} ({Hours}h trước ca). Áp dụng phạt {Penalty} VNĐ.",
                    expertUserId, booking.BookingCode, hoursBefore, penaltyVnd);
            }

            // 3. Giải phóng slot
            if (slot != null)
            {
                slot.IsBooked = false;
                slot.HoldExpiresAt = null;
                slot.HoldUserId = null;
                slot.UpdatedAt = nowUtc;
                _unitOfWork.Repository<ExpertSlot>().Update(slot);
            }

            _unitOfWork.Repository<Booking>().Update(booking);

            // 4. Gửi thông báo cho khách hàng
            await _unitOfWork.Repository<SystemNotification>().AddAsync(new SystemNotification
            {
                NotificationId = Guid.NewGuid(),
                UserId = booking.UserId,
                Title = "Chuyên gia đã hủy lịch hẹn – Hoàn tiền 100%",
                Message = $"Chuyên gia đã hủy lịch hẹn #{booking.BookingCode} (Lý do: {cleanReason}). Khoản tiền {booking.Fee:N0} VNĐ đã được hoàn trả lại cho bạn.",
                NotificationType = "BOOKING_CANCELLED_BY_EXPERT",
                IsRead = false,
                TargetActionUrl = "/experts",
                CreatedAt = nowUtc.UtcDateTime
            });

            await _unitOfWork.SaveChangesAsync();

            return await GetBookingByIdAsync(booking.Id, expertUserId, "expert");
        }

        private async Task CreditRefundToUserWalletAsync(Guid userId, Guid bookingId, string bookingCode, decimal refundAmount, string reason, DateTimeOffset nowUtc)
        {
            if (refundAmount <= 0) return;

            var wallets = await _unitOfWork.Repository<UserWallet>().FindAsync(w => w.UserId == userId && w.WalletType == "USER");
            var userWallet = wallets.FirstOrDefault();
            if (userWallet == null)
            {
                userWallet = new UserWallet
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    WalletType = "USER",
                    AvailableBalance = 0m,
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc
                };
                await _unitOfWork.Repository<UserWallet>().AddAsync(userWallet);
            }

            var before = userWallet.AvailableBalance;
            userWallet.AvailableBalance += refundAmount;
            userWallet.UpdatedAt = nowUtc;
            _unitOfWork.Repository<UserWallet>().Update(userWallet);

            await _unitOfWork.Repository<WalletTransaction>().AddAsync(new WalletTransaction
            {
                Id = Guid.NewGuid(),
                WalletId = userWallet.Id,
                UserId = userId,
                Amount = refundAmount,
                BalanceBefore = before,
                BalanceAfter = userWallet.AvailableBalance,
                TransactionType = "REFUND",
                ReferenceId = bookingId,
                Description = $"Hoàn tiền ca tư vấn #{bookingCode} ({reason})",
                CreatedAt = nowUtc
            });
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
