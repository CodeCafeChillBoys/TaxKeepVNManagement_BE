using Microsoft.Extensions.Logging;
using TaxKeepVN.Application.DTOs.Experts;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
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

            // Kiểm tra trùng giờ với các slot sẵn có của chuyên gia
            var existingSlots = (await _unitOfWork.Repository<ExpertSlot>().FindAsync(s =>
                s.ExpertProfileId == profile.Id &&
                s.SlotDate == request.SlotDate
            )).ToList();

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
    }
}
