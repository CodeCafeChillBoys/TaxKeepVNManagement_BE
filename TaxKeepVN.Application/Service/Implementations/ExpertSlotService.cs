using Microsoft.Extensions.Logging;
using TaxKeepVN.Application.DTOs.Experts;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Helpers;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    public class ExpertSlotService : IExpertSlotService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ExpertSlotService> _logger;

        public ExpertSlotService(IUnitOfWork unitOfWork, ILogger<ExpertSlotService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        private async Task<ExpertProfile> GetExpertProfileAsync(Guid expertUserId)
        {
            var profile = (await _unitOfWork.Repository<ExpertProfile>().FindAsync(p => p.UserId == expertUserId)).FirstOrDefault();
            if (profile == null)
            {
                throw new ForbiddenException("Tài khoản chưa có hồ sơ chuyên gia hoặc không có quyền quản lý lịch làm việc.");
            }
            return profile;
        }

        private static bool HasOverlap(TimeOnly start1, TimeOnly end1, TimeOnly start2, TimeOnly end2)
        {
            return start1 < end2 && end1 > start2;
        }

        private static ExpertSlotDetailResponse MapToDetailResponse(ExpertSlot slot)
        {
            var nowUtc = DateTimeOffset.UtcNow;
            var isHolding = slot.HoldExpiresAt.HasValue && slot.HoldExpiresAt.Value > nowUtc;
            var durationMinutes = (int)(slot.EndTime - slot.StartTime).TotalMinutes;

            return new ExpertSlotDetailResponse
            {
                Id = slot.Id,
                ExpertProfileId = slot.ExpertProfileId,
                SlotDate = slot.SlotDate,
                StartTime = slot.StartTime,
                EndTime = slot.EndTime,
                DurationMinutes = durationMinutes,
                SessionType = slot.SessionType,
                IsBooked = slot.IsBooked,
                IsActive = slot.IsActive,
                HoldExpiresAt = slot.HoldExpiresAt,
                IsHolding = isHolding,
                CreatedAt = slot.CreatedAt,
                UpdatedAt = slot.UpdatedAt
            };
        }

        // ── 1. LẤY DANH SÁCH KHUNG GIỜ CỦA CHUYÊN GIA ────────────────────────────────
        public async Task<List<ExpertSlotDetailResponse>> GetMySlotsAsync(Guid expertUserId, ExpertSlotFilterParameters filters)
        {
            var profile = await GetExpertProfileAsync(expertUserId);

            var query = (await _unitOfWork.Repository<ExpertSlot>().FindAsync(s => s.ExpertProfileId == profile.Id)).AsQueryable();

            if (filters.FromDate.HasValue)
            {
                query = query.Where(s => s.SlotDate >= filters.FromDate.Value);
            }

            if (filters.ToDate.HasValue)
            {
                query = query.Where(s => s.SlotDate <= filters.ToDate.Value);
            }

            if (filters.SessionType.HasValue)
            {
                query = query.Where(s => s.SessionType == filters.SessionType.Value);
            }

            if (filters.IsBooked.HasValue)
            {
                query = query.Where(s => s.IsBooked == filters.IsBooked.Value);
            }

            if (filters.IsActive.HasValue)
            {
                query = query.Where(s => s.IsActive == filters.IsActive.Value);
            }

            var slots = query
                .OrderBy(s => s.SlotDate)
                .ThenBy(s => s.StartTime)
                .ToList();

            return slots.Select(MapToDetailResponse).ToList();
        }

        // ── 2. XEM CHI TIẾT 1 KHUNG GIỜ ──────────────────────────────────────────────
        public async Task<ExpertSlotDetailResponse> GetSlotByIdAsync(Guid expertUserId, Guid slotId)
        {
            var profile = await GetExpertProfileAsync(expertUserId);

            var slot = await _unitOfWork.Repository<ExpertSlot>().GetByIdAsync(slotId);
            if (slot == null)
            {
                throw new NotFoundException($"Không tìm thấy khung giờ làm việc với Id = {slotId}.");
            }

            if (slot.ExpertProfileId != profile.Id)
            {
                throw new ForbiddenException("Bạn không có quyền xem khung giờ làm việc của chuyên gia khác.");
            }

            return MapToDetailResponse(slot);
        }

        // ── 3. TẠO MỚI 1 KHUNG GIỜ ───────────────────────────────────────────────────
        public async Task<ExpertSlotDetailResponse> CreateSlotAsync(Guid expertUserId, CreateExpertSlotRequest request)
        {
            var profile = await GetExpertProfileAsync(expertUserId);
            // 1. Đọc chính sách sàn hiện hành từ bảng consultation_platform_policies
            var policy = await BookingHelper.GetPlatformPolicyAsync(_unitOfWork);
            // Kiểm tra trùng giờ với các slot sẵn có của chuyên gia
            var existingSlots = (await _unitOfWork.Repository<ExpertSlot>().FindAsync(s =>
                s.ExpertProfileId == profile.Id &&
                s.SlotDate == request.SlotDate
            )).ToList();

            // 3. VALIDATE: Kiểm tra không vượt quá số ca tối đa trong ngày (BR-8.1: ExpertMaxDailySlots = 6)
            if (existingSlots.Count >= policy.ExpertMaxDailySlots)
            {
                throw new BadRequestException("MAX_DAILY_SLOTS_EXCEEDED",
                    $"Bạn đã đạt giới hạn tối đa {policy.ExpertMaxDailySlots} khung giờ trong ngày {request.SlotDate:dd/MM/yyyy} theo chính sách của sàn.");
            }

            var conflict = existingSlots.FirstOrDefault(s => HasOverlap(s.StartTime, s.EndTime, request.StartTime, request.EndTime));
            if (conflict != null)
            {
                throw new ConflictException("SLOT_OVERLAP",
                    $"Khung giờ {request.StartTime:HH\\:mm} - {request.EndTime:HH\\:mm} ngày {request.SlotDate:dd/MM/yyyy} bị trùng hoặc đè lên khung giờ đã có ({conflict.StartTime:HH\\:mm} - {conflict.EndTime:HH\\:mm}).");
            }

            var nowUtc = DateTimeOffset.UtcNow;
            var slot = new ExpertSlot
            {
                Id = Guid.NewGuid(),
                ExpertProfileId = profile.Id,
                SlotDate = request.SlotDate,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                SessionType = request.SessionType,
                IsBooked = false,
                IsActive = true,
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc
            };

            await _unitOfWork.Repository<ExpertSlot>().AddAsync(slot);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Chuyên gia {ProfileId} đã tạo khung giờ mới {SlotId} vào ngày {SlotDate} ({StartTime}-{EndTime})",
                profile.Id, slot.Id, slot.SlotDate, slot.StartTime, slot.EndTime);

            return MapToDetailResponse(slot);
        }

        // ── 4. CẬP NHẬT KHUNG GIỜ ────────────────────────────────────────────────────
        public async Task<ExpertSlotDetailResponse> UpdateSlotAsync(Guid expertUserId, Guid slotId, UpdateExpertSlotRequest request)
        {
            var profile = await GetExpertProfileAsync(expertUserId);

            var slot = await _unitOfWork.Repository<ExpertSlot>().GetByIdAsync(slotId);
            if (slot == null)
            {
                throw new NotFoundException($"Không tìm thấy khung giờ làm việc với Id = {slotId}.");
            }

            if (slot.ExpertProfileId != profile.Id)
            {
                throw new ForbiddenException("Bạn không có quyền chỉnh sửa khung giờ của chuyên gia khác.");
            }

            if (slot.IsBooked)
            {
                throw new BadRequestException("SLOT_ALREADY_BOOKED", "Không thể chỉnh sửa khung giờ đã có khách hàng đặt lịch.");
            }

            var nowUtc = DateTimeOffset.UtcNow;
            if (slot.HoldExpiresAt.HasValue && slot.HoldExpiresAt.Value > nowUtc)
            {
                throw new ConflictException("SLOT_HOLDING", "Khung giờ đang có khách hàng tạm giữ để thanh toán. Vui lòng chờ phiên giữ chỗ kết thúc.");
            }

            // Nếu chuyên gia đổi ngày sang ngày khác -> Kiểm tra ngày mới có bị quá số ca tối đa không
            if (slot.SlotDate != request.SlotDate)
            {
                var policy = await BookingHelper.GetPlatformPolicyAsync(_unitOfWork);
                var targetDateSlotsCount = (await _unitOfWork.Repository<ExpertSlot>().FindAsync(s =>
                    s.ExpertProfileId == profile.Id &&
                    s.SlotDate == request.SlotDate &&
                    s.Id != slotId
                )).Count();

                if (targetDateSlotsCount >= policy.ExpertMaxDailySlots)
                {
                    throw new BadRequestException("MAX_DAILY_SLOTS_EXCEEDED",
                        $"Bạn đã đạt giới hạn tối đa {policy.ExpertMaxDailySlots} khung giờ trong ngày {request.SlotDate:dd/MM/yyyy} theo chính sách của sàn.");
                }
            }

            // Kiểm tra trùng giờ với các slot khác của chuyên gia
            var existingSlots = (await _unitOfWork.Repository<ExpertSlot>().FindAsync(s =>
                s.ExpertProfileId == profile.Id &&
                s.SlotDate == request.SlotDate &&
                s.Id != slotId
            )).ToList();

            var conflict = existingSlots.FirstOrDefault(s => HasOverlap(s.StartTime, s.EndTime, request.StartTime, request.EndTime));
            if (conflict != null)
            {
                throw new ConflictException("SLOT_OVERLAP",
                    $"Khung giờ {request.StartTime:HH\\:mm} - {request.EndTime:HH\\:mm} ngày {request.SlotDate:dd/MM/yyyy} bị trùng với khung giờ khác ({conflict.StartTime:HH\\:mm} - {conflict.EndTime:HH\\:mm}).");
            }

            slot.SlotDate = request.SlotDate;
            slot.StartTime = request.StartTime;
            slot.EndTime = request.EndTime;
            slot.SessionType = request.SessionType;
            slot.IsActive = request.IsActive;
            slot.UpdatedAt = nowUtc;

            _unitOfWork.Repository<ExpertSlot>().Update(slot);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Chuyên gia {ProfileId} đã cập nhật khung giờ {SlotId}.", profile.Id, slot.Id);

            return MapToDetailResponse(slot);
        }

        // ── 6. BẬT / TẮT NHANH TRẠNG THÁI HOẠT ĐỘNG (TOGGLE ACTIVE) ──────────────────
        public async Task<ExpertSlotDetailResponse> ToggleSlotActiveAsync(Guid expertUserId, Guid slotId)
        {
            var profile = await GetExpertProfileAsync(expertUserId);

            var slot = await _unitOfWork.Repository<ExpertSlot>().GetByIdAsync(slotId);
            if (slot == null)
            {
                throw new NotFoundException($"Không tìm thấy khung giờ làm việc với Id = {slotId}.");
            }

            if (slot.ExpertProfileId != profile.Id)
            {
                throw new ForbiddenException("Bạn không có quyền thao tác trên khung giờ của chuyên gia khác.");
            }

            if (slot.IsBooked)
            {
                throw new BadRequestException("SLOT_ALREADY_BOOKED", "Không thể tắt trạng thái của khung giờ đã được khách hàng đặt.");
            }

            var nowUtc = DateTimeOffset.UtcNow;
            if (slot.HoldExpiresAt.HasValue && slot.HoldExpiresAt.Value > nowUtc)
            {
                throw new ConflictException("SLOT_HOLDING", "Khung giờ đang có khách hàng tạm giữ để thanh toán. Vui lòng chờ phiên giữ chỗ kết thúc.");
            }

            slot.IsActive = !slot.IsActive;
            slot.UpdatedAt = nowUtc;

            _unitOfWork.Repository<ExpertSlot>().Update(slot);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Chuyên gia {ProfileId} đã chuyển trạng thái khung giờ {SlotId} sang IsActive = {IsActive}.",
                profile.Id, slot.Id, slot.IsActive);

            return MapToDetailResponse(slot);
        }

        // ── 7. XÓA KHUNG GIỜ ─────────────────────────────────────────────────────────
        public async Task DeleteSlotAsync(Guid expertUserId, Guid slotId)
        {
            var profile = await GetExpertProfileAsync(expertUserId);

            var slot = await _unitOfWork.Repository<ExpertSlot>().GetByIdAsync(slotId);
            if (slot == null)
            {
                throw new NotFoundException($"Không tìm thấy khung giờ làm việc với Id = {slotId}.");
            }

            if (slot.ExpertProfileId != profile.Id)
            {
                throw new ForbiddenException("Bạn không có quyền xóa khung giờ của chuyên gia khác.");
            }

            if (slot.IsBooked)
            {
                throw new BadRequestException("SLOT_ALREADY_BOOKED", "Không thể xóa khung giờ đã có khách hàng đặt lịch.");
            }

            var nowUtc = DateTimeOffset.UtcNow;
            if (slot.HoldExpiresAt.HasValue && slot.HoldExpiresAt.Value > nowUtc)
            {
                throw new ConflictException("SLOT_HOLDING", "Khung giờ đang có khách hàng tạm giữ để thanh toán. Vui lòng chờ phiên giữ chỗ kết thúc.");
            }

            _unitOfWork.Repository<ExpertSlot>().Remove(slot);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Chuyên gia {ProfileId} đã xóa thành công khung giờ {SlotId}.", profile.Id, slot.Id);
        }

        public async Task<RecurringSlotBatchResultResponse> CreateRecurringSlotsAsync(Guid expertUserId, CreateRecurringSlotRequest request)
        {
            var profile = await GetExpertProfileAsync(expertUserId);
            var policy = await BookingHelper.GetPlatformPolicyAsync(_unitOfWork);

            var startMonday = GetMondayOfWeek(request.StartDate);
            var maxEndDate = startMonday.AddDays(request.NumberOfWeeks * 7);

            // Lấy trước toàn bộ slot đã có trong khoảng thời gian này để tối ưu query
            var existingSlots = (await _unitOfWork.Repository<ExpertSlot>().FindAsync(s =>
                s.ExpertProfileId == profile.Id &&
                s.SlotDate >= request.StartDate &&
                s.SlotDate <= maxEndDate
            )).ToList();

            var newSlots = new List<ExpertSlot>();
            int totalSkipped = 0;
            var nowUtc = DateTimeOffset.UtcNow;
            var nowVietnam = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7));
            var todayVietnam = DateOnly.FromDateTime(nowVietnam.DateTime);
            var nowTimeVietnam = TimeOnly.FromDateTime(nowVietnam.DateTime);

            for (int w = 0; w < request.NumberOfWeeks; w++)
            {
                // tuần bắt đầu tại thứ tuần 0 , tuần 1 , tuần 2
                var currentWeekMonday = startMonday.AddDays(w * 7);
                // duyệt qua thứ mấy trong tuần đâu tiên
                foreach (var template in request.WeeklySlots)
                {
                    // Tính ngày thực tế dựa trên DayOfWeek (Thứ 2 = 1, Thứ 3 = 2, ..., Chủ Nhật = 0)
                    int dayOffset = ((int)template.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
                    // tuần thứ mấy vào ngày thứ mấy
                    var targetDate = currentWeekMonday.AddDays(dayOffset);
                    // Bỏ qua nếu ngày này nhỏ hơn ngày bắt đầu chỉ định
                    if (targetDate < request.StartDate)
                    {
                        continue;
                    }
                    // Bỏ qua nếu rơi vào ngày hôm nay nhưng giờ bắt đầu đã trôi qua
                    if (targetDate == todayVietnam && template.StartTime <= nowTimeVietnam)
                    {
                        totalSkipped++;
                        continue;
                    }
                    // 1. Kiểm tra giới hạn số ca tối đa trong ngày (policy.ExpertMaxDailySlots = 6)
                    var currentDayCount = existingSlots.Count(s => s.SlotDate == targetDate)
                                        + newSlots.Count(s => s.SlotDate == targetDate);
                    if (currentDayCount >= policy.ExpertMaxDailySlots)
                    {
                        totalSkipped++;
                        continue;
                    }
                    // 2. Kiểm tra chống trùng giờ với slot đã có sẵn hoặc slot vừa tạo trong cùng batch
                    var hasConflict = existingSlots.Any(s => s.SlotDate == targetDate && HasOverlap(s.StartTime, s.EndTime, template.StartTime, template.EndTime))
                                   || newSlots.Any(s => s.SlotDate == targetDate && HasOverlap(s.StartTime, s.EndTime, template.StartTime, template.EndTime));
                    if (hasConflict)
                    {
                        totalSkipped++;
                        continue;
                    }
                    // 3. Khởi tạo slot hợp lệ
                    var slot = new ExpertSlot
                    {
                        Id = Guid.NewGuid(),
                        ExpertProfileId = profile.Id,
                        SlotDate = targetDate,
                        StartTime = template.StartTime,
                        EndTime = template.EndTime,
                        SessionType = template.SessionType,
                        IsBooked = false,
                        IsActive = true,
                        CreatedAt = nowUtc,
                        UpdatedAt = nowUtc
                    };
                    newSlots.Add(slot);
                }
            }
            // Lưu hàng loạt các slot mới
            foreach (var slot in newSlots)
            {
                await _unitOfWork.Repository<ExpertSlot>().AddAsync(slot);
            }
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Chuyên gia {ProfileId} đã tạo hàng loạt {Count} khung giờ làm việc định kỳ ({Skipped} bị bỏ qua).",
                profile.Id, newSlots.Count, totalSkipped);
            return new RecurringSlotBatchResultResponse
            {
                TotalCreated = newSlots.Count,
                TotalSkipped = totalSkipped,
                FromDate = request.StartDate,
                ToDate = newSlots.Any() ? newSlots.Max(s => s.SlotDate) : request.StartDate,
                CreatedSlots = newSlots.OrderBy(s => s.SlotDate).ThenBy(s => s.StartTime).Select(MapToDetailResponse).ToList()
            };

        }

        private static DateOnly GetMondayOfWeek(DateOnly date)
        {
            int diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            return date.AddDays(-diff);
        }

        public async Task<EmergencyCancelSlotResponse> EmergencyCancelSlotAsync(Guid expertUserId, Guid slotId, EmergencyCancelSlotRequest request, string? cancelledBy = null)
        {
            var profile = await GetExpertProfileAsync(expertUserId);
            var slot = await _unitOfWork.Repository<ExpertSlot>().GetByIdAsync(slotId);
            if (slot == null)
            {
                throw new NotFoundException($"Không tìm thấy khung giờ với Id = {slotId}.");
            }

            if (slot.ExpertProfileId != profile.Id)
            {
                throw new ForbiddenException("Bạn không có quyền thao tác trên khung giờ của chuyên gia khác.");
            }

            var cleanReason = request.Reason?.Trim() ?? string.Empty;
            if (cleanReason.Length < 10)
            {
                throw new BadRequestException("INVALID_REASON", "Vui lòng nhập lý do hủy chi tiết (tối thiểu 10 ký tự).");
            }

            var nowUtc = DateTimeOffset.UtcNow;
            var slotStartUtc = BookingHelper.ConvertSlotToUtc(slot.SlotDate, slot.StartTime);
            // Kiểm tra xem khung giờ này đã trôi qua ở quá khứ chưa
            if (slotStartUtc <= nowUtc)
            {
                throw new BadRequestException("SLOT_EXPIRED", "Không thể hủy khung giờ đã hoặc đang diễn ra.");
            }

            var policy = await BookingHelper.GetPlatformPolicyAsync(_unitOfWork);
            var response = new EmergencyCancelSlotResponse
            {
                SlotId = slot.Id,
                SlotDate = slot.SlotDate,
                StartTime = slot.StartTime,
                EndTime = slot.EndTime
            };
            await _unitOfWork.BeginTransactionAsync();

            try
            {
                // ── TRƯỜNG HỢP 1: KHUNG GIỜ ĐÃ CÓ NGƯỜI ĐẶT ───────────────────────────
                if (slot.IsBooked)
                {
                    // Tìm lịch hẹn đang có hiệu lực gắn với slot này
                    var bookings = await _unitOfWork.Repository<Booking>().FindAsync(b =>
                        b.ExpertSlotId == slot.Id &&
                        (b.Status == BookingStatus.CONFIRMED || b.Status == BookingStatus.AWAITING_EXPERT_APPROVAL)
                    );
                    var booking = bookings.FirstOrDefault();
                    if (booking != null)
                    {
                        response.HadBooking = true;
                        response.BookingId = booking.Id;
                        response.BookingCode = booking.BookingCode;
                        // 1. Kiểm tra chính sách Hủy muộn (Sát giờ < ExpertLateCancelHours, mặc định 12h)
                        var hoursBeforeSlot = (slotStartUtc - nowUtc).TotalHours;
                        var isLate = hoursBeforeSlot < policy.ExpertLateCancelHours;
                        response.IsLateCancel = isLate;
                        response.PenaltyAmount = isLate ? policy.ExpertLateCancelPenaltyVnd : 0m;
                        // 2. Cập nhật trạng thái Booking: CANCELLED_BY_EXPERT
                        booking.Status = BookingStatus.CANCELLED_BY_EXPERT;
                        booking.CancelledBy = !string.IsNullOrWhiteSpace(cancelledBy) ? cancelledBy : "EXPERT";
                        booking.CancellationReason = cleanReason;
                        booking.CancelledAt = nowUtc;
                        booking.RefundStatus = "REFUNDED";
                        booking.RefundedAt = nowUtc;
                        booking.UpdatedAt = nowUtc;
                        _unitOfWork.Repository<Booking>().Update(booking);
                        // 3. Hoàn tiền 100% từ Escrow cho khách hàng (Khách hàng không có lỗi)
                        var escrows = await _unitOfWork.Repository<EscrowTransaction>().FindAsync(e => e.BookingId == booking.Id);
                        var escrow = escrows.FirstOrDefault();
                        if (escrow != null && escrow.EscrowStatus != EscrowStatus.Refunded)
                        {
                            escrow.EscrowStatus = EscrowStatus.Refunded;
                            escrow.RefundAmount = escrow.Amount;
                            escrow.RefundReason = $"Chuyên gia hủy lịch khẩn cấp: {cleanReason}";
                            escrow.RefundedAt = nowUtc;
                            escrow.UpdatedAt = nowUtc;
                            _unitOfWork.Repository<EscrowTransaction>().Update(escrow);
                        }
                        // 4. Gửi thông báo khẩn cấp cho KHÁCH HÀNG
                        await _unitOfWork.Repository<SystemNotification>().AddAsync(new SystemNotification
                        {
                            NotificationId = Guid.NewGuid(),
                            UserId = booking.UserId,
                            Title = "Lịch hẹn đã bị hủy do chuyên gia gặp sự cố khẩn cấp – Đã hoàn tiền 100%",
                            Message = $"Chuyên gia đã hủy ca tư vấn #{booking.BookingCode} vào ngày {slot.SlotDate:dd/MM/yyyy} ({slot.StartTime:HH\\:mm} - {slot.EndTime:HH\\:mm}) do: {cleanReason}. Số tiền {booking.Fee:N0} VNĐ đã được hoàn trả 100%.",
                            NotificationType = "BOOKING_CANCELLED_BY_EXPERT",
                            IsRead = false,
                            TargetActionUrl = "/bookings",
                            CreatedAt = nowUtc.UtcDateTime
                        });
                        // 5. Gửi thông báo cho CHUYÊN GIA (kèm thông tin phạt nếu có)
                        var penaltyNotice = isLate
                            ? $" Bạn đã hủy sát giờ (< {policy.ExpertLateCancelHours} tiếng) nên bị phạt {policy.ExpertLateCancelPenaltyVnd:N0} VNĐ theo quy định sàn."
                            : " Bạn đã hủy trước thời hạn quy định nên không bị phạt phí.";
                        await _unitOfWork.Repository<SystemNotification>().AddAsync(new SystemNotification
                        {
                            NotificationId = Guid.NewGuid(),
                            UserId = expertUserId,
                            Title = "Xác nhận hủy ca tư vấn khẩn cấp",
                            Message = $"Bạn đã hủy thành công ca tư vấn #{booking.BookingCode}.{penaltyNotice}",
                            NotificationType = "EXPERT_EMERGENCY_CANCELLED",
                            IsRead = false,
                            TargetActionUrl = $"/expert/slots/{slot.Id}",
                            CreatedAt = nowUtc.UtcDateTime
                        });
                        response.Message = isLate
                            ? $"Hủy thành công ca tư vấn #{booking.BookingCode}. Khách được hoàn 100% tiền. Bạn bị phạt {policy.ExpertLateCancelPenaltyVnd:N0} VNĐ do hủy sát giờ."
                            : $"Hủy thành công ca tư vấn #{booking.BookingCode}. Khách được hoàn 100% tiền. Không phát sinh phí phạt.";
                    }
                }
                else
                {
                    // ── TRƯỜNG HỢP 2: SLOT CHƯA CÓ AI ĐẶT ──────────────────────────────
                    response.HadBooking = false;
                    response.IsLateCancel = false;
                    response.PenaltyAmount = 0m;
                    response.Message = "Đã khóa khung giờ thành công, không có khách hàng bị ảnh hưởng.";
                }
                // Tắt khung giờ để không ai thấy hoặc đặt nữa
                slot.IsActive = false;
                slot.IsBooked = false;
                slot.HoldExpiresAt = null;
                slot.HoldUserId = null;
                slot.UpdatedAt = nowUtc;
                _unitOfWork.Repository<ExpertSlot>().Update(slot);
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransactionAsync();
                _logger.LogWarning("Chuyên gia {ExpertUserId} đã hủy khẩn cấp slot {SlotId}. HadBooking: {HadBooking}, IsLate: {IsLate}, Penalty: {Penalty}",
                    expertUserId, slot.Id, response.HadBooking, response.IsLateCancel, response.PenaltyAmount);
                return response;
            }
            catch (Exception)
            {
                await _unitOfWork.RollbackTransactionAsync();
                throw;
            }


        }
    }
}
