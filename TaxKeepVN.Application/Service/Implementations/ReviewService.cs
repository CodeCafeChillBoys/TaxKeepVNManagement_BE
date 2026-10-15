using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Reviews;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    /// <summary>
    /// Triển khai nghiệp vụ Đánh giá và Phản hồi sau phiên tư vấn.
    /// Tất cả ngưỡng/thời gian đều đọc từ IConfiguration, không hardcode.
    /// </summary>
    public class ReviewService : IReviewService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ReviewService> _logger;

        // Config keys — các giá trị được đọc từ appsettings.json / environment variable
        private const string CfgReviewWindowDays = "ReviewSettings:ReviewWindowDays";
        private const string CfgFlagThresholdRating = "ReviewSettings:FlagThresholdRating";
        private const string CfgMinCommentLength = "ReviewSettings:MinCommentLength";

        public ReviewService(
            IUnitOfWork unitOfWork,
            IConfiguration configuration,
            ILogger<ReviewService> logger)
        {
            _unitOfWork = unitOfWork;
            _configuration = configuration;
            _logger = logger;
        }

        // ════════════════════════════════════════════════════════════════════════
        //  PRIVATE HELPERS — đọc config, không hardcode
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>Số ngày kể từ CompletedAt mà khách hàng được phép gửi đánh giá (mặc định 7).</summary>
        private int ReviewWindowDays => int.TryParse(_configuration[CfgReviewWindowDays], out var v) ? v : 7;

        /// <summary>Ngưỡng số sao dưới đây sẽ tự động flag review (mặc định 2).</summary>
        private int FlagThresholdRating => int.TryParse(_configuration[CfgFlagThresholdRating], out var v) ? v : 2;

        /// <summary>Độ dài tối thiểu nội dung nhận xét (mặc định 10).</summary>
        private int MinCommentLength => int.TryParse(_configuration[CfgMinCommentLength], out var v) ? v : 10;

        /// <summary>Tạo SystemNotification và lưu vào DB.</summary>
        private async Task SendNotificationAsync(Guid userId, string title, string message, string type, string? targetUrl = null)
        {
            var repo = _unitOfWork.Repository<SystemNotification>();
            await repo.AddAsync(new SystemNotification
            {
                NotificationId = Guid.NewGuid(),
                UserId = userId,
                Title = title,
                Message = message,
                NotificationType = type,
                IsRead = false,
                TargetActionUrl = targetUrl,
                CreatedAt = DateTime.UtcNow
            });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  1. CHUYÊN GIA KẾT THÚC PHIÊN
        // ════════════════════════════════════════════════════════════════════════

        public async Task CompleteSessionAsync(Guid expertUserId, Guid bookingId)
        {
            var booking = await _unitOfWork.Repository<Booking>().GetByIdAsync(bookingId)
                ?? throw new NotFoundException($"Không tìm thấy lịch hẹn với Id = {bookingId}.");

            var slot = await _unitOfWork.Repository<ExpertSlot>().GetByIdAsync(booking.ExpertSlotId)
                ?? throw new NotFoundException("Không tìm thấy khung giờ của lịch hẹn.");

            var profile = await _unitOfWork.Repository<ExpertProfile>().GetByIdAsync(slot.ExpertProfileId)
                ?? throw new NotFoundException("Không tìm thấy hồ sơ chuyên gia.");

            if (profile.UserId != expertUserId)
                throw new ForbiddenException("Bạn không phải chuyên gia được phân công cho ca tư vấn này.");

            if (booking.Status != BookingStatus.CONFIRMED)
                throw new BadRequestException("INVALID_STATUS",
                    $"Lịch hẹn đang ở trạng thái '{booking.Status}', không thể kết thúc. Chỉ ca đã được xác nhận (CONFIRMED) mới có thể đánh dấu hoàn thành.");

            var nowUtc = DateTimeOffset.UtcNow;
            booking.Status = BookingStatus.COMPLETED;
            booking.CompletedAt = nowUtc;
            booking.ReviewDeadline = nowUtc.AddDays(ReviewWindowDays);
            booking.UpdatedAt = nowUtc;

            // Tăng số ca hoàn thành của chuyên gia
            profile.CompletedConsultationsCount += 1;
            profile.UpdatedAt = nowUtc;

            _unitOfWork.Repository<Booking>().Update(booking);
            _unitOfWork.Repository<ExpertProfile>().Update(profile);

            // Gửi thông báo nhắc nhở khách hàng đánh giá
            await SendNotificationAsync(
                userId: booking.UserId,
                title: "Phiên tư vấn đã hoàn thành — Hãy để lại đánh giá!",
                message: $"Buổi tư vấn của bạn đã kết thúc. Bạn có {ReviewWindowDays} ngày để gửi đánh giá cho chuyên gia.",
                type: "REVIEW_REMINDER",
                targetUrl: $"/bookings/{bookingId}/review");

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Chuyên gia {ExpertUserId} đã kết thúc phiên booking {BookingId}. ReviewDeadline = {Deadline}.",
                expertUserId, bookingId, booking.ReviewDeadline);
        }

        // ════════════════════════════════════════════════════════════════════════
        //  2. KHÁCH HÀNG KIỂM TRA QUYỀN ĐÁNH GIÁ
        // ════════════════════════════════════════════════════════════════════════

        public async Task<ReviewEligibilityResponse> CheckReviewEligibilityAsync(Guid userId, Guid bookingId)
        {
            var booking = await _unitOfWork.Repository<Booking>().GetByIdAsync(bookingId)
                ?? throw new NotFoundException($"Không tìm thấy lịch hẹn với Id = {bookingId}.");

            if (booking.UserId != userId)
                throw new ForbiddenException("Bạn không có quyền xem thông tin lịch hẹn này.");

            // Kiểm tra đã review chưa
            var existingReview = (await _unitOfWork.Repository<ExpertReview>().FindAsync(
                r => r.BookingId == bookingId && r.UserId == userId)).FirstOrDefault();

            if (existingReview != null)
            {
                return new ReviewEligibilityResponse
                {
                    CanReview = false,
                    AlreadyReviewed = true,
                    ReviewDeadline = booking.ReviewDeadline,
                    Message = "Bạn đã gửi đánh giá cho phiên tư vấn này rồi."
                };
            }

            if (booking.Status != BookingStatus.COMPLETED)
            {
                return new ReviewEligibilityResponse
                {
                    CanReview = false,
                    AlreadyReviewed = false,
                    Message = "Phiên tư vấn chưa hoàn thành, chưa thể đánh giá."
                };
            }

            var nowUtc = DateTimeOffset.UtcNow;
            if (booking.ReviewDeadline.HasValue && booking.ReviewDeadline.Value < nowUtc)
            {
                return new ReviewEligibilityResponse
                {
                    CanReview = false,
                    AlreadyReviewed = false,
                    ReviewDeadline = booking.ReviewDeadline,
                    RemainingSeconds = 0,
                    Message = $"Đã quá thời hạn {ReviewWindowDays} ngày để gửi đánh giá."
                };
            }

            var remaining = booking.ReviewDeadline.HasValue
                ? (int)Math.Max(0, (booking.ReviewDeadline.Value - nowUtc).TotalSeconds)
                : (int?)null;

            return new ReviewEligibilityResponse
            {
                CanReview = true,
                AlreadyReviewed = false,
                ReviewDeadline = booking.ReviewDeadline,
                RemainingSeconds = remaining,
                Message = "Bạn có thể gửi đánh giá cho phiên tư vấn này."
            };
        }

        // ════════════════════════════════════════════════════════════════════════
        //  3. LẤY DANH SÁCH ĐÁNH GIÁ CÔNG KHAI CỦA CHUYÊN GIA
        // ════════════════════════════════════════════════════════════════════════

        public async Task<PagedResult<ReviewListItemResponse>> GetExpertPublicReviewsAsync(Guid expertProfileId, int page = 1, int size = 10)
        {
            var profile = await _unitOfWork.Repository<ExpertProfile>().GetByIdAsync(expertProfileId)
                ?? throw new NotFoundException("Không tìm thấy hồ sơ chuyên gia.");

            var reviews = (await _unitOfWork.Repository<ExpertReview>().FindAsync(
                r => r.ExpertProfileId == expertProfileId && r.IsPublished))
                .OrderByDescending(r => r.CreatedAt).AsQueryable();

            var total = reviews.Count();
            var paged = reviews.Skip((page - 1) * size).Take(size).ToList();

            var userIds = paged.Where(r => !r.IsAnonymous).Select(r => r.UserId).Distinct().ToList();
            var users = userIds.Any()
                ? (await _unitOfWork.Repository<User>().FindAsync(u => userIds.Contains(u.UserId)))
                    .ToDictionary(u => u.UserId)
                : new System.Collections.Generic.Dictionary<Guid, User>();

            var items = paged.Select(r =>
            {
                ReviewDetailScoresDto? detailScores = null;
                if (!string.IsNullOrEmpty(r.DetailScoresJson))
                {
                    try { detailScores = JsonSerializer.Deserialize<ReviewDetailScoresDto>(r.DetailScoresJson); }
                    catch { /* Bỏ qua nếu lỗi format */ }
                }

                string reviewerName = "Khách hàng ẩn danh";
                if (!r.IsAnonymous && users.TryGetValue(r.UserId, out var u))
                {
                    reviewerName = !string.IsNullOrWhiteSpace(u.FullName) ? u.FullName : "Khách hàng";
                }

                return new ReviewListItemResponse
                {
                    Id = r.Id,
                    ReviewerName = reviewerName,
                    Rating = r.Rating,
                    Comment = r.Comment,
                    DetailScores = detailScores,
                    ExpertReply = r.ExpertReply,
                    ExpertRepliedAt = r.ExpertRepliedAt,
                    CreatedAt = r.CreatedAt
                };
            }).ToList();

            return new PagedResult<ReviewListItemResponse>
            {
                Items = items,
                Pagination = new PaginationMeta
                {
                    Page = page,
                    PageSize = size,
                    TotalItems = total,
                    TotalPages = (int)Math.Ceiling(total / (double)size)
                }
            };
        }

        // ════════════════════════════════════════════════════════════════════════
        //  4. KHÁCH HÀNG GỬI ĐÁNH GIÁ
        // ════════════════════════════════════════════════════════════════════════

        public async Task<ReviewDetailResponse> SubmitReviewAsync(Guid userId, Guid bookingId, CreateReviewRequest request)
        {
            // ── Validate rating ──────────────────────────────────────────────
            if (request.Rating < 1 || request.Rating > 5)
                throw new BadRequestException("INVALID_RATING", "Điểm đánh giá phải từ 1 đến 5 sao.");

            if (string.IsNullOrWhiteSpace(request.Comment) || request.Comment.Trim().Length < MinCommentLength)
                throw new BadRequestException("COMMENT_TOO_SHORT",
                    $"Nội dung nhận xét phải có ít nhất {MinCommentLength} ký tự.");

            // ── Validate detail scores (nếu có) ─────────────────────────────
            if (request.DetailScores != null)
            {
                ValidateDetailScore(request.DetailScores.Expertise, "Độ am hiểu chuyên môn");
                ValidateDetailScore(request.DetailScores.Punctuality, "Mức độ đúng giờ");
                ValidateDetailScore(request.DetailScores.Communication, "Thái độ giao tiếp");
                ValidateDetailScore(request.DetailScores.ProblemSolving, "Khả năng giải quyết vấn đề");
            }

            // ── Kiểm tra quyền đánh giá ──────────────────────────────────────
            var eligibility = await CheckReviewEligibilityAsync(userId, bookingId);
            if (!eligibility.CanReview)
                throw new BadRequestException("REVIEW_NOT_ALLOWED", eligibility.Message ?? "Không thể gửi đánh giá.");

            var booking = await _unitOfWork.Repository<Booking>().GetByIdAsync(bookingId)
                ?? throw new NotFoundException($"Không tìm thấy lịch hẹn với Id = {bookingId}.");

            var slot = await _unitOfWork.Repository<ExpertSlot>().GetByIdAsync(booking.ExpertSlotId)
                ?? throw new NotFoundException("Không tìm thấy khung giờ của lịch hẹn.");

            var profile = await _unitOfWork.Repository<ExpertProfile>().GetByIdAsync(slot.ExpertProfileId)
                ?? throw new NotFoundException("Không tìm thấy hồ sơ chuyên gia.");

            // ── Xác định có flag không (đọc ngưỡng từ config) ───────────────
            bool isFlagged = request.Rating <= FlagThresholdRating;

            // ── Serialize detail scores ──────────────────────────────────────
            string? detailScoresJson = request.DetailScores != null
                ? JsonSerializer.Serialize(request.DetailScores)
                : null;

            var nowUtc = DateTimeOffset.UtcNow;
            var review = new ExpertReview
            {
                Id = Guid.NewGuid(),
                ExpertProfileId = profile.Id,
                UserId = userId,
                BookingId = bookingId,
                Rating = request.Rating,
                Comment = request.Comment.Trim(),
                DetailScoresJson = detailScoresJson,
                IsAnonymous = request.IsAnonymous,
                IsPublished = true,
                IsFlaggedForReview = isFlagged,
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc
            };

            await _unitOfWork.Repository<ExpertReview>().AddAsync(review);

            // ── Cập nhật Rating và TotalReviews của ExpertProfile (tính lũy kế) ──
            var allRatings = (await _unitOfWork.Repository<ExpertReview>().FindAsync(
                r => r.ExpertProfileId == profile.Id && r.IsPublished)).Select(r => r.Rating).ToList();
            allRatings.Add(request.Rating); // thêm review vừa tạo (chưa SaveChanges)

            profile.TotalReviews = allRatings.Count;
            profile.Rating = Math.Round((decimal)allRatings.Average(), 2);
            profile.UpdatedAt = nowUtc;
            _unitOfWork.Repository<ExpertProfile>().Update(profile);

            await _unitOfWork.SaveChangesAsync();

            // ── Gửi thông báo ─────────────────────────────────────────────────

            // 3a. Thông báo cho chuyên gia về đánh giá mới
            var reviewerName = request.IsAnonymous ? "Khách hàng ẩn danh" : "Khách hàng";
            await SendNotificationAsync(
                userId: profile.UserId,
                title: "Bạn nhận được đánh giá mới!",
                message: $"{reviewerName} vừa gửi đánh giá {request.Rating}⭐ cho phiên tư vấn của bạn.",
                type: "NEW_REVIEW",
                targetUrl: $"/expert/reviews/{review.Id}");

            // 3b. Nếu review tiêu cực (flag) → thông báo cho Admin/CSKH
            if (isFlagged)
            {
                // Lấy danh sách Admin để gửi thông báo
                var admins = (await _unitOfWork.Repository<User>().FindAsync(u => u.UserRole == "admin")).ToList();
                foreach (var admin in admins)
                {
                    await SendNotificationAsync(
                        userId: admin.UserId,
                        title: $"⚠️ Đánh giá tiêu cực cần kiểm duyệt [{request.Rating}⭐]",
                        message: $"Review Id {review.Id} từ phiên booking {bookingId} được đánh {request.Rating}⭐ — cần xem xét theo quy trình CSKH.",
                        type: "FLAG_REVIEW_ALERT",
                        targetUrl: $"/admin/reviews/{review.Id}");
                }

                _logger.LogWarning("Review {ReviewId} bị flag (rating = {Rating} <= threshold {Threshold}) cho booking {BookingId}.",
                    review.Id, request.Rating, FlagThresholdRating, bookingId);
            }

            await _unitOfWork.SaveChangesAsync();

            // ── Lấy thông tin reviewer để trả về ─────────────────────────────
            var user = await _unitOfWork.Repository<User>().GetByIdAsync(userId);

            return MapToDetailResponse(review, user, request.IsAnonymous);
        }

        // ════════════════════════════════════════════════════════════════════════
        //  4. CHUYÊN GIA PHẢN HỒI ĐÁNH GIÁ
        // ════════════════════════════════════════════════════════════════════════

        public async Task<ReviewDetailResponse> ReplyToReviewAsync(Guid expertUserId, Guid reviewId, ExpertReplyRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Reply))
                throw new BadRequestException("EMPTY_REPLY", "Nội dung phản hồi không được để trống.");

            var review = await _unitOfWork.Repository<ExpertReview>().GetByIdAsync(reviewId)
                ?? throw new NotFoundException($"Không tìm thấy đánh giá với Id = {reviewId}.");

            var profile = await _unitOfWork.Repository<ExpertProfile>().GetByIdAsync(review.ExpertProfileId)
                ?? throw new NotFoundException("Không tìm thấy hồ sơ chuyên gia.");

            if (profile.UserId != expertUserId)
                throw new ForbiddenException("Bạn không phải chuyên gia của đánh giá này.");

            if (review.ExpertReply != null)
                throw new BadRequestException("ALREADY_REPLIED", "Bạn đã phản hồi đánh giá này rồi. Chỉ được phép phản hồi 1 lần.");

            var nowUtc = DateTimeOffset.UtcNow;
            review.ExpertReply = request.Reply.Trim();
            review.ExpertRepliedAt = nowUtc;
            review.UpdatedAt = nowUtc;
            _unitOfWork.Repository<ExpertReview>().Update(review);

            // Gửi thông báo cho khách hàng biết chuyên gia đã phản hồi
            await SendNotificationAsync(
                userId: review.UserId,
                title: "Chuyên gia đã phản hồi đánh giá của bạn",
                message: "Chuyên gia vừa gửi phản hồi cho nhận xét của bạn. Nhấn để xem.",
                type: "EXPERT_REPLY",
                targetUrl: $"/bookings/{review.BookingId}/review");

            await _unitOfWork.SaveChangesAsync();

            var user = await _unitOfWork.Repository<User>().GetByIdAsync(review.UserId);
            return MapToDetailResponse(review, user, review.IsAnonymous);
        }

        // ════════════════════════════════════════════════════════════════════════
        //  5. CHUYÊN GIA XEM DANH SÁCH ĐÁNH GIÁ ĐÃ NHẬN
        // ════════════════════════════════════════════════════════════════════════

        public async Task<PagedResult<ReviewDetailResponse>> GetMyReceivedReviewsAsync(Guid expertUserId, int page = 1, int size = 10)
        {
            var profile = (await _unitOfWork.Repository<ExpertProfile>().FindAsync(p => p.UserId == expertUserId)).FirstOrDefault()
                ?? throw new NotFoundException("Tài khoản này chưa có hồ sơ chuyên gia.");

            var reviews = (await _unitOfWork.Repository<ExpertReview>().FindAsync(r => r.ExpertProfileId == profile.Id))
                .OrderByDescending(r => r.CreatedAt).AsQueryable();

            var total = reviews.Count();
            var paged = reviews.Skip((page - 1) * size).Take(size).ToList();

            var userIds = paged.Select(r => r.UserId).Distinct().ToList();
            var users = (await _unitOfWork.Repository<User>().FindAsync(u => userIds.Contains(u.UserId)))
                .ToDictionary(u => u.UserId);

            var items = paged.Select(r =>
            {
                users.TryGetValue(r.UserId, out var user);
                return MapToDetailResponse(r, user, r.IsAnonymous);
            }).ToList();

            return new PagedResult<ReviewDetailResponse>
            {
                Items = items,
                Pagination = new PaginationMeta
                {
                    Page = page,
                    PageSize = size,
                    TotalItems = total,
                    TotalPages = (int)Math.Ceiling(total / (double)size)
                }
            };
        }

        // ════════════════════════════════════════════════════════════════════════
        //  6. ADMIN - ẨN/HIỆN ĐÁNH GIÁ
        // ════════════════════════════════════════════════════════════════════════

        public async Task<ReviewDetailResponse> TogglePublishReviewAsync(Guid reviewId, bool isPublished)
        {
            var review = await _unitOfWork.Repository<ExpertReview>().GetByIdAsync(reviewId)
                ?? throw new NotFoundException($"Không tìm thấy đánh giá với Id = {reviewId}.");

            review.IsPublished = isPublished;
            review.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<ExpertReview>().Update(review);

            // Cập nhật lại Rating của ExpertProfile (loại trừ review không published)
            var profile = await _unitOfWork.Repository<ExpertProfile>().GetByIdAsync(review.ExpertProfileId);
            if (profile != null)
            {
                var publishedRatings = (await _unitOfWork.Repository<ExpertReview>().FindAsync(
                    r => r.ExpertProfileId == profile.Id && r.IsPublished)).Select(r => r.Rating).ToList();

                // Bao gồm review hiện tại nếu đang được publish
                if (isPublished && !publishedRatings.Contains(review.Rating))
                    publishedRatings.Add(review.Rating);

                profile.TotalReviews = publishedRatings.Count;
                profile.Rating = publishedRatings.Any()
                    ? Math.Round((decimal)publishedRatings.Average(), 2)
                    : 0m;
                profile.UpdatedAt = DateTimeOffset.UtcNow;
                _unitOfWork.Repository<ExpertProfile>().Update(profile);
            }

            await _unitOfWork.SaveChangesAsync();

            var user = await _unitOfWork.Repository<User>().GetByIdAsync(review.UserId);
            return MapToDetailResponse(review, user, review.IsAnonymous);
        }

        // ════════════════════════════════════════════════════════════════════════
        //  7. ADMIN - LẤY DANH SÁCH ĐÁNH GIÁ BỊ FLAG
        // ════════════════════════════════════════════════════════════════════════

        public async Task<PagedResult<ReviewDetailResponse>> GetFlaggedReviewsAsync(int page = 1, int size = 20)
        {
            var reviews = (await _unitOfWork.Repository<ExpertReview>().FindAsync(r => r.IsFlaggedForReview))
                .OrderByDescending(r => r.CreatedAt).AsQueryable();

            var total = reviews.Count();
            var paged = reviews.Skip((page - 1) * size).Take(size).ToList();

            var userIds = paged.Select(r => r.UserId).Distinct().ToList();
            var users = (await _unitOfWork.Repository<User>().FindAsync(u => userIds.Contains(u.UserId)))
                .ToDictionary(u => u.UserId);

            var items = paged.Select(r =>
            {
                users.TryGetValue(r.UserId, out var user);
                return MapToDetailResponse(r, user, false); // Admin xem full, không ẩn danh
            }).ToList();

            return new PagedResult<ReviewDetailResponse>
            {
                Items = items,
                Pagination = new PaginationMeta
                {
                    Page = page,
                    PageSize = size,
                    TotalItems = total,
                    TotalPages = (int)Math.Ceiling(total / (double)size)
                }
            };
        }

        // ════════════════════════════════════════════════════════════════════════
        //  PRIVATE MAPPERS
        // ════════════════════════════════════════════════════════════════════════

        private static ReviewDetailResponse MapToDetailResponse(ExpertReview review, User? user, bool isAnonymous)
        {
            ReviewDetailScoresDto? detailScores = null;
            if (!string.IsNullOrEmpty(review.DetailScoresJson))
            {
                try { detailScores = JsonSerializer.Deserialize<ReviewDetailScoresDto>(review.DetailScoresJson); }
                catch { /* JSON lỗi không break response */ }
            }

            return new ReviewDetailResponse
            {
                Id = review.Id,
                BookingId = review.BookingId ?? Guid.Empty,
                ExpertProfileId = review.ExpertProfileId,
                UserId = review.UserId,
                ReviewerName = isAnonymous ? "Khách hàng ẩn danh" : (user?.FullName ?? "Khách hàng"),
                Rating = review.Rating,
                Comment = review.Comment,
                DetailScores = detailScores,
                IsAnonymous = review.IsAnonymous,
                IsPublished = review.IsPublished,
                IsFlaggedForReview = review.IsFlaggedForReview,
                ExpertReply = review.ExpertReply,
                ExpertRepliedAt = review.ExpertRepliedAt,
                CreatedAt = review.CreatedAt
            };
        }

        private void ValidateDetailScore(int? value, string fieldName)
        {
            if (value.HasValue && (value.Value < 1 || value.Value > 5))
                throw new BadRequestException("INVALID_DETAIL_SCORE",
                    $"Điểm tiêu chí '{fieldName}' phải từ 1 đến 5 sao.");
        }
    }
}
