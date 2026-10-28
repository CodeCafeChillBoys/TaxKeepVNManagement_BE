using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Bookings;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Mappers
{
    public static class BookingMapper
    {
        /// <summary>
        /// Chuyển đổi Booking entity và các đối tượng liên quan sang BookingDetailResponse
        /// </summary>
        public static BookingDetailResponse MapToDetailResponse(
            Booking booking,
            User clientUser,
            ExpertProfile expertProfile,
            ExpertSlot slot,
            Specialization spec,
            User? expertUser,
            ExpertApplication? app,
            ConsultationSession? session = null)
        {
            if (booking == null) return null!;
            var nowUtc = DateTimeOffset.UtcNow;
            var holdCountdown = (int)Math.Max(0, (booking.HoldExpiresAt - nowUtc).TotalSeconds);
            int? approvalCountdown = booking.ApprovalDeadline.HasValue
                ? (int)Math.Max(0, (booking.ApprovalDeadline.Value - nowUtc).TotalSeconds)
                : null;

            return new BookingDetailResponse
            {
                Id = booking.Id,
                BookingCode = booking.BookingCode,
                UserId = booking.UserId,
                UserFullName = clientUser.FullName,
                UserEmail = clientUser.Email,
                UserPhoneNumber = clientUser.PhoneNumber,

                ExpertProfileId = expertProfile.Id,
                ExpertFullName = app?.FullName ?? expertUser?.FullName ?? "Chuyên gia",
                ExpertAvatarUrl = app?.AvatarUrl,
                ExpertJobTitle = app?.JobTitle ?? "Chuyên gia tư vấn",

                ExpertSlotId = slot.Id,
                SlotDate = slot.SlotDate,
                StartTime = slot.StartTime,
                EndTime = slot.EndTime,
                SessionType = booking.SessionType,
                DurationMinutes = booking.DurationMinutes,
                Fee = booking.Fee,

                SpecializationId = spec.Id,
                SpecializationName = spec.Name,
                TopicTitle = booking.TopicTitle,
                ProblemDescription = booking.ProblemDescription,

                Status = booking.Status,
                HoldExpiresAt = booking.HoldExpiresAt,
                HoldCountdownSeconds = holdCountdown,
                ApprovalDeadline = booking.ApprovalDeadline,
                ApprovalCountdownSeconds = approvalCountdown,

                ApprovedAt = booking.ApprovedAt,
                RejectedAt = booking.RejectedAt,
                RejectionReason = booking.RejectionReason,

                PaidAt = booking.PaidAt,
                PaymentReference = booking.PaymentReference,
                RefundStatus = booking.RefundStatus,
                RefundedAt = booking.RefundedAt,

                CancelledAt = booking.CancelledAt,
                CancellationReason = booking.CancellationReason,
                CancelledBy = booking.CancelledBy,

                CreatedAt = booking.CreatedAt,

                NoShowType = session?.NoShowType ?? "NONE",
                SessionSharedNotes = session?.SharedNotes
            };
        }

        public static BookingDetailResponse ToDetailResponse(
            this Booking booking,
            User clientUser,
            ExpertProfile expertProfile,
            ExpertSlot slot,
            Specialization spec,
            User? expertUser,
            ExpertApplication? app,
            ConsultationSession? session = null)
            => MapToDetailResponse(booking, clientUser, expertProfile, slot, spec, expertUser, app, session);

        /// <summary>
        /// Chuyển đổi một Booking entity sang BookingListItemResponse
        /// </summary>
        public static BookingListItemResponse MapToListItemResponse(
            Booking booking,
            User? clientUser,
            ExpertProfile? profile,
            ExpertSlot? slot,
            Specialization? spec,
            User? expertUser,
            ExpertApplication? app,
            string noShowType = "NONE")
        {
            if (booking == null) return null!;

            return new BookingListItemResponse
            {
                Id = booking.Id,
                BookingCode = booking.BookingCode,
                UserId = booking.UserId,
                UserFullName = clientUser?.FullName ?? "Khách hàng",
                ExpertProfileId = profile?.Id ?? Guid.Empty,
                ExpertFullName = app?.FullName ?? expertUser?.FullName ?? "Chuyên gia",
                ExpertAvatarUrl = app?.AvatarUrl,
                SlotDate = slot?.SlotDate ?? DateOnly.MinValue,
                StartTime = slot?.StartTime ?? TimeOnly.MinValue,
                EndTime = slot?.EndTime ?? TimeOnly.MinValue,
                SessionType = booking.SessionType,
                DurationMinutes = booking.DurationMinutes,
                Fee = booking.Fee,
                SpecializationName = spec?.Name ?? string.Empty,
                TopicTitle = booking.TopicTitle,
                Status = booking.Status,
                HoldExpiresAt = booking.HoldExpiresAt,
                ApprovalDeadline = booking.ApprovalDeadline,
                CreatedAt = booking.CreatedAt,
                NoShowType = noShowType
            };
        }

        public static BookingListItemResponse ToListItemResponse(
            this Booking booking,
            User? clientUser,
            ExpertProfile? profile,
            ExpertSlot? slot,
            Specialization? spec,
            User? expertUser,
            ExpertApplication? app,
            string noShowType = "NONE")
            => MapToListItemResponse(booking, clientUser, profile, slot, spec, expertUser, app, noShowType);

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
            var sessions = (await unitOfWork.Repository<ConsultationSession>().FindAsync(s => bookingIds.Contains(s.BookingId)))
                .ToDictionary(s => s.BookingId);

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

                sessions.TryGetValue(b.Id, out var sess);
                var noShowType = sess?.NoShowType ?? "NONE";

                return MapToListItemResponse(b, clientUser, profile, slot, spec, expertUser, app, noShowType);
            }).ToList();
        }
    }
}
