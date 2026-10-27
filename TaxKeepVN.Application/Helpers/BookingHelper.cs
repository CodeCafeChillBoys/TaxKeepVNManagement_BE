using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Bookings;
using TaxKeepVN.Application.Mappers;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Helpers
{
    /// <summary>
    /// Helper xử lý các quy tắc nghiệp vụ cốt lõi của Đặt lịch tư vấn & Escrow (Đặc tả 3 & 4).
    /// ZERO-HARDCODE: Toàn bộ ngưỡng thời gian, hạn mức phí và tỷ lệ phần trăm hoa hồng
    /// đều đọc động từ bảng consultation_platform_policies trong CSDL qua GetPlatformPolicyAsync.
    /// </summary>
    public static class BookingHelper
    {
        /// <summary>Múi giờ chuẩn Việt Nam (UTC+7)</summary>
        public static readonly TimeSpan VietnamTimeZoneOffset = TimeSpan.FromHours(7);

        /// <summary>
        /// Chuyển đổi ngày và giờ của Slot (lưu theo giờ Việt Nam) sang thời điểm DateTimeOffset UTC để so sánh chuẩn xác
        /// </summary>
        public static DateTimeOffset ConvertSlotToUtc(DateOnly slotDate, TimeOnly startTime)
        {
            return new DateTimeOffset(slotDate.ToDateTime(startTime), VietnamTimeZoneOffset).ToUniversalTime();
        }

        /// <summary>
        /// Lấy thời điểm hiện tại theo giờ Việt Nam (UTC+7)
        /// </summary>
        public static DateTimeOffset GetVietnamNow()
        {
            return DateTimeOffset.UtcNow.ToOffset(VietnamTimeZoneOffset);
        }


        /// <summary>
        /// Lấy chính sách sàn tư vấn hiện hành từ bảng chuyên biệt consultation_platform_policies.
        /// </summary>
        public static async Task<ConsultationPlatformPolicy> GetPlatformPolicyAsync(IUnitOfWork unitOfWork)
        {
            var policy = (await unitOfWork.Repository<ConsultationPlatformPolicy>().FindAsync(p => p.IsActive))
                .OrderByDescending(p => p.Id)
                .FirstOrDefault();

            if (policy != null) return policy;

            policy = (await unitOfWork.Repository<ConsultationPlatformPolicy>().FindAsync(p => p.Id == 1)).FirstOrDefault();
            if (policy != null) return policy;

            throw new InvalidOperationException(
                "Lỗi cấu hình hệ thống: Không tìm thấy chính sách sàn tư vấn trong bảng consultation_platform_policies. Vui lòng kiểm tra dữ liệu cấu hình.");
        }


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
        /// Sinh mã đơn hàng số nguyên duy nhất (long) tương thích chuẩn PayOS (14 chữ số an toàn trong giới hạn Int64/IEEE-754)
        /// </summary>
        public static long GeneratePayOsOrderCode()
        {
            var timestampSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var randomSuffix = Random.Shared.Next(1000, 9999);
            return (timestampSeconds * 10000L) + randomSuffix;
        }

        /// <summary>
        /// Kiểm tra xem người dùng hiện tại có lịch hẹn nào khác đang bị trùng khung giờ không (Đặc tả 3 - BR-3.3)
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
                 b.Status == BookingStatus.CONFIRMED ||
                 b.Status == BookingStatus.IN_PROGRESS))).ToList();

            if (!activeBookings.Any()) return false;

            var slotIds = activeBookings.Select(b => b.ExpertSlotId).Distinct().ToList();
            var slots = (await unitOfWork.Repository<ExpertSlot>().FindAsync(s => slotIds.Contains(s.Id))).ToList();

            return slots.Any(s => s.SlotDate == slotDate && s.StartTime < endTime && s.EndTime > startTime);
        }

        /// <summary>
        /// Tính toán chi phí phiên tư vấn dựa trên biểu phí đã duyệt của chuyên gia hoặc cấu hình sàn động của hệ thống (Zero-Hardcode)
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

            // Lấy cấu hình phí theo loại phiên từ bảng consultation_fee_configurations
            var feeConfig = (await unitOfWork.Repository<ConsultationFeeConfiguration>().FindAsync(c =>
                c.SessionType == sessionType && c.IsActive))
                .OrderBy(c => Math.Abs(c.DurationMinutes - durationMinutes))
                .FirstOrDefault();

            if (feeConfig != null)
            {
                return feeConfig.MinFee;
            }

            // Đọc mức phí sàn từ bảng consultation_platform_policies (ExpertMinFeeVnd) — tuyệt đối không hardcode số tiền
            var policy = await GetPlatformPolicyAsync(unitOfWork);
            return policy.ExpertMinFeeVnd;
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


                return b.ToListItemResponse(clientUser, profile, slot, spec, expertUser, app);
            }).ToList();
        }

    }
}
