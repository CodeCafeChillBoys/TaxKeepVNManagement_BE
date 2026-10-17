using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Disputes;
using TaxKeepVN.Application.DTOs.Wallets;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Helpers;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    /// <summary>
    /// Service xử lý Giải ngân thù lao chuyên gia, Ví điện tử, Rút tiền và Xử lý tranh chấp (Đặc tả 7)
    /// Tuân thủ nguyên tắc ZERO-HARDCODE: Toàn bộ hạn mức, tỷ lệ %, ngưỡng giờ đều đọc động từ bảng system_configs.
    /// </summary>
    public class DisbursementDisputeService : IDisbursementDisputeService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<DisbursementDisputeService> _logger;

        public DisbursementDisputeService(
            IUnitOfWork unitOfWork,
            ILogger<DisbursementDisputeService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        // ════════════════════════════════════════════════════════════════════════
        //  1. GIẢI NGÂN & XÁC NHẬN HÀI LÒNG SỚM (MỤC 5)
        // ════════════════════════════════════════════════════════════════════════

        public async Task ConfirmSatisfactionAsync(Guid userId, Guid bookingId)
        {
            var booking = await _unitOfWork.Repository<Booking>().GetByIdAsync(bookingId)
                ?? throw new NotFoundException($"Không tìm thấy lịch hẹn với Id = {bookingId}.");

            if (booking.UserId != userId)
            {
                throw new ForbiddenException("FORBIDDEN", "Bạn không có quyền xác nhận cho ca tư vấn của người khác.");
            }

            if (booking.Status != BookingStatus.COMPLETED)
            {
                throw new BadRequestException("INVALID_STATUS",
                    $"Chỉ có thể xác nhận hài lòng khi ca tư vấn đã hoàn thành (COMPLETED). Hiện tại: '{booking.Status}'.");
            }

            // Kiểm tra xem có đang tranh chấp không
            var disputes = await _unitOfWork.Repository<BookingDispute>().FindAsync(d => d.BookingId == bookingId);
            if (disputes.Any(d => d.Status == "PENDING_EXPERT_RESPONSE" || d.Status == "AWAITING_ADMIN_RESOLUTION"))
            {
                throw new BadRequestException("UNDER_DISPUTE", "Ca tư vấn đang trong quá trình xử lý khiếu nại, không thể xác nhận hài lòng sớm.");
            }

            await ExecuteDisbursementInternalAsync(booking, "Khách hàng chủ động bấm 'Xác nhận hài lòng' sớm.");
        }

        public async Task<bool> ProcessCoolDownDisbursementAsync(Guid bookingId)
        {
            var booking = await _unitOfWork.Repository<Booking>().GetByIdAsync(bookingId);
            if (booking == null || booking.Status != BookingStatus.COMPLETED)
            {
                return false;
            }

            var disputes = await _unitOfWork.Repository<BookingDispute>().FindAsync(d => d.BookingId == bookingId);
            if (disputes.Any(d => d.Status == "PENDING_EXPERT_RESPONSE" || d.Status == "AWAITING_ADMIN_RESOLUTION"))
            {
                _logger.LogInformation("Booking {BookingId} đang có khiếu nại tranh chấp, bỏ qua giải ngân tự động.", bookingId);
                return false;
            }

            var nowUtc = DateTimeOffset.UtcNow;
            var policy = await BookingHelper.GetPlatformPolicyAsync(_unitOfWork);
            var coolDownHours = policy.EscrowCooldownHours > 0 ? policy.EscrowCooldownHours : 24;
            var completedAt = booking.CompletedAt ?? booking.UpdatedAt;

            if (nowUtc < completedAt.AddHours(coolDownHours))
            {
                return false; // Chưa hết thời gian 24 giờ
            }

            await ExecuteDisbursementInternalAsync(booking, $"Hết {coolDownHours} giờ Cool-down không phát sinh khiếu nại (Giải ngân tự động).");
            return true;
        }

        private async Task ExecuteDisbursementInternalAsync(Booking booking, string reason)
        {
            var nowUtc = DateTimeOffset.UtcNow;
            var escrows = await _unitOfWork.Repository<EscrowTransaction>().FindAsync(e => e.BookingId == booking.Id);
            var escrow = escrows.FirstOrDefault();

            if (escrow == null)
            {
                throw new BadRequestException("ESCROW_NOT_FOUND", "Không tìm thấy giao dịch Escrow tương ứng với lịch hẹn.");
            }

            if (escrow.EscrowStatus == EscrowStatus.Settled)
            {
                return; // Đã giải ngân rồi
            }

            var slot = await _unitOfWork.Repository<ExpertSlot>().GetByIdAsync(booking.ExpertSlotId)
                ?? throw new NotFoundException("Không tìm thấy khung giờ của lịch hẹn.");

            var profile = await _unitOfWork.Repository<ExpertProfile>().GetByIdAsync(slot.ExpertProfileId)
                ?? throw new NotFoundException("Không tìm thấy hồ sơ chuyên gia.");

            var expertReceivable = escrow.ExpertReceivable;

            // 1. Cộng tiền vào ví chuyên gia
            var expertWallet = await GetOrCreateWalletAsync(profile.UserId, "EXPERT");
            var balanceBefore = expertWallet.AvailableBalance;
            expertWallet.AvailableBalance += expertReceivable;
            expertWallet.TotalEarned += expertReceivable;
            expertWallet.UpdatedAt = nowUtc;
            _unitOfWork.Repository<UserWallet>().Update(expertWallet);

            // 2. Ghi sổ cái biến động ví
            await _unitOfWork.Repository<WalletTransaction>().AddAsync(new WalletTransaction
            {
                Id = Guid.NewGuid(),
                WalletId = expertWallet.Id,
                UserId = profile.UserId,
                Amount = expertReceivable,
                BalanceBefore = balanceBefore,
                BalanceAfter = expertWallet.AvailableBalance,
                TransactionType = "DISBURSEMENT",
                ReferenceId = booking.Id,
                Description = $"Thù lao ca tư vấn #{booking.BookingCode} ({reason})",
                CreatedAt = nowUtc
            });

            // 3. Cập nhật Escrow và Booking
            escrow.EscrowStatus = EscrowStatus.Settled;
            escrow.SettledAt = nowUtc;
            escrow.UpdatedAt = nowUtc;
            _unitOfWork.Repository<EscrowTransaction>().Update(escrow);

            booking.Status = BookingStatus.SETTLED;
            booking.UpdatedAt = nowUtc;
            _unitOfWork.Repository<Booking>().Update(booking);

            // 4. Gửi thông báo cho chuyên gia
            await SendNotificationAsync(
                userId: profile.UserId,
                title: "Thù lao tư vấn đã được giải ngân vào ví!",
                message: $"Số tiền {expertReceivable:N0} VNĐ cho phiên tư vấn #{booking.BookingCode} đã được cộng vào Ví thu nhập của bạn.",
                type: "DISBURSEMENT_SUCCESS",
                targetUrl: "/expert/wallet");

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Đã giải ngân {Amount} VNĐ cho chuyên gia {ExpertUserId} từ booking {BookingCode}. Lý do: {Reason}",
                expertReceivable, profile.UserId, booking.BookingCode, reason);
        }

        // ════════════════════════════════════════════════════════════════════════
        //  2. KHIẾU NẠI & TRANH CHẤP DỊCH VỤ (MỤC 6.B)
        // ════════════════════════════════════════════════════════════════════════

        public async Task<DisputeEligibilityResponse> CheckDisputeEligibilityAsync(Guid userId, Guid bookingId)
        {
            var booking = await _unitOfWork.Repository<Booking>().GetByIdAsync(bookingId)
                ?? throw new NotFoundException($"Không tìm thấy lịch hẹn với Id = {bookingId}.");

            if (booking.UserId != userId)
            {
                throw new ForbiddenException("FORBIDDEN", "Bạn không có quyền xem thông tin lịch hẹn này.");
            }

            var existingDispute = (await _unitOfWork.Repository<BookingDispute>().FindAsync(d => d.BookingId == bookingId)).FirstOrDefault();
            if (existingDispute != null)
            {
                return new DisputeEligibilityResponse
                {
                    CanDispute = false,
                    AlreadyDisputed = true,
                    Message = "Phiên tư vấn này đã phát sinh khiếu nại trước đó."
                };
            }

            if (booking.Status != BookingStatus.COMPLETED)
            {
                return new DisputeEligibilityResponse
                {
                    CanDispute = false,
                    AlreadyDisputed = false,
                    Message = $"Chỉ có thể khiếu nại phiên tư vấn đã kết thúc (COMPLETED). Trạng thái hiện tại: '{booking.Status}'."
                };
            }

            var policy = await BookingHelper.GetPlatformPolicyAsync(_unitOfWork);
            var disputeWindowHours = policy.DisputeWindowHours > 0 ? policy.DisputeWindowHours : 24;
            var completedAt = booking.CompletedAt ?? booking.UpdatedAt;
            var deadline = completedAt.AddHours(disputeWindowHours);
            var nowUtc = DateTimeOffset.UtcNow;

            if (nowUtc > deadline)
            {
                return new DisputeEligibilityResponse
                {
                    CanDispute = false,
                    AlreadyDisputed = false,
                    DisputeDeadline = deadline,
                    RemainingSeconds = 0,
                    Message = $"Đã hết thời hạn khiếu nại ({disputeWindowHours} giờ kể từ khi hoàn tất ca tư vấn)."
                };
            }

            var remainingSeconds = (int)Math.Max(0, (deadline - nowUtc).TotalSeconds);

            return new DisputeEligibilityResponse
            {
                CanDispute = true,
                AlreadyDisputed = false,
                DisputeDeadline = deadline,
                RemainingSeconds = remainingSeconds,
                Message = $"Bạn có thể gửi khiếu nại trong vòng {disputeWindowHours} giờ kể từ khi kết thúc ca."
            };
        }

        public async Task<DisputeDetailResponse> SubmitDisputeAsync(Guid userId, Guid bookingId, CreateDisputeRequest request)
        {
            var eligibility = await CheckDisputeEligibilityAsync(userId, bookingId);
            if (!eligibility.CanDispute)
            {
                throw new BadRequestException("DISPUTE_NOT_ALLOWED", eligibility.Message ?? "Không thể gửi khiếu nại cho ca tư vấn này.");
            }

            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                throw new BadRequestException("REASON_REQUIRED", "Vui lòng chọn lý do khiếu nại.");
            }

            if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Trim().Length < 10)
            {
                throw new BadRequestException("DESCRIPTION_TOO_SHORT", "Nội dung mô tả khiếu nại phải có ít nhất 10 ký tự.");
            }

            var booking = (await _unitOfWork.Repository<Booking>().GetByIdAsync(bookingId))!;
            var slot = await _unitOfWork.Repository<ExpertSlot>().GetByIdAsync(booking.ExpertSlotId)
                ?? throw new NotFoundException("Không tìm thấy khung giờ của ca tư vấn.");
            var profile = await _unitOfWork.Repository<ExpertProfile>().GetByIdAsync(slot.ExpertProfileId)
                ?? throw new NotFoundException("Không tìm thấy hồ sơ chuyên gia.");

            var escrows = await _unitOfWork.Repository<EscrowTransaction>().FindAsync(e => e.BookingId == bookingId);
            var escrow = escrows.FirstOrDefault()
                ?? throw new BadRequestException("ESCROW_NOT_FOUND", "Không tìm thấy giao dịch Escrow cho ca tư vấn này.");

            var nowUtc = DateTimeOffset.UtcNow;
            var policy = await BookingHelper.GetPlatformPolicyAsync(_unitOfWork);
            var replyHours = policy.DisputeExpertReplyHours > 0 ? policy.DisputeExpertReplyHours : 24;

            // 1. Đóng băng tiền Escrow (Freeze Escrow) & chuyển trạng thái Booking
            escrow.EscrowStatus = EscrowStatus.Disputed;
            escrow.UpdatedAt = nowUtc;
            _unitOfWork.Repository<EscrowTransaction>().Update(escrow);

            booking.Status = BookingStatus.UNDER_DISPUTE;
            booking.UpdatedAt = nowUtc;
            _unitOfWork.Repository<Booking>().Update(booking);

            // 2. Tạo bản ghi BookingDispute
            var dispute = new BookingDispute
            {
                Id = Guid.NewGuid(),
                BookingId = bookingId,
                UserId = userId,
                ExpertUserId = profile.UserId,
                Reason = request.Reason.Trim().ToUpperInvariant(),
                Description = request.Description.Trim(),
                EvidenceUrlsJson = request.EvidenceUrls != null && request.EvidenceUrls.Any()
                    ? JsonSerializer.Serialize(request.EvidenceUrls)
                    : null,
                Status = "PENDING_EXPERT_RESPONSE",
                ExpertResponseDeadline = nowUtc.AddHours(replyHours),
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc
            };
            await _unitOfWork.Repository<BookingDispute>().AddAsync(dispute);

            // 3. Gửi thông báo cho Chuyên gia
            await SendNotificationAsync(
                userId: profile.UserId,
                title: "⚠️ Khách hàng đã gửi khiếu nại phiên tư vấn!",
                message: $"Phiên tư vấn #{booking.BookingCode} nhận được khiếu nại (Lý do: {dispute.Reason}). Bạn có {replyHours} giờ để gửi bằng chứng phản biện.",
                type: "DISPUTE_RAISED",
                targetUrl: $"/expert/disputes/{dispute.Id}");

            // 4. Gửi thông báo cảnh báo cho đội ngũ Admin
            var admins = (await _unitOfWork.Repository<User>().FindAsync(u => u.UserRole == "admin")).ToList();
            foreach (var admin in admins)
            {
                await SendNotificationAsync(
                    userId: admin.UserId,
                    title: "🚨 Khiếu nại dịch vụ mới cần kiểm soát",
                    message: $"Booking #{booking.BookingCode} vừa phát sinh tranh chấp. Khoản tiền {escrow.Amount:N0} VNĐ đã bị đóng băng.",
                    type: "DISPUTE_ADMIN_ALERT",
                    targetUrl: $"/admin/disputes/{dispute.Id}");
            }

            await _unitOfWork.SaveChangesAsync();

            _logger.LogWarning("Khách hàng {UserId} đã khiếu nại booking {BookingCode}. Tiền Escrow {Amount} VNĐ đã bị đóng băng.",
                userId, booking.BookingCode, escrow.Amount);

            return await MapToDisputeDetailResponseAsync(dispute, booking);
        }

        public async Task<DisputeDetailResponse> ExpertRespondDisputeAsync(Guid expertUserId, Guid disputeId, ExpertRespondDisputeRequest request)
        {
            var dispute = await _unitOfWork.Repository<BookingDispute>().GetByIdAsync(disputeId)
                ?? throw new NotFoundException($"Không tìm thấy khiếu nại với Id = {disputeId}.");

            if (dispute.ExpertUserId != expertUserId)
            {
                throw new ForbiddenException("FORBIDDEN", "Bạn không phải chuyên gia của ca tư vấn đang bị khiếu nại này.");
            }

            if (dispute.Status != "PENDING_EXPERT_RESPONSE")
            {
                throw new BadRequestException("INVALID_STATUS", $"Khiếu nại hiện ở trạng thái '{dispute.Status}', không thể gửi thêm phản hồi.");
            }

            if (string.IsNullOrWhiteSpace(request.Response) || request.Response.Trim().Length < 10)
            {
                throw new BadRequestException("RESPONSE_TOO_SHORT", "Nội dung phản biện của chuyên gia phải có ít nhất 10 ký tự.");
            }

            var nowUtc = DateTimeOffset.UtcNow;
            dispute.ExpertResponse = request.Response.Trim();
            dispute.ExpertEvidenceUrlsJson = request.EvidenceUrls != null && request.EvidenceUrls.Any()
                ? JsonSerializer.Serialize(request.EvidenceUrls)
                : null;
            dispute.ExpertRespondedAt = nowUtc;
            dispute.Status = "AWAITING_ADMIN_RESOLUTION";
            dispute.UpdatedAt = nowUtc;
            _unitOfWork.Repository<BookingDispute>().Update(dispute);

            var booking = await _unitOfWork.Repository<Booking>().GetByIdAsync(dispute.BookingId);

            // Bắn notification cho Admin
            var admins = (await _unitOfWork.Repository<User>().FindAsync(u => u.UserRole == "admin")).ToList();
            foreach (var admin in admins)
            {
                await SendNotificationAsync(
                    userId: admin.UserId,
                    title: "Chuyên gia đã gửi giải trình khiếu nại",
                    message: $"Chuyên gia đã nộp phản biện cho khiếu nại booking #{booking?.BookingCode}. Vui lòng xem xét phán quyết.",
                    type: "DISPUTE_EXPERT_RESPONDED",
                    targetUrl: $"/admin/disputes/{dispute.Id}");
            }

            await _unitOfWork.SaveChangesAsync();

            return await MapToDisputeDetailResponseAsync(dispute, booking);
        }

        public async Task<DisputeDetailResponse> GetDisputeByIdAsync(Guid currentUserId, string currentUserRole, Guid disputeId)
        {
            var dispute = await _unitOfWork.Repository<BookingDispute>().GetByIdAsync(disputeId)
                ?? throw new NotFoundException($"Không tìm thấy khiếu nại với Id = {disputeId}.");

            var isAdmin = string.Equals(currentUserRole, "admin", StringComparison.OrdinalIgnoreCase);
            if (!isAdmin && dispute.UserId != currentUserId && dispute.ExpertUserId != currentUserId)
            {
                throw new ForbiddenException("FORBIDDEN", "Bạn không có quyền xem thông tin vụ việc khiếu nại này.");
            }

            var booking = await _unitOfWork.Repository<Booking>().GetByIdAsync(dispute.BookingId);
            return await MapToDisputeDetailResponseAsync(dispute, booking);
        }

        public async Task<DisputeDetailResponse?> GetDisputeByBookingIdAsync(Guid currentUserId, string currentUserRole, Guid bookingId)
        {
            var dispute = (await _unitOfWork.Repository<BookingDispute>().FindAsync(d => d.BookingId == bookingId)).FirstOrDefault();
            if (dispute == null) return null;

            var isAdmin = string.Equals(currentUserRole, "admin", StringComparison.OrdinalIgnoreCase);
            if (!isAdmin && dispute.UserId != currentUserId && dispute.ExpertUserId != currentUserId)
            {
                throw new ForbiddenException("FORBIDDEN", "Bạn không có quyền xem thông tin vụ việc khiếu nại này.");
            }

            var booking = await _unitOfWork.Repository<Booking>().GetByIdAsync(bookingId);
            return await MapToDisputeDetailResponseAsync(dispute, booking);
        }

        public async Task<PagedResult<DisputeListItemResponse>> GetDisputesAsync(Guid currentUserId, string currentUserRole, string? status = null, int page = 1, int size = 10)
        {
            var isAdmin = string.Equals(currentUserRole, "admin", StringComparison.OrdinalIgnoreCase);
            var query = (await _unitOfWork.Repository<BookingDispute>().GetAllAsync()).AsQueryable();

            if (!isAdmin)
            {
                query = query.Where(d => d.UserId == currentUserId || d.ExpertUserId == currentUserId);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(d => d.Status.Equals(status.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            query = query.OrderByDescending(d => d.CreatedAt);
            var totalItems = query.Count();
            var paged = query.Skip((page - 1) * size).Take(size).ToList();

            var bookingIds = paged.Select(d => d.BookingId).Distinct().ToList();
            var bookings = (await _unitOfWork.Repository<Booking>().FindAsync(b => bookingIds.Contains(b.Id)))
                .ToDictionary(b => b.Id);

            var userIds = paged.Select(d => d.UserId).Concat(paged.Select(d => d.ExpertUserId)).Distinct().ToList();
            var users = (await _unitOfWork.Repository<User>().FindAsync(u => userIds.Contains(u.UserId)))
                .ToDictionary(u => u.UserId);

            var items = paged.Select(d =>
            {
                bookings.TryGetValue(d.BookingId, out var b);
                users.TryGetValue(d.UserId, out var taxpayer);
                users.TryGetValue(d.ExpertUserId, out var expert);

                return new DisputeListItemResponse
                {
                    Id = d.Id,
                    BookingId = d.BookingId,
                    BookingCode = b?.BookingCode ?? string.Empty,
                    TaxpayerFullName = taxpayer?.FullName ?? "Khách hàng",
                    ExpertFullName = expert?.FullName ?? "Chuyên gia",
                    TotalFee = b?.Fee ?? 0m,
                    Reason = d.Reason,
                    Status = d.Status,
                    CreatedAt = d.CreatedAt,
                    ExpertResponseDeadline = d.ExpertResponseDeadline
                };
            }).ToList();

            return new PagedResult<DisputeListItemResponse>
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

        // ── PHÁN QUYẾT ADMIN (MỤC 6.B BƯỚC 5) ──────────────────────────────────────
        public async Task<DisputeDetailResponse> AdminResolveDisputeAsync(Guid adminId, Guid disputeId, AdminResolveDisputeRequest request)
        {
            var dispute = await _unitOfWork.Repository<BookingDispute>().GetByIdAsync(disputeId)
                ?? throw new NotFoundException($"Không tìm thấy khiếu nại với Id = {disputeId}.");

            if (dispute.Status != "PENDING_EXPERT_RESPONSE" && dispute.Status != "AWAITING_ADMIN_RESOLUTION")
            {
                throw new BadRequestException("DISPUTE_ALREADY_RESOLVED", $"Vụ việc khiếu nại này đã kết thúc với trạng thái '{dispute.Status}'.");
            }

            var decisionType = request.DecisionType.Trim().ToUpperInvariant();
            if (decisionType != "FULL_REFUND" && decisionType != "SETTLE_EXPERT" && decisionType != "PARTIAL")
            {
                throw new BadRequestException("INVALID_DECISION_TYPE", "Loại phán quyết phải là: FULL_REFUND, SETTLE_EXPERT hoặc PARTIAL.");
            }

            if (string.IsNullOrWhiteSpace(request.Resolution))
            {
                throw new BadRequestException("RESOLUTION_REQUIRED", "Bắt buộc nhập nội dung phán quyết giải quyết vụ việc.");
            }

            var booking = await _unitOfWork.Repository<Booking>().GetByIdAsync(dispute.BookingId)
                ?? throw new NotFoundException("Không tìm thấy thông tin ca đặt lịch.");

            var escrows = await _unitOfWork.Repository<EscrowTransaction>().FindAsync(e => e.BookingId == booking.Id);
            var escrow = escrows.FirstOrDefault()
                ?? throw new BadRequestException("ESCROW_NOT_FOUND", "Không tìm thấy khoản tiền Escrow.");

            var nowUtc = DateTimeOffset.UtcNow;
            var totalAmount = escrow.Amount;
            decimal userRefund = 0m;
            decimal expertReceivable = 0m;
            decimal platformFee = 0m;

            var policy = await BookingHelper.GetPlatformPolicyAsync(_unitOfWork);
            var commissionRate = policy.PlatformCommissionRatePercent;

            if (decisionType == "FULL_REFUND")
            {
                // Khiếu nại hợp lệ hoàn toàn: Hoàn 100% cho User, Chuyên gia 0đ, Sàn 0đ
                userRefund = totalAmount;
                expertReceivable = 0m;
                platformFee = 0m;

                escrow.EscrowStatus = EscrowStatus.Refunded;
                escrow.RefundAmount = userRefund;
                escrow.RefundReason = $"Phán quyết Admin giải quyết tranh chấp (Hoàn 100%): {request.Resolution.Trim()}";
                escrow.RefundedAt = nowUtc;

                booking.Status = BookingStatus.CANCELLED_BY_EXPERT;
            }
            else if (decisionType == "SETTLE_EXPERT")
            {
                // Bác bỏ khiếu nại: Chuyên gia hưởng đầy đủ thù lao (sau hoa hồng sàn), User 0đ
                userRefund = 0m;
                platformFee = Math.Round(totalAmount * (commissionRate / 100m), 2);
                expertReceivable = totalAmount - platformFee;

                escrow.EscrowStatus = EscrowStatus.Settled;
                escrow.SettledAt = nowUtc;

                booking.Status = BookingStatus.SETTLED;
            }
            else // PARTIAL
            {
                var ratePercent = request.RefundRatePercent ?? 50m;
                if (ratePercent <= 0 || ratePercent >= 100)
                {
                    throw new BadRequestException("INVALID_REFUND_RATE", "Tỷ lệ hoàn tiền cho User phải nằm trong khoảng (0, 100)%.");
                }

                userRefund = Math.Round(totalAmount * (ratePercent / 100m), 2);
                var remainingForExpertAndPlatform = totalAmount - userRefund;
                platformFee = Math.Round(remainingForExpertAndPlatform * (commissionRate / 100m), 2);
                expertReceivable = remainingForExpertAndPlatform - platformFee;

                escrow.EscrowStatus = EscrowStatus.PartiallyRefunded;
                escrow.RefundAmount = userRefund;
                escrow.RefundReason = $"Phán quyết Admin chia tỷ lệ (Hoàn {ratePercent}%): {request.Resolution.Trim()}";
                escrow.RefundedAt = nowUtc;
                escrow.SettledAt = nowUtc;

                booking.Status = BookingStatus.SETTLED;
                dispute.RefundRatePercent = ratePercent;
            }

            // Thực thi tài chính vào ví User (nếu có hoàn tiền)
            if (userRefund > 0)
            {
                var userWallet = await GetOrCreateWalletAsync(dispute.UserId, "USER");
                var before = userWallet.AvailableBalance;
                userWallet.AvailableBalance += userRefund;
                userWallet.UpdatedAt = nowUtc;
                _unitOfWork.Repository<UserWallet>().Update(userWallet);

                await _unitOfWork.Repository<WalletTransaction>().AddAsync(new WalletTransaction
                {
                    Id = Guid.NewGuid(),
                    WalletId = userWallet.Id,
                    UserId = dispute.UserId,
                    Amount = userRefund,
                    BalanceBefore = before,
                    BalanceAfter = userWallet.AvailableBalance,
                    TransactionType = "REFUND",
                    ReferenceId = dispute.Id,
                    Description = $"Tiền hoàn lại từ phán quyết khiếu nại ca tư vấn #{booking.BookingCode}",
                    CreatedAt = nowUtc
                });

                await SendNotificationAsync(
                    userId: dispute.UserId,
                    title: "Phán quyết khiếu nại đã hoàn tất – Hoàn tiền về ví",
                    message: $"Vụ việc khiếu nại #{booking.BookingCode} đã được giải quyết. Bạn nhận được khoản hoàn trả {userRefund:N0} VNĐ vào Ví hoàn tiền.",
                    type: "DISPUTE_REFUNDED",
                    targetUrl: $"/wallet");
            }

            // Thực thi tài chính vào ví Chuyên gia (nếu có thù lao)
            if (expertReceivable > 0)
            {
                var expertWallet = await GetOrCreateWalletAsync(dispute.ExpertUserId, "EXPERT");
                var before = expertWallet.AvailableBalance;
                expertWallet.AvailableBalance += expertReceivable;
                expertWallet.TotalEarned += expertReceivable;
                expertWallet.UpdatedAt = nowUtc;
                _unitOfWork.Repository<UserWallet>().Update(expertWallet);

                await _unitOfWork.Repository<WalletTransaction>().AddAsync(new WalletTransaction
                {
                    Id = Guid.NewGuid(),
                    WalletId = expertWallet.Id,
                    UserId = dispute.ExpertUserId,
                    Amount = expertReceivable,
                    BalanceBefore = before,
                    BalanceAfter = expertWallet.AvailableBalance,
                    TransactionType = "DISBURSEMENT",
                    ReferenceId = dispute.Id,
                    Description = $"Thù lao phán quyết khiếu nại ca tư vấn #{booking.BookingCode}",
                    CreatedAt = nowUtc
                });

                await SendNotificationAsync(
                    userId: dispute.ExpertUserId,
                    title: "Phán quyết khiếu nại đã hoàn tất – Giải ngân thù lao",
                    message: $"Vụ việc khiếu nại #{booking.BookingCode} đã được giải quyết. Số tiền {expertReceivable:N0} VNĐ đã được chuyển vào Ví thu nhập của bạn.",
                    type: "DISPUTE_SETTLED",
                    targetUrl: $"/expert/wallet");
            }

            // Cập nhật Dispute
            dispute.Status = decisionType switch
            {
                "FULL_REFUND" => "RESOLVED_FULL_REFUND",
                "SETTLE_EXPERT" => "RESOLVED_SETTLE_EXPERT",
                _ => "RESOLVED_PARTIAL"
            };
            dispute.AdminDecisionType = decisionType;
            dispute.AdminResolution = request.Resolution.Trim();
            dispute.UserRefundAmount = userRefund;
            dispute.ExpertSettlementAmount = expertReceivable;
            dispute.PlatformFeeAmount = platformFee;
            dispute.AdminNotes = request.AdminNotes?.Trim();
            dispute.ResolvedBy = adminId;
            dispute.ResolvedAt = nowUtc;
            dispute.UpdatedAt = nowUtc;

            escrow.UpdatedAt = nowUtc;
            booking.UpdatedAt = nowUtc;

            _unitOfWork.Repository<BookingDispute>().Update(dispute);
            _unitOfWork.Repository<EscrowTransaction>().Update(escrow);
            _unitOfWork.Repository<Booking>().Update(booking);

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Admin {AdminId} đã phán quyết khiếu nại {DisputeId} theo loại {DecisionType}. UserRefund = {UserRefund}, ExpertReceivable = {ExpertReceivable}",
                adminId, disputeId, decisionType, userRefund, expertReceivable);

            return await MapToDisputeDetailResponseAsync(dispute, booking);
        }

        // ════════════════════════════════════════════════════════════════════════
        //  3. QUẢN LÝ VÍ & LỆNH RÚT TIỀN (MỤC 5.3)
        // ════════════════════════════════════════════════════════════════════════

        public async Task<WalletResponse> GetMyWalletAsync(Guid userId, string? walletType = null)
        {
            var user = await _unitOfWork.Repository<User>().GetByIdAsync(userId)
                ?? throw new NotFoundException("Không tìm thấy thông tin người dùng.");

            var isExpert = string.Equals(user.UserRole, "expert", StringComparison.OrdinalIgnoreCase);

            var type = !string.IsNullOrWhiteSpace(walletType)
                ? walletType.Trim().ToUpperInvariant()
                : (isExpert ? "EXPERT" : "USER");

            // Nếu người dùng không phải Chuyên gia mà yêu cầu xem ví EXPERT -> Tự động chuyển về ví USER để tránh tạo bản ghi ví rác
            if (type == "EXPERT" && !isExpert)
            {
                type = "USER";
            }

            var wallet = await GetOrCreateWalletAsync(userId, type);

            var transactions = (await _unitOfWork.Repository<WalletTransaction>().FindAsync(t => t.WalletId == wallet.Id))
                .OrderByDescending(t => t.CreatedAt)
                .Take(10)
                .Select(t => new WalletTransactionResponse
                {
                    Id = t.Id,
                    WalletId = t.WalletId,
                    Amount = t.Amount,
                    BalanceBefore = t.BalanceBefore,
                    BalanceAfter = t.BalanceAfter,
                    TransactionType = t.TransactionType,
                    ReferenceId = t.ReferenceId,
                    Description = t.Description,
                    CreatedAt = t.CreatedAt
                }).ToList();

            return new WalletResponse
            {
                Id = wallet.Id,
                UserId = wallet.UserId,
                WalletType = wallet.WalletType,
                AvailableBalance = wallet.AvailableBalance,
                FrozenBalance = wallet.FrozenBalance,
                TotalEarned = wallet.TotalEarned,
                TotalWithdrawn = wallet.TotalWithdrawn,
                UpdatedAt = wallet.UpdatedAt,
                RecentTransactions = transactions
            };
        }

        public async Task<PagedResult<WalletTransactionResponse>> GetMyWalletTransactionsAsync(Guid userId, int page = 1, int size = 10)
        {
            var transactions = (await _unitOfWork.Repository<WalletTransaction>().FindAsync(t => t.UserId == userId))
                .OrderByDescending(t => t.CreatedAt).AsQueryable();

            var totalItems = transactions.Count();
            var paged = transactions.Skip((page - 1) * size).Take(size).ToList();

            var items = paged.Select(t => new WalletTransactionResponse
            {
                Id = t.Id,
                WalletId = t.WalletId,
                Amount = t.Amount,
                BalanceBefore = t.BalanceBefore,
                BalanceAfter = t.BalanceAfter,
                TransactionType = t.TransactionType,
                ReferenceId = t.ReferenceId,
                Description = t.Description,
                CreatedAt = t.CreatedAt
            }).ToList();

            return new PagedResult<WalletTransactionResponse>
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

        public async Task<WithdrawalResponse> RequestWithdrawalAsync(Guid expertUserId, CreateWithdrawalRequest request)
        {
            // Business Rule: "Chuyên gia chỉ được rút tiền từ ví nền tảng về tài khoản ngân hàng khi tài khoản đã hoàn tất định danh công dân (eKYC) và cung cấp Mã số thuế cá nhân."
            var user = await _unitOfWork.Repository<User>().GetByIdAsync(expertUserId)
                ?? throw new NotFoundException("Không tìm thấy thông tin người dùng.");

            if (!user.IsVerified)
            {
                throw new BadRequestException("EKYC_REQUIRED", "Bạn cần hoàn tất định danh công dân điện tử (eKYC) trước khi thực hiện rút tiền.");
            }

            if (string.IsNullOrWhiteSpace(user.TaxIdNumber))
            {
                throw new BadRequestException("TAX_ID_REQUIRED", "Bạn cần cập nhật Mã số thuế cá nhân hợp lệ trong hồ sơ trước khi thực hiện rút tiền.");
            }

            var policy = await BookingHelper.GetPlatformPolicyAsync(_unitOfWork);
            var minWithdrawal = policy.ExpertMinWithdrawalVnd > 0m ? policy.ExpertMinWithdrawalVnd : 500000m;
            if (request.Amount < minWithdrawal)
            {
                throw new BadRequestException("MIN_WITHDRAWAL_NOT_MET",
                    $"Số tiền rút tối thiểu là {minWithdrawal:N0} VNĐ. Số tiền bạn yêu cầu: {request.Amount:N0} VNĐ.");
            }

            var wallet = await GetOrCreateWalletAsync(expertUserId, "EXPERT");
            if (wallet.AvailableBalance < request.Amount)
            {
                throw new BadRequestException("INSUFFICIENT_BALANCE",
                    $"Số dư khả dụng ({wallet.AvailableBalance:N0} VNĐ) không đủ để thực hiện lệnh rút {request.Amount:N0} VNĐ.");
            }

            if (string.IsNullOrWhiteSpace(request.BankName) ||
                string.IsNullOrWhiteSpace(request.AccountNumber) ||
                string.IsNullOrWhiteSpace(request.AccountName))
            {
                throw new BadRequestException("BANK_INFO_REQUIRED", "Vui lòng nhập đầy đủ tên ngân hàng, số tài khoản và tên chủ tài khoản thụ hưởng.");
            }

            var nowUtc = DateTimeOffset.UtcNow;

            // Tạm đóng băng số tiền yêu cầu rút
            var before = wallet.AvailableBalance;
            wallet.AvailableBalance -= request.Amount;
            wallet.FrozenBalance += request.Amount;
            wallet.UpdatedAt = nowUtc;
            _unitOfWork.Repository<UserWallet>().Update(wallet);

            var withdrawal = new WithdrawalRequest
            {
                Id = Guid.NewGuid(),
                UserId = expertUserId,
                Amount = request.Amount,
                BankName = request.BankName.Trim().ToUpperInvariant(),
                AccountNumber = request.AccountNumber.Trim(),
                AccountName = request.AccountName.Trim().ToUpperInvariant(),
                Status = "PENDING",
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc
            };
            await _unitOfWork.Repository<WithdrawalRequest>().AddAsync(withdrawal);

            await _unitOfWork.Repository<WalletTransaction>().AddAsync(new WalletTransaction
            {
                Id = Guid.NewGuid(),
                WalletId = wallet.Id,
                UserId = expertUserId,
                Amount = -request.Amount,
                BalanceBefore = before,
                BalanceAfter = wallet.AvailableBalance,
                TransactionType = "WITHDRAWAL",
                ReferenceId = withdrawal.Id,
                Description = $"Tạm giữ số dư để xử lý lệnh rút tiền về ngân hàng {withdrawal.BankName} - STK {withdrawal.AccountNumber}",
                CreatedAt = nowUtc
            });

            // Bắn thông báo cho Admin
            var admins = (await _unitOfWork.Repository<User>().FindAsync(u => u.UserRole == "admin")).ToList();
            foreach (var admin in admins)
            {
                await SendNotificationAsync(
                    userId: admin.UserId,
                    title: "Lệnh rút tiền mới từ chuyên gia",
                    message: $"Chuyên gia {user.FullName} yêu cầu rút {request.Amount:N0} VNĐ về ngân hàng {withdrawal.BankName}.",
                    type: "WITHDRAWAL_REQUEST_ADMIN",
                    targetUrl: "/admin/withdrawals");
            }

            await _unitOfWork.SaveChangesAsync();

            return MapToWithdrawalResponse(withdrawal, user);
        }

        public async Task<WithdrawalResponse> RequestUserRefundWithdrawalAsync(Guid userId, CreateWithdrawalRequest request)
        {
            var user = await _unitOfWork.Repository<User>().GetByIdAsync(userId)
                ?? throw new NotFoundException("Không tìm thấy thông tin người dùng.");

            const decimal minRefundWithdrawal = 50000m; // Ngưỡng rút tiền hoàn tối thiểu 50.000 VNĐ
            if (request.Amount < minRefundWithdrawal)
            {
                throw new BadRequestException("MIN_WITHDRAWAL_NOT_MET",
                    $"Số tiền rút tiền hoàn tối thiểu là {minRefundWithdrawal:N0} VNĐ. Số tiền bạn yêu cầu: {request.Amount:N0} VNĐ.");
            }

            var wallet = await GetOrCreateWalletAsync(userId, "USER");
            if (wallet.AvailableBalance < request.Amount)
            {
                throw new BadRequestException("INSUFFICIENT_BALANCE",
                    $"Số dư hoàn tiền khả dụng trong ví ({wallet.AvailableBalance:N0} VNĐ) không đủ để thực hiện lệnh rút {request.Amount:N0} VNĐ.");
            }

            if (string.IsNullOrWhiteSpace(request.BankName) ||
                string.IsNullOrWhiteSpace(request.AccountNumber) ||
                string.IsNullOrWhiteSpace(request.AccountName))
            {
                throw new BadRequestException("BANK_INFO_REQUIRED", "Vui lòng nhập đầy đủ tên ngân hàng, số tài khoản và tên chủ tài khoản thụ hưởng.");
            }

            var nowUtc = DateTimeOffset.UtcNow;

            // Tạm đóng băng số tiền yêu cầu rút từ ví USER
            var before = wallet.AvailableBalance;
            wallet.AvailableBalance -= request.Amount;
            wallet.FrozenBalance += request.Amount;
            wallet.UpdatedAt = nowUtc;
            _unitOfWork.Repository<UserWallet>().Update(wallet);

            var withdrawal = new WithdrawalRequest
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Amount = request.Amount,
                BankName = request.BankName.Trim().ToUpperInvariant(),
                AccountNumber = request.AccountNumber.Trim(),
                AccountName = request.AccountName.Trim().ToUpperInvariant(),
                Status = "PENDING",
                AdminNotes = "Khách hàng yêu cầu rút tiền hoàn từ ví cá nhân",
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc
            };
            await _unitOfWork.Repository<WithdrawalRequest>().AddAsync(withdrawal);

            await _unitOfWork.Repository<WalletTransaction>().AddAsync(new WalletTransaction
            {
                Id = Guid.NewGuid(),
                WalletId = wallet.Id,
                UserId = userId,
                Amount = -request.Amount,
                BalanceBefore = before,
                BalanceAfter = wallet.AvailableBalance,
                TransactionType = "WITHDRAWAL",
                ReferenceId = withdrawal.Id,
                Description = $"Tạm giữ số dư để xử lý lệnh rút tiền hoàn về ngân hàng {withdrawal.BankName} - STK {withdrawal.AccountNumber}",
                CreatedAt = nowUtc
            });

            // Bắn thông báo cho Admin
            var admins = (await _unitOfWork.Repository<User>().FindAsync(u => u.UserRole == "admin")).ToList();
            foreach (var admin in admins)
            {
                await SendNotificationAsync(
                    userId: admin.UserId,
                    title: "Khách hàng yêu cầu rút tiền hoàn",
                    message: $"Người dùng {user.FullName} gửi yêu cầu rút hoàn {request.Amount:N0} VNĐ về STK {withdrawal.AccountNumber} ({withdrawal.BankName}).",
                    type: "USER_WITHDRAWAL_REQUEST",
                    targetUrl: "/admin/withdrawals");
            }

            await _unitOfWork.SaveChangesAsync();

            return MapToWithdrawalResponse(withdrawal, user);
        }

        public async Task<PagedResult<WithdrawalResponse>> GetWithdrawalsAsync(Guid currentUserId, string currentUserRole, string? status = null, int page = 1, int size = 10)
        {
            var isAdmin = string.Equals(currentUserRole, "admin", StringComparison.OrdinalIgnoreCase);
            var query = (await _unitOfWork.Repository<WithdrawalRequest>().GetAllAsync()).AsQueryable();

            if (!isAdmin)
            {
                query = query.Where(r => r.UserId == currentUserId);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(r => r.Status.Equals(status.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            query = query.OrderByDescending(r => r.CreatedAt);
            var totalItems = query.Count();
            var paged = query.Skip((page - 1) * size).Take(size).ToList();

            var userIds = paged.Select(r => r.UserId).Distinct().ToList();
            var users = (await _unitOfWork.Repository<User>().FindAsync(u => userIds.Contains(u.UserId)))
                .ToDictionary(u => u.UserId);

            var items = paged.Select(r =>
            {
                users.TryGetValue(r.UserId, out var u);
                return MapToWithdrawalResponse(r, u);
            }).ToList();

            return new PagedResult<WithdrawalResponse>
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

        public async Task<WithdrawalResponse> AdminProcessWithdrawalAsync(Guid adminId, Guid withdrawalId, AdminProcessWithdrawalRequest request)
        {
            var withdrawal = await _unitOfWork.Repository<WithdrawalRequest>().GetByIdAsync(withdrawalId)
                ?? throw new NotFoundException($"Không tìm thấy lệnh rút tiền với Id = {withdrawalId}.");

            if (withdrawal.Status != "PENDING")
            {
                throw new BadRequestException("ALREADY_PROCESSED", $"Lệnh rút tiền này đã được xử lý với trạng thái '{withdrawal.Status}'.");
            }

            var action = request.Action.Trim().ToUpperInvariant();
            if (action != "APPROVE" && action != "REJECT")
            {
                throw new BadRequestException("INVALID_ACTION", "Hành động phải là 'APPROVE' hoặc 'REJECT'.");
            }

            var user = await _unitOfWork.Repository<User>().GetByIdAsync(withdrawal.UserId);
            var isExpert = user != null && string.Equals(user.UserRole, "expert", StringComparison.OrdinalIgnoreCase);
            var targetWalletType = isExpert ? "EXPERT" : "USER";
            var wallet = await GetOrCreateWalletAsync(withdrawal.UserId, targetWalletType);
            var nowUtc = DateTimeOffset.UtcNow;

            if (action == "APPROVE")
            {
                // Trừ số dư đóng băng, cộng vào tổng tiền đã rút
                wallet.FrozenBalance = Math.Max(0, wallet.FrozenBalance - withdrawal.Amount);
                wallet.TotalWithdrawn += withdrawal.Amount;
                wallet.UpdatedAt = nowUtc;
                _unitOfWork.Repository<UserWallet>().Update(wallet);

                withdrawal.Status = "COMPLETED";
                withdrawal.TransactionReference = request.TransactionReference?.Trim();
                withdrawal.AdminNotes = request.AdminNotes?.Trim();
                withdrawal.ProcessedBy = adminId;
                withdrawal.ProcessedAt = nowUtc;
                withdrawal.UpdatedAt = nowUtc;
                _unitOfWork.Repository<WithdrawalRequest>().Update(withdrawal);

                var targetUrl = isExpert ? "/expert/wallet" : "/wallet";
                await SendNotificationAsync(
                    userId: withdrawal.UserId,
                    title: "Lệnh rút tiền đã hoàn tất thành công",
                    message: $"Khoản tiền {withdrawal.Amount:N0} VNĐ đã được chuyển về tài khoản {withdrawal.BankName} - {withdrawal.AccountNumber}. Mã tham chiếu: {withdrawal.TransactionReference ?? "N/A"}.",
                    type: "WITHDRAWAL_SUCCESS",
                    targetUrl: targetUrl);
            }
            else // REJECT
            {
                if (string.IsNullOrWhiteSpace(request.RejectionReason))
                {
                    throw new BadRequestException("REASON_REQUIRED", "Bắt buộc nhập lý do từ chối lệnh rút tiền.");
                }

                // Hoàn lại tiền từ FrozenBalance về AvailableBalance
                var before = wallet.AvailableBalance;
                wallet.FrozenBalance = Math.Max(0, wallet.FrozenBalance - withdrawal.Amount);
                wallet.AvailableBalance += withdrawal.Amount;
                wallet.UpdatedAt = nowUtc;
                _unitOfWork.Repository<UserWallet>().Update(wallet);

                withdrawal.Status = "REJECTED";
                withdrawal.RejectionReason = request.RejectionReason.Trim();
                withdrawal.AdminNotes = request.AdminNotes?.Trim();
                withdrawal.ProcessedBy = adminId;
                withdrawal.ProcessedAt = nowUtc;
                withdrawal.UpdatedAt = nowUtc;
                _unitOfWork.Repository<WithdrawalRequest>().Update(withdrawal);

                await _unitOfWork.Repository<WalletTransaction>().AddAsync(new WalletTransaction
                {
                    Id = Guid.NewGuid(),
                    WalletId = wallet.Id,
                    UserId = withdrawal.UserId,
                    Amount = withdrawal.Amount,
                    BalanceBefore = before,
                    BalanceAfter = wallet.AvailableBalance,
                    TransactionType = "WITHDRAWAL_REFUND",
                    ReferenceId = withdrawal.Id,
                    Description = $"Hoàn trả số dư do lệnh rút tiền bị từ chối: {withdrawal.RejectionReason}",
                    CreatedAt = nowUtc
                });

                var targetUrl = isExpert ? "/expert/wallet" : "/wallet";
                await SendNotificationAsync(
                    userId: withdrawal.UserId,
                    title: "Lệnh rút tiền bị từ chối – Số dư đã được hoàn lại",
                    message: $"Lệnh rút {withdrawal.Amount:N0} VNĐ đã bị từ chối (Lý do: {withdrawal.RejectionReason}). Số tiền đã được hoàn trả lại ví khả dụng.",
                    type: "WITHDRAWAL_REJECTED",
                    targetUrl: targetUrl);
            }

            await _unitOfWork.SaveChangesAsync();

            return MapToWithdrawalResponse(withdrawal, user);
        }

        // ════════════════════════════════════════════════════════════════════════
        //  PRIVATE HELPERS & MAPPERS
        // ════════════════════════════════════════════════════════════════════════

        private async Task<UserWallet> GetOrCreateWalletAsync(Guid userId, string walletType)
        {
            var user = await _unitOfWork.Repository<User>().GetByIdAsync(userId);
            var isExpert = user != null && string.Equals(user.UserRole, "expert", StringComparison.OrdinalIgnoreCase);

            var resolvedType = (walletType.Equals("EXPERT", StringComparison.OrdinalIgnoreCase) && !isExpert)
                ? "USER"
                : walletType.Trim().ToUpperInvariant();

            var wallets = await _unitOfWork.Repository<UserWallet>().FindAsync(w => w.UserId == userId && w.WalletType == resolvedType);
            var wallet = wallets.FirstOrDefault();

            if (wallet == null)
            {
                wallet = new UserWallet
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    WalletType = resolvedType,
                    AvailableBalance = 0m,
                    FrozenBalance = 0m,
                    TotalEarned = 0m,
                    TotalWithdrawn = 0m,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                await _unitOfWork.Repository<UserWallet>().AddAsync(wallet);
                await _unitOfWork.SaveChangesAsync();
            }

            return wallet;
        }

        private async Task SendNotificationAsync(Guid userId, string title, string message, string type, string? targetUrl = null)
        {
            await _unitOfWork.Repository<SystemNotification>().AddAsync(new SystemNotification
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

        private async Task<DisputeDetailResponse> MapToDisputeDetailResponseAsync(BookingDispute d, Booking? b)
        {
            var taxpayer = await _unitOfWork.Repository<User>().GetByIdAsync(d.UserId);
            var expert = await _unitOfWork.Repository<User>().GetByIdAsync(d.ExpertUserId);

            List<string> userEvidence = new();
            if (!string.IsNullOrEmpty(d.EvidenceUrlsJson))
            {
                try { userEvidence = JsonSerializer.Deserialize<List<string>>(d.EvidenceUrlsJson) ?? new(); }
                catch { /* ignore */ }
            }

            List<string> expertEvidence = new();
            if (!string.IsNullOrEmpty(d.ExpertEvidenceUrlsJson))
            {
                try { expertEvidence = JsonSerializer.Deserialize<List<string>>(d.ExpertEvidenceUrlsJson) ?? new(); }
                catch { /* ignore */ }
            }

            var nowUtc = DateTimeOffset.UtcNow;
            int? remainingSeconds = null;
            if (d.ExpertResponseDeadline.HasValue && d.Status == "PENDING_EXPERT_RESPONSE")
            {
                remainingSeconds = (int)Math.Max(0, (d.ExpertResponseDeadline.Value - nowUtc).TotalSeconds);
            }

            return new DisputeDetailResponse
            {
                Id = d.Id,
                BookingId = d.BookingId,
                BookingCode = b?.BookingCode ?? string.Empty,
                TotalFee = b?.Fee ?? 0m,
                UserId = d.UserId,
                TaxpayerFullName = taxpayer?.FullName ?? "Khách hàng",
                TaxpayerPhone = taxpayer?.PhoneNumber,
                ExpertUserId = d.ExpertUserId,
                ExpertFullName = expert?.FullName ?? "Chuyên gia",
                Reason = d.Reason,
                Description = d.Description,
                EvidenceUrls = userEvidence,
                Status = d.Status,
                ExpertResponse = d.ExpertResponse,
                ExpertEvidenceUrls = expertEvidence,
                ExpertRespondedAt = d.ExpertRespondedAt,
                ExpertResponseDeadline = d.ExpertResponseDeadline,
                ExpertRemainingSeconds = remainingSeconds,
                AdminDecisionType = d.AdminDecisionType,
                AdminResolution = d.AdminResolution,
                RefundRatePercent = d.RefundRatePercent,
                UserRefundAmount = d.UserRefundAmount,
                ExpertSettlementAmount = d.ExpertSettlementAmount,
                PlatformFeeAmount = d.PlatformFeeAmount,
                AdminNotes = d.AdminNotes,
                ResolvedBy = d.ResolvedBy,
                ResolvedAt = d.ResolvedAt,
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt
            };
        }

        private static WithdrawalResponse MapToWithdrawalResponse(WithdrawalRequest r, User? user)
        {
            return new WithdrawalResponse
            {
                Id = r.Id,
                UserId = r.UserId,
                UserFullName = user?.FullName ?? string.Empty,
                Amount = r.Amount,
                BankName = r.BankName,
                AccountNumber = r.AccountNumber,
                AccountName = r.AccountName,
                Status = r.Status,
                RejectionReason = r.RejectionReason,
                TransactionReference = r.TransactionReference,
                AdminNotes = r.AdminNotes,
                CreatedAt = r.CreatedAt,
                ProcessedAt = r.ProcessedAt
            };
        }
    }
}
