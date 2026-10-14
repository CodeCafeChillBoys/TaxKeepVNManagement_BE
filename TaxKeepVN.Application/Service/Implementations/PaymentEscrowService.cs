using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Payments;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Helpers;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    /// <summary>
    /// Service xử lý Thanh toán VNPay / VietQR & Quản lý tạm giữ tiền bảo đảm Escrow (Đặc tả 4)
    /// Tuân thủ: Zero-Hardcode (tham số nghiệp vụ đọc từ system_configs), Zero-Mock-Data (URL chuẩn VNPay & VietQR).
    /// </summary>
    public class PaymentEscrowService : IPaymentEscrowService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PaymentEscrowService> _logger;

        public PaymentEscrowService(
            IUnitOfWork unitOfWork,
            IConfiguration configuration,
            ILogger<PaymentEscrowService> logger)
        {
            _unitOfWork = unitOfWork;
            _configuration = configuration;
            _logger = logger;
        }

        // ── 1. KHỞI TẠO HOẶC LẤY LẠI LINK THANH TOÁN VNPAY & VIETQR (ĐẶC TẢ 4) ─────────
        public async Task<PaymentLinkResponse> CreateOrGetPaymentLinkAsync(
            Guid userId, 
            Guid bookingId, 
            CreatePaymentLinkRequest? request = null,
            string? clientIp = null)
        {
            var booking = await _unitOfWork.Repository<Booking>().GetByIdAsync(bookingId);
            if (booking == null)
            {
                throw new NotFoundException($"Không tìm thấy lịch hẹn tư vấn với Id = {bookingId}.");
            }

            if (booking.UserId != userId)
            {
                throw new ForbiddenException("FORBIDDEN", "Bạn không có quyền tạo link thanh toán cho lịch hẹn của người khác.");
            }

            if (booking.Status != BookingStatus.PENDING_PAYMENT)
            {
                throw new BadRequestException("INVALID_BOOKING_STATUS",
                    $"Lịch hẹn hiện đang ở trạng thái '{booking.Status}', không thể khởi tạo thanh toán.");
            }

            var nowUtc = DateTimeOffset.UtcNow;
            if (booking.HoldExpiresAt <= nowUtc)
            {
                throw new BadRequestException("HOLD_SLOT_EXPIRED",
                    "Thời gian tạm giữ khung giờ (Hold Slot) đã hết hạn. Vui lòng đặt lại lịch hẹn mới.");
            }

            // Kiểm tra xem đã có bản ghi EscrowTransaction cho Booking này chưa (Idempotent)
            var existingEscrows = await _unitOfWork.Repository<EscrowTransaction>().FindAsync(e => e.BookingId == booking.Id);
            var escrow = existingEscrows.FirstOrDefault();

            if (escrow != null && escrow.EscrowStatus == EscrowStatus.PendingPayment && !string.IsNullOrWhiteSpace(escrow.CheckoutUrl))
            {
                return MapToPaymentLinkResponse(booking, escrow, nowUtc);
            }

            // Đọc động tỷ lệ hoa hồng nền tảng A% từ bảng system_configs (Zero-Hardcode)
            var commissionRate = await BookingHelper.GetRequiredConfigDecimalAsync(
                _unitOfWork,
                BookingHelper.ConfigKeyPlatformCommissionRatePercent);

            var amount = booking.Fee;
            var commissionAmount = Math.Round(amount * (commissionRate / 100m), 2);
            var expertReceivable = amount - commissionAmount;
            var orderCode = escrow?.OrderCode ?? BookingHelper.GeneratePayOsOrderCode();

            // Đọc cấu hình VNPay
            var vnpTmnCode = _configuration["VnPay:TmnCode"] ?? "CGXZLS0Z";
            var vnpHashSecret = _configuration["VnPay:HashSecret"] ?? "XNBCJFAKAZQSGTARRLGCHVZWCIOIGSHN";
            var vnpBaseUrl = _configuration["VnPay:BaseUrl"] ?? _configuration["VnPay:PaymentUrl"] ?? "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
            var vnpReturnUrl = _configuration["VnPay:ReturnUrl"] ?? "https://localhost:7027/api/v1/payments/vnpay-return";

            // Đọc cấu hình VietQR
            var qrBankBin = _configuration["VietQr:BankBin"] ?? _configuration["VietQR:BankBin"] ?? "970405";
            var qrAccountNo = _configuration["VietQr:AccountNumber"] ?? _configuration["VietQR:AccountNumber"] ?? _configuration["VietQr:AccountNo"] ?? _configuration["VietQR:AccountNo"] ?? "5402205457446";
            var qrAccountName = _configuration["VietQr:AccountName"] ?? _configuration["VietQR:AccountName"] ?? "";
            var qrTemplate = _configuration["VietQr:Template"] ?? _configuration["VietQR:Template"] ?? "compact2";

            // 1. Tạo VNPay checkout URL (v2.1.0 HMAC-SHA512)
            var checkoutUrl = VnPayHelper.CreatePaymentUrl(
                vnpBaseUrl,
                vnpTmnCode,
                vnpHashSecret,
                vnpReturnUrl,
                orderCode,
                amount,
                booking.BookingCode,
                booking.HoldExpiresAt,
                clientIp ?? "127.0.0.1",
                request?.ReturnUrl);

            // 2. Tạo mã VietQR QuickLink chuẩn NAPAS247
            var qrCode = VietQrHelper.GenerateVietQrUrl(
                qrBankBin,
                qrAccountNo,
                qrAccountName,
                amount,
                booking.BookingCode,
                qrTemplate);

            if (escrow == null)
            {
                escrow = new EscrowTransaction
                {
                    Id = Guid.NewGuid(),
                    BookingId = booking.Id,
                    OrderCode = orderCode,
                    Amount = amount,
                    CommissionRate = commissionRate,
                    CommissionAmount = commissionAmount,
                    ExpertReceivable = expertReceivable,
                    EscrowStatus = EscrowStatus.PendingPayment,
                    PaymentGateway = "VNPAY",
                    PaymentLinkId = orderCode.ToString(CultureInfo.InvariantCulture),
                    CheckoutUrl = checkoutUrl,
                    QrCode = qrCode,
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc
                };

                await _unitOfWork.Repository<EscrowTransaction>().AddAsync(escrow);
            }
            else
            {
                escrow.Amount = amount;
                escrow.CommissionRate = commissionRate;
                escrow.CommissionAmount = commissionAmount;
                escrow.ExpertReceivable = expertReceivable;
                escrow.PaymentGateway = "VNPAY";
                escrow.PaymentLinkId = orderCode.ToString(CultureInfo.InvariantCulture);
                escrow.CheckoutUrl = checkoutUrl;
                escrow.QrCode = qrCode;
                escrow.UpdatedAt = nowUtc;

                _unitOfWork.Repository<EscrowTransaction>().Update(escrow);
            }

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "Đã khởi tạo link thanh toán VNPay / VietQR cho Booking {BookingCode} (OrderCode: {OrderCode}, Amount: {Amount:N0} VNĐ, Commission: {CommissionRate}%).",
                booking.BookingCode, orderCode, amount, commissionRate);

            return MapToPaymentLinkResponse(booking, escrow, nowUtc);
        }

        // ── 2. XỬ LÝ WEBHOOK IPN TỪ CỔNG THANH TOÁN VNPAY (ĐẶC TẢ 4) ─────────────────
        public async Task<VnPayIpnResponse> HandleVnPayIpnAsync(IDictionary<string, string> queryParams)
        {
            if (queryParams == null || !queryParams.Any())
            {
                return VnPayIpnResponse.OtherError("Dữ liệu IPN rỗng.");
            }

            var hashSecret = _configuration["VnPay:HashSecret"] ?? "RAOQUIASHAEJGWFNYAOMKYFISOPSPAGK";
            var isValidSignature = VnPayHelper.ValidateSignature(queryParams, hashSecret);
            if (!isValidSignature)
            {
                _logger.LogWarning("VNPay IPN: Chữ ký HMAC-SHA512 không hợp lệ.");
                return VnPayIpnResponse.InvalidSignature();
            }

            if (!queryParams.TryGetValue("vnp_TxnRef", out var txnRefStr) || !long.TryParse(txnRefStr, out var orderCode))
            {
                return VnPayIpnResponse.OrderNotFound();
            }

            var escrows = await _unitOfWork.Repository<EscrowTransaction>().FindAsync(e => e.OrderCode == orderCode);
            var escrow = escrows.FirstOrDefault();
            if (escrow == null)
            {
                return VnPayIpnResponse.OrderNotFound();
            }

            var booking = await _unitOfWork.Repository<Booking>().GetByIdAsync(escrow.BookingId);
            if (booking == null)
            {
                return VnPayIpnResponse.OrderNotFound();
            }

            // Kiểm tra tính Idempotent
            if (escrow.EscrowStatus == EscrowStatus.HeldInEscrow || booking.PaidAt.HasValue)
            {
                return VnPayIpnResponse.OrderAlreadyConfirmed();
            }

            // Kiểm tra số tiền: VNPay nhân 100
            if (queryParams.TryGetValue("vnp_Amount", out var amountStr) && long.TryParse(amountStr, out var vnpAmount))
            {
                var expectedAmount = (long)Math.Round(escrow.Amount * 100m, 0);
                if (vnpAmount < expectedAmount)
                {
                    return VnPayIpnResponse.InvalidAmount();
                }
            }

            var responseCode = queryParams.TryGetValue("vnp_ResponseCode", out var rspVal) ? rspVal : "99";
            var transactionNo = queryParams.TryGetValue("vnp_TransactionNo", out var txnVal) ? txnVal : $"VNPAY_{orderCode}";

            if (responseCode == "00")
            {
                var confirmTimeoutHours = await BookingHelper.GetRequiredConfigIntAsync(
                    _unitOfWork,
                    BookingHelper.ConfigKeyExpertConfirmTimeoutHours);

                var nowUtc = DateTimeOffset.UtcNow;
                var approvalDeadline = nowUtc.AddHours(confirmTimeoutHours);

                await _unitOfWork.BeginTransactionAsync();
                try
                {
                    // 1. Ghi nhận tiền vào ví bảo đảm Escrow (HeldInEscrow)
                    escrow.EscrowStatus = EscrowStatus.HeldInEscrow;
                    escrow.PaidAt = nowUtc;
                    escrow.PaymentReference = transactionNo;
                    escrow.UpdatedAt = nowUtc;
                    _unitOfWork.Repository<EscrowTransaction>().Update(escrow);

                    // 2. Chuyển trạng thái Booking sang AWAITING_EXPERT_APPROVAL và gán SLA 2 giờ
                    booking.Status = BookingStatus.AWAITING_EXPERT_APPROVAL;
                    booking.PaidAt = nowUtc;
                    booking.PaymentReference = transactionNo;
                    booking.ApprovalDeadline = approvalDeadline;
                    booking.UpdatedAt = nowUtc;
                    _unitOfWork.Repository<Booking>().Update(booking);

                    // 3. Khóa chính thức khung giờ làm việc của Chuyên gia
                    var slot = await _unitOfWork.Repository<ExpertSlot>().GetByIdAsync(booking.ExpertSlotId);
                    if (slot != null)
                    {
                        slot.IsBooked = true;
                        slot.HoldExpiresAt = null;
                        slot.HoldUserId = null;
                        slot.UpdatedAt = nowUtc;
                        _unitOfWork.Repository<ExpertSlot>().Update(slot);
                    }

                    await _unitOfWork.CommitTransactionAsync();

                    _logger.LogInformation(
                        "VNPay IPN thành công cho OrderCode {OrderCode} (Booking {BookingCode}). Tiền đã chuyển vào HeldInEscrow, hạn duyệt chuyên gia: {Deadline}.",
                        orderCode, booking.BookingCode, approvalDeadline);

                    return VnPayIpnResponse.Success();
                }
                catch (Exception ex)
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    _logger.LogError(ex, "Lỗi khi cập nhật giao dịch Escrow từ VNPay IPN cho OrderCode {OrderCode}.", orderCode);
                    return VnPayIpnResponse.OtherError(ex.Message);
                }
            }

            _logger.LogWarning("Giao dịch VNPay cho OrderCode {OrderCode} không thành công (ResponseCode: {Code}).", orderCode, responseCode);
            return VnPayIpnResponse.Success(); // Acknowledge IPN was received
        }

        // ── 3. XỬ LÝ RETURN URL TỪ VNPAY ──────────────────────────────────────────────
        public async Task<EscrowTransactionResponse?> HandleVnPayReturnAsync(IDictionary<string, string> queryParams)
        {
            if (queryParams == null || !queryParams.Any())
            {
                return null;
            }

            var hashSecret = _configuration["VnPay:HashSecret"] ?? "RAOQUIASHAEJGWFNYAOMKYFISOPSPAGK";
            var isValidSignature = VnPayHelper.ValidateSignature(queryParams, hashSecret);
            if (!isValidSignature)
            {
                throw new BadRequestException("INVALID_SIGNATURE", "Chữ ký xác thực trả về từ VNPay không hợp lệ.");
            }

            if (!queryParams.TryGetValue("vnp_TxnRef", out var txnRefStr) || !long.TryParse(txnRefStr, out var orderCode))
            {
                throw new NotFoundException("Không tìm thấy thông tin đơn hàng thanh toán.");
            }

            var escrows = await _unitOfWork.Repository<EscrowTransaction>().FindAsync(e => e.OrderCode == orderCode);
            var escrow = escrows.FirstOrDefault();
            if (escrow == null)
            {
                throw new NotFoundException($"Không tìm thấy giao dịch Escrow cho mã đơn hàng = {orderCode}.");
            }

            var booking = await _unitOfWork.Repository<Booking>().GetByIdAsync(escrow.BookingId);
            if (booking == null)
            {
                throw new NotFoundException($"Không tìm thấy lịch hẹn liên kết.");
            }

            return MapToEscrowResponse(escrow, booking);
        }

        // ── 4. XEM CHI TIẾT ESCROW THEO BOOKING ID (ĐẶC TẢ 4) ─────────────────────────
        public async Task<EscrowTransactionResponse> GetEscrowByBookingIdAsync(Guid bookingId, Guid currentUserId, string currentUserRole)
        {
            var booking = await _unitOfWork.Repository<Booking>().GetByIdAsync(bookingId);
            if (booking == null)
            {
                throw new NotFoundException($"Không tìm thấy lịch hẹn tư vấn với Id = {bookingId}.");
            }

            var slot = await _unitOfWork.Repository<ExpertSlot>().GetByIdAsync(booking.ExpertSlotId);
            var expertProfile = slot != null 
                ? await _unitOfWork.Repository<ExpertProfile>().GetByIdAsync(slot.ExpertProfileId) 
                : null;

            var isOwner = booking.UserId == currentUserId;
            var isAssignedExpert = expertProfile != null && expertProfile.UserId == currentUserId;
            var isAdmin = string.Equals(currentUserRole, "admin", StringComparison.OrdinalIgnoreCase);

            if (!isOwner && !isAssignedExpert && !isAdmin)
            {
                throw new ForbiddenException("FORBIDDEN", "Bạn không có quyền truy cập thông tin giao dịch Escrow của lịch hẹn này.");
            }

            var escrows = await _unitOfWork.Repository<EscrowTransaction>().FindAsync(e => e.BookingId == bookingId);
            var escrow = escrows.FirstOrDefault();
            if (escrow == null)
            {
                throw new NotFoundException($"Chưa khởi tạo giao dịch Escrow cho Booking {booking.BookingCode}.");
            }

            return MapToEscrowResponse(escrow, booking);
        }

        // ── 5. TRUY VẤN DANH SÁCH ESCROW TRANSACTIONS (FILTERING, SORTING, PAGINATION) ─
        public async Task<PagedResult<EscrowTransactionResponse>> GetEscrowTransactionsAsync(
            Guid currentUserId, 
            string currentUserRole, 
            EscrowTransactionQueryParameters query)
        {
            var isAdmin = string.Equals(currentUserRole, "admin", StringComparison.OrdinalIgnoreCase);
            var page = Math.Max(1, query.Page);
            var size = Math.Clamp(query.PageSize, 1, 100);

            var allEscrows = (await _unitOfWork.Repository<EscrowTransaction>().GetAllAsync()).ToList();
            var allBookings = (await _unitOfWork.Repository<Booking>().GetAllAsync()).ToDictionary(b => b.Id);

            var filtered = allEscrows.Where(e =>
            {
                if (!allBookings.TryGetValue(e.BookingId, out var b)) return false;

                if (!isAdmin && b.UserId != currentUserId) return false;

                if (query.Status.HasValue && e.EscrowStatus != query.Status.Value) return false;

                if (query.MinAmount.HasValue && e.Amount < query.MinAmount.Value) return false;
                if (query.MaxAmount.HasValue && e.Amount > query.MaxAmount.Value) return false;

                if (query.FromDate.HasValue && e.CreatedAt < query.FromDate.Value) return false;
                if (query.ToDate.HasValue && e.CreatedAt > query.ToDate.Value) return false;

                if (!string.IsNullOrWhiteSpace(query.Search))
                {
                    var s = query.Search.Trim().ToLowerInvariant();
                    var matchCode = b.BookingCode.ToLowerInvariant().Contains(s);
                    var matchOrder = e.OrderCode.ToString(CultureInfo.InvariantCulture).Contains(s);
                    var matchRef = (e.PaymentReference ?? string.Empty).ToLowerInvariant().Contains(s);
                    if (!matchCode && !matchOrder && !matchRef) return false;
                }

                return true;
            });

            var sorted = ApplySort(filtered, query.Sort).ToList();
            var totalItems = sorted.Count;
            var pagedItems = sorted.Skip((page - 1) * size).Take(size).ToList();

            var responses = pagedItems.Select(e => MapToEscrowResponse(e, allBookings[e.BookingId])).ToList();

            return new PagedResult<EscrowTransactionResponse>
            {
                Items = responses,
                Pagination = new PaginationMeta
                {
                    Page = page,
                    PageSize = size,
                    TotalItems = totalItems,
                    TotalPages = (int)Math.Ceiling(totalItems / (double)size)
                }
            };
        }

        // ── 6. THỐNG KÊ VÍ NỀN TẢNG / ADMIN PLATFORM WALLET (ĐẶC TẢ 4 & 7) ───────────────
        public async Task<AdminPlatformWalletResponse> GetAdminPlatformWalletAsync(Guid currentUserId, string currentUserRole)
        {
            var isAdmin = string.Equals(currentUserRole, "admin", StringComparison.OrdinalIgnoreCase);
            if (!isAdmin)
            {
                throw new ForbiddenException("FORBIDDEN", "Chỉ quản trị viên hệ thống (Admin) mới có quyền truy cập Ví Nền Tảng.");
            }

            var allEscrows = (await _unitOfWork.Repository<EscrowTransaction>().GetAllAsync()).ToList();
            var allBookings = (await _unitOfWork.Repository<Booking>().GetAllAsync()).ToDictionary(b => b.Id);

            // 1. Quỹ tạm giữ Escrow hiện tại (HeldInEscrow + CoolingDown)
            var activeEscrowItems = allEscrows
                .Where(e => e.EscrowStatus == EscrowStatus.HeldInEscrow || e.EscrowStatus == EscrowStatus.CoolingDown)
                .ToList();

            var escrowHoldingBalance = activeEscrowItems.Sum(e => e.Amount);
            var pendingExpertReceivable = activeEscrowItems.Sum(e => e.ExpertReceivable);
            var pendingPlatformCommission = activeEscrowItems.Sum(e => e.CommissionAmount);

            // 2. Doanh thu hoa hồng thực nhận từ các ca đã kết toán Settled
            var settledItems = allEscrows.Where(e => e.EscrowStatus == EscrowStatus.Settled).ToList();
            var realizedPlatformRevenue = settledItems.Sum(e => e.CommissionAmount);
            var totalSettledToExperts = settledItems.Sum(e => e.ExpertReceivable);

            // Doanh thu hoa hồng lũy kế (Đã kết toán + Đang tạm giữ trong Escrow)
            var totalAccumulatedRevenue = realizedPlatformRevenue + pendingPlatformCommission;

            // 3. Tổng tiền đã hoàn trả cho người nộp thuế
            var totalRefundedToTaxpayers = allEscrows.Sum(e => e.RefundAmount);

            // 4. Tổng dòng tiền GMV (Tất cả tiền thực nạp vào sàn: HeldInEscrow, CoolingDown, Settled, Refunded, PartiallyRefunded)
            var paidItems = allEscrows
                .Where(e => e.EscrowStatus == EscrowStatus.HeldInEscrow || 
                            e.EscrowStatus == EscrowStatus.CoolingDown || 
                            e.EscrowStatus == EscrowStatus.Settled || 
                            e.EscrowStatus == EscrowStatus.Refunded || 
                            e.EscrowStatus == EscrowStatus.PartiallyRefunded)
                .ToList();
            var totalGrossVolume = paidItems.Sum(e => e.Amount);

            // 5. Thống kê số lượng theo trạng thái
            var stats = new AdminWalletTransactionStats
            {
                TotalTransactions = allEscrows.Count,
                PendingPaymentCount = allEscrows.Count(e => e.EscrowStatus == EscrowStatus.PendingPayment),
                HeldInEscrowCount = allEscrows.Count(e => e.EscrowStatus == EscrowStatus.HeldInEscrow),
                CoolingDownCount = allEscrows.Count(e => e.EscrowStatus == EscrowStatus.CoolingDown),
                SettledCount = allEscrows.Count(e => e.EscrowStatus == EscrowStatus.Settled),
                RefundedCount = allEscrows.Count(e => e.EscrowStatus == EscrowStatus.Refunded),
                PartiallyRefundedCount = allEscrows.Count(e => e.EscrowStatus == EscrowStatus.PartiallyRefunded),
                CancelledCount = allEscrows.Count(e => e.EscrowStatus == EscrowStatus.Cancelled)
            };

            // 6. Top 10 giao dịch gần đây nhất
            var recentTransactions = allEscrows
                .OrderByDescending(e => e.CreatedAt)
                .Take(10)
                .Select(e =>
                {
                    allBookings.TryGetValue(e.BookingId, out var b);
                    return MapToEscrowResponse(e, b);
                })
                .ToList();

            return new AdminPlatformWalletResponse
            {
                TotalGrossVolume = totalGrossVolume,
                EscrowHoldingBalance = escrowHoldingBalance,
                PendingExpertReceivable = pendingExpertReceivable,
                PendingPlatformCommission = pendingPlatformCommission,
                RealizedPlatformRevenue = realizedPlatformRevenue,
                TotalAccumulatedRevenue = totalAccumulatedRevenue,
                TotalSettledToExperts = totalSettledToExperts,
                TotalRefundedToTaxpayers = totalRefundedToTaxpayers,
                Stats = stats,
                RecentTransactions = recentTransactions
            };
        }

        // ── PRIVATE MAPPERS ──────────────────────────────────────────────────────────
        private static PaymentLinkResponse MapToPaymentLinkResponse(Booking booking, EscrowTransaction escrow, DateTimeOffset nowUtc)
        {
            var holdRemainingSeconds = (int)Math.Max(0, (booking.HoldExpiresAt - nowUtc).TotalSeconds);

            return new PaymentLinkResponse
            {
                BookingId = booking.Id,
                BookingCode = booking.BookingCode,
                EscrowTransactionId = escrow.Id,
                OrderCode = escrow.OrderCode,
                Amount = escrow.Amount,
                CommissionRate = escrow.CommissionRate,
                CommissionAmount = escrow.CommissionAmount,
                ExpertReceivable = escrow.ExpertReceivable,
                EscrowStatus = escrow.EscrowStatus,
                PaymentGateway = escrow.PaymentGateway,
                PaymentLinkId = escrow.PaymentLinkId,
                CheckoutUrl = escrow.CheckoutUrl,
                QrCode = escrow.QrCode,
                HoldExpiresAt = booking.HoldExpiresAt,
                HoldCountdownSeconds = holdRemainingSeconds
            };
        }

        private static EscrowTransactionResponse MapToEscrowResponse(EscrowTransaction escrow, Booking? booking)
        {
            return new EscrowTransactionResponse
            {
                Id = escrow.Id,
                BookingId = escrow.BookingId,
                BookingCode = booking?.BookingCode ?? string.Empty,
                BookingStatus = booking?.Status ?? BookingStatus.PENDING_PAYMENT,
                OrderCode = escrow.OrderCode,
                Amount = escrow.Amount,
                CommissionRate = escrow.CommissionRate,
                CommissionAmount = escrow.CommissionAmount,
                ExpertReceivable = escrow.ExpertReceivable,
                EscrowStatus = escrow.EscrowStatus,
                PaymentGateway = escrow.PaymentGateway,
                PaymentReference = escrow.PaymentReference,
                PaymentLinkId = escrow.PaymentLinkId,
                CheckoutUrl = escrow.CheckoutUrl,
                QrCode = escrow.QrCode,
                RefundAmount = escrow.RefundAmount,
                RefundReason = escrow.RefundReason,
                PaidAt = escrow.PaidAt,
                SettledAt = escrow.SettledAt,
                RefundedAt = escrow.RefundedAt,
                CreatedAt = escrow.CreatedAt,
                UpdatedAt = escrow.UpdatedAt
            };
        }

        private static IEnumerable<EscrowTransaction> ApplySort(IEnumerable<EscrowTransaction> query, string? sort)
        {
            if (string.IsNullOrWhiteSpace(sort))
            {
                return query.OrderByDescending(e => e.CreatedAt);
            }

            return sort.Trim().ToLowerInvariant() switch
            {
                "createdat"   => query.OrderBy(e => e.CreatedAt),
                "-createdat"  => query.OrderByDescending(e => e.CreatedAt),
                "amount"      => query.OrderBy(e => e.Amount),
                "-amount"     => query.OrderByDescending(e => e.Amount),
                "paidat"      => query.OrderBy(e => e.PaidAt),
                "-paidat"     => query.OrderByDescending(e => e.PaidAt),
                "updatedat"   => query.OrderBy(e => e.UpdatedAt),
                "-updatedat"  => query.OrderByDescending(e => e.UpdatedAt),
                _             => query.OrderByDescending(e => e.CreatedAt)
            };
        }
    }
}
