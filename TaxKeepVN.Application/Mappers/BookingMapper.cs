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
        /// Chuyển đổi BookingAttachment entity sang DTO
        /// </summary>
        public static BookingAttachmentDto ToDto(this BookingAttachment attachment)
        {
            if (attachment == null) return null!;

            return new BookingAttachmentDto
            {
                Id = attachment.Id,
                FileName = attachment.FileName,
                FileSize = attachment.FileSize,
                ContentType = attachment.ContentType,
                CreatedAt = attachment.CreatedAt
            };
        }

        public static List<BookingAttachmentDto> ToDtoList(this IEnumerable<BookingAttachment> attachments)
        {
            return attachments?.Select(a => a.ToDto()).ToList() ?? new List<BookingAttachmentDto>();
        }

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
            List<BookingAttachmentDto> attachments)
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
                UserFullName = clientUser?.FullName ?? "Khách hàng",
                UserEmail = clientUser?.Email ?? string.Empty,
                UserPhoneNumber = clientUser?.PhoneNumber,

                ExpertProfileId = expertProfile.Id,
                ExpertFullName = app?.FullName ?? expertUser?.FullName ?? "Chuyên gia",
                ExpertAvatarUrl = app?.AvatarUrl,
                ExpertJobTitle = expertProfile.JobTitle,

                ExpertSlotId = slot.Id,
                SlotDate = slot.SlotDate,
                StartTime = slot.StartTime,
                EndTime = slot.EndTime,
                SessionType = booking.SessionType,
                DurationMinutes = booking.DurationMinutes,
                Fee = booking.Fee,

                SpecializationId = spec?.Id ?? 0,
                SpecializationName = spec?.Name ?? string.Empty,
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
                Attachments = attachments ?? new List<BookingAttachmentDto>()
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
            List<BookingAttachmentDto> attachments)
            => MapToDetailResponse(booking, clientUser, expertProfile, slot, spec, expertUser, app, attachments);

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
            int attachmentCount)
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
                AttachmentCount = attachmentCount,
                CreatedAt = booking.CreatedAt
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
            int attachmentCount)
            => MapToListItemResponse(booking, clientUser, profile, slot, spec, expertUser, app, attachmentCount);

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

                return MapToListItemResponse(b, clientUser, profile, slot, spec, expertUser, app, attCount);
            }).ToList();
        }
    }
}
