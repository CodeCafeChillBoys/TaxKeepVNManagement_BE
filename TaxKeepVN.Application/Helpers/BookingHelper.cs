using TaxKeepVN.Application.DTOs.Bookings;
using TaxKeepVN.Application.Mappers;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Helpers
{
    /// <summary>
    /// Các phương thức trợ giúp xử lý nghiệp vụ cho tính năng Đặt lịch tư vấn (Đặc tả 3)
    /// </summary>
    public static class BookingHelper
    {
        /// <summary>Thời gian đặt trước tối thiểu theo quy định nghiệp vụ (Minimum Lead-time: 4 giờ)</summary>
        public const int MinimumLeadTimeHours = 4;
        public const int DefaultHoldMinutes = 10;
        public const int DefaultApprovalHours = 2;

        /// <summary>
        /// Sinh mã định danh đặt lịch thân thiện với người dùng theo định dạng: BK-YYYYMMDD-XXXXXX
        /// </summary>
        public static string GenerateBookingCode()
        {
            var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
            var randomPart = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
            return $"BK-{datePart}-{randomPart}";
        }

        /// <summary>
        /// Lấy thời gian đặt trước tối thiểu (Minimum Lead-time) theo quy định nghiệp vụ (mặc định 4 tiếng)
        /// </summary>
        public static int GetMinimumLeadTimeHours() => MinimumLeadTimeHours;

        /// <summary>
        /// Kiểm tra xem người dùng hiện tại có lịch hẹn nào khác đang bị trùng khung giờ không (Đặc tả 8)
        /// </summary>
        public static async Task<bool> CheckUserSlotConflictAsync(
            IUnitOfWork unitOfWork,
            Guid userId,
            DateOnly slotDate,
            TimeOnly startTime,
            TimeOnly endTime)
        {
            var activeBookings = (await unitOfWork.Repository<Booking>().FindAsync(b =>
                b.UserId == userId &&
                (b.Status == BookingStatus.PENDING_PAYMENT ||
                 b.Status == BookingStatus.AWAITING_EXPERT_APPROVAL ||
                 b.Status == BookingStatus.CONFIRMED))).ToList();

            if (!activeBookings.Any()) return false;

            var slotIds = activeBookings.Select(b => b.ExpertSlotId).Distinct().ToList();
            var slots = (await unitOfWork.Repository<ExpertSlot>().FindAsync(s => slotIds.Contains(s.Id))).ToList();

            return slots.Any(s => s.SlotDate == slotDate && s.StartTime < endTime && s.EndTime > startTime);
        }

        /// <summary>
        /// Tính toán chi phí phiên tư vấn dựa trên biểu phí đã duyệt của chuyên gia hoặc cấu hình sàn của hệ thống
        /// </summary>
        public static async Task<decimal> CalculateConsultationFeeAsync(
            IUnitOfWork unitOfWork,
            ExpertProfile profile,
            SessionType sessionType,
            int durationMinutes)
        {
            var proposals = (await unitOfWork.Repository<ExpertApplicationFeeProposal>().FindAsync(f =>
                f.ApplicationId == profile.LatestApplicationId &&
                f.SessionType == sessionType)).ToList();

            var exactMatch = proposals.FirstOrDefault(p => p.DurationMinutes == durationMinutes);
            if (exactMatch != null)
            {
                return exactMatch.ProposedFee;
            }

            if (proposals.Any())
            {
                return proposals.OrderBy(p => Math.Abs(p.DurationMinutes - durationMinutes)).First().ProposedFee;
            }

            // Fallback: Lấy cấu hình tối thiểu của hệ thống
            var systemConfig = (await unitOfWork.Repository<ConsultationFeeConfiguration>().FindAsync(c =>
                c.SessionType == sessionType && c.IsActive)).FirstOrDefault();

            return systemConfig?.MinFee ?? 200000m;
        }

        /// <summary>
        /// Nạp các quan hệ dữ liệu liên quan và chuyển danh sách Booking entity sang BookingListItemResponse DTO
        /// </summary>
        public static async Task<List<BookingListItemResponse>> MapToListItemsAsync(
            IUnitOfWork unitOfWork,
            List<Booking> bookings)
        {
            if (!bookings.Any()) return new List<BookingListItemResponse>();

            var userIds = bookings.Select(b => b.UserId).Distinct().ToList();
            var users = (await unitOfWork.Repository<User>().FindAsync(u => userIds.Contains(u.UserId)))
                .ToDictionary(u => u.UserId);

            var slotIds = bookings.Select(b => b.ExpertSlotId).Distinct().ToList();
            var slots = (await unitOfWork.Repository<ExpertSlot>().FindAsync(s => slotIds.Contains(s.Id)))
                .ToDictionary(s => s.Id);

            var profileIds = slots.Values.Select(s => s.ExpertProfileId).Distinct().ToList();
            var profiles = (await unitOfWork.Repository<ExpertProfile>().FindAsync(p => profileIds.Contains(p.Id)))
                .ToDictionary(p => p.Id);

            var expertUserIds = profiles.Values.Select(p => p.UserId).Distinct().ToList();
            var expertUsers = (await unitOfWork.Repository<User>().FindAsync(u => expertUserIds.Contains(u.UserId)))
                .ToDictionary(u => u.UserId);

            var appIds = profiles.Values.Select(p => p.LatestApplicationId).Distinct().ToList();
            var apps = (await unitOfWork.Repository<ExpertApplication>().FindAsync(a => appIds.Contains(a.Id)))
                .ToDictionary(a => a.Id);

            var specIds = bookings.Select(b => b.SpecializationId).Distinct().ToList();
            var specs = (await unitOfWork.Repository<Specialization>().FindAsync(s => specIds.Contains(s.Id)))
                .ToDictionary(s => s.Id);

            var bookingIds = bookings.Select(b => b.Id).ToList();
            var attachments = (await unitOfWork.Repository<BookingAttachment>().FindAsync(a => bookingIds.Contains(a.BookingId))).ToList();

            return bookings.Select(b =>
            {
                users.TryGetValue(b.UserId, out var clientUser);
                slots.TryGetValue(b.ExpertSlotId, out var slot);
                ExpertProfile? profile = null;
                if (slot != null)
                {
                    profiles.TryGetValue(slot.ExpertProfileId, out profile);
                }
                specs.TryGetValue(b.SpecializationId, out var spec);

                User? expertUser = null;
                ExpertApplication? app = null;
                if (profile != null)
                {
                    expertUsers.TryGetValue(profile.UserId, out expertUser);
                    apps.TryGetValue(profile.LatestApplicationId, out app);
                }

                var attCount = attachments.Count(a => a.BookingId == b.Id);

                return b.ToListItemResponse(clientUser, profile, slot, spec, expertUser, app, attCount);
            }).ToList();
        }
    }
}
