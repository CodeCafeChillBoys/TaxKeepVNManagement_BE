using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TaxKeepVN.Application.DTOs.Subscriptions;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Helpers;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    public class UserSubscriptionService : IUserSubscriptionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _configuration;
        private readonly ISubscriptionPlanService _planService;
        private readonly ILogger<UserSubscriptionService> _logger;

        public UserSubscriptionService(
            IUnitOfWork unitOfWork,
            IConfiguration configuration,
            ISubscriptionPlanService planService,
            ILogger<UserSubscriptionService> logger)
        {
            _unitOfWork = unitOfWork;
            _configuration = configuration;
            _planService = planService;
            _logger = logger;
        }

        public async Task<UserSubscriptionResponse?> GetActiveSubscriptionAsync(Guid userId)
        {
            var sub = await GetOrCreateActiveSubscriptionEntityAsync(userId);
            return MapToResponse(sub);
        }

        public async Task<UserSubscription> GetOrCreateActiveSubscriptionEntityAsync(Guid userId)
        {
            var now = DateTimeOffset.UtcNow;
            var subRepo = _unitOfWork.Repository<UserSubscription>();

            var subs = (await subRepo.FindAsync(s => s.UserId == userId && s.Status == "active")).ToList();
            var activeSub = subs.FirstOrDefault(s => !s.ExpiresAt.HasValue || s.ExpiresAt.Value > now);

            if (activeSub != null)
            {
                // Ensure Plan is loaded
                if (activeSub.Plan == null)
                {
                    activeSub.Plan = await _unitOfWork.Repository<SubscriptionPlan>().GetByIdAsync(activeSub.PlanId);
                }
                return activeSub;
            }

            // Có subscription nhưng đã hết hạn -> đánh dấu expired
            var expiredSubs = subs.Where(s => s.ExpiresAt.HasValue && s.ExpiresAt.Value <= now).ToList();
            foreach (var exp in expiredSubs)
            {
                exp.Status = "expired";
                exp.UpdatedAt = now;
                subRepo.Update(exp);
            }
            if (expiredSubs.Any())
            {
                await _unitOfWork.SaveChangesAsync();
            }

            // Tự động gán gói Basic mặc định nếu chưa có gói hợp lệ
            return await AutoAssignBasicPlanIfNoneAsync(userId);
        }

        public async Task<UserSubscription> AutoAssignBasicPlanIfNoneAsync(Guid userId)
        {
            var planRepo = _unitOfWork.Repository<SubscriptionPlan>();
            var subRepo = _unitOfWork.Repository<UserSubscription>();

            var basicPlans = await planRepo.FindAsync(p => p.Slug.ToLower() == "basic");
            var basicPlan = basicPlans.FirstOrDefault();

            if (basicPlan == null)
            {
                await _planService.SeedDefaultPlansIfEmptyAsync();
                basicPlans = await planRepo.FindAsync(p => p.Slug.ToLower() == "basic");
                basicPlan = basicPlans.First();
            }

            var now = DateTimeOffset.UtcNow;
            var basicSub = new UserSubscription
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PlanId = basicPlan.Id,
                BillingCycle = "free",
                Status = "active",
                AmountPaid = 0m,
                PaymentMethod = "FREE",
                PaymentStatus = "free",
                StartsAt = now,
                ExpiresAt = null, // Vĩnh viễn cho gói Basic
                SnapshotMaxAiQuestionsPerMonth = basicPlan.MaxAiQuestionsPerMonth,
                SnapshotChatHistoryRetentionDays = basicPlan.ChatHistoryRetentionDays,
                SnapshotMaxOcrScansPerMonth = basicPlan.MaxOcrScansPerMonth,
                SnapshotMaxIncomeSources = basicPlan.MaxIncomeSources,
                SnapshotMaxDependents = basicPlan.MaxDependents,
                SnapshotCanExportPdf = basicPlan.CanExportPdf,
                SnapshotCanExportZip = basicPlan.CanExportZip,
                SnapshotBookingDiscountPercent = basicPlan.BookingDiscountPercent,
                SnapshotFreeConsultationsPerYear = basicPlan.FreeConsultationsPerYear,
                CreatedAt = now,
                UpdatedAt = now,
                Plan = basicPlan
            };

            await subRepo.AddAsync(basicSub);
            await _unitOfWork.SaveChangesAsync();

            return basicSub;
        }

        public async Task<SubscriptionPurchaseResponse> PurchaseAsync(Guid userId, PurchaseSubscriptionRequest request)
        {
            var plan = await _unitOfWork.Repository<SubscriptionPlan>().GetByIdAsync(request.PlanId);
            if (plan == null || !plan.IsActive)
            {
                throw new NotFoundException("Gói dịch vụ không tồn tại hoặc đã ngừng cung cấp.");
            }

            var cycle = request.BillingCycle.ToLower().Trim();
            if (cycle != "monthly" && cycle != "yearly")
            {
                throw new BadRequestException("INVALID_BILLING_CYCLE", "Chu kỳ thanh toán chỉ chấp nhận 'monthly' hoặc 'yearly'.");
            }

            decimal price = cycle == "yearly" ? plan.PriceYearly : plan.PriceMonthly;
            var now = DateTimeOffset.UtcNow;
            var orderCode = BookingHelper.GeneratePayOsOrderCode();

            // Nếu giá = 0 (Gói miễn phí)
            if (price <= 0)
            {
                await DeactivateCurrentActiveSubscriptionsAsync(userId);
                var freeSub = CreateSubscriptionRecord(userId, plan, cycle, 0m, "FREE", "free", orderCode.ToString(), now, null);
                await _unitOfWork.Repository<UserSubscription>().AddAsync(freeSub);
                await _unitOfWork.SaveChangesAsync();

                return new SubscriptionPurchaseResponse
                {
                    SubscriptionId = freeSub.Id,
                    OrderCode = orderCode,
                    Amount = 0m,
                    PaymentMethod = "FREE",
                    IsCompleted = true,
                    Message = $"Kích hoạt {plan.Name} thành công."
                };
            }

            var method = request.PaymentMethod.ToUpper().Trim();

            // ══════════════════════════════════════════════════════════════════════════
            // LỰA CHỌN 1: THANH TOÁN BẰNG SỐ DƯ VÍ USER (TÁI SỬ DỤNG HOÀN TOÀN TỪ WALLET)
            // ══════════════════════════════════════════════════════════════════════════
            if (method == "WALLET")
            {
                var wallets = await _unitOfWork.Repository<UserWallet>().FindAsync(w => w.UserId == userId && w.WalletType == "USER");
                var wallet = wallets.FirstOrDefault();

                if (wallet == null || wallet.AvailableBalance < price)
                {
                    var balance = wallet?.AvailableBalance ?? 0m;
                    throw new BadRequestException("INSUFFICIENT_WALLET_BALANCE",
                        $"Số dư ví ({balance:N0} VNĐ) không đủ để thanh toán gói {plan.Name} ({price:N0} VNĐ). Vui lòng nạp thêm tiền hoặc chọn cổng VNPay/VietQR.");
                }

                await _unitOfWork.BeginTransactionAsync();
                try
                {
                    // 1. Trừ tiền ví
                    var before = wallet.AvailableBalance;
                    wallet.AvailableBalance -= price;
                    wallet.UpdatedAt = now;
                    _unitOfWork.Repository<UserWallet>().Update(wallet);

                    // 2. Ghi biến động số dư (WalletTransaction)
                    var txnRef = $"WALLET_SUB_{orderCode}";
                    var subId = Guid.NewGuid();
                    await _unitOfWork.Repository<WalletTransaction>().AddAsync(new WalletTransaction
                    {
                        Id = Guid.NewGuid(),
                        WalletId = wallet.Id,
                        UserId = userId,
                        Amount = -price,
                        BalanceBefore = before,
                        BalanceAfter = wallet.AvailableBalance,
                        TransactionType = "PAYMENT_SUBSCRIPTION",
                        ReferenceId = subId,
                        Description = $"Thanh toán gói {plan.Name} (chu kỳ {cycle}) bằng số dư ví",
                        CreatedAt = now
                    });

                    // 3. Huỷ/Hết hạn các gói cũ
                    await DeactivateCurrentActiveSubscriptionsAsync(userId);

                    // 4. Kích hoạt gói mới
                    var expiresAt = cycle == "yearly" ? now.AddYears(1) : now.AddMonths(1);
                    var newSub = CreateSubscriptionRecord(userId, plan, cycle, price, "WALLET", "paid", txnRef, now, expiresAt, subId);
                    await _unitOfWork.Repository<UserSubscription>().AddAsync(newSub);

                    await _unitOfWork.SaveChangesAsync();
                    await _unitOfWork.CommitTransactionAsync();

                    _logger.LogInformation("Người dùng {UserId} thanh toán gói {PlanName} thành công qua Ví. Số dư ví: {Before:N0} -> {After:N0}",
                        userId, plan.Name, before, wallet.AvailableBalance);

                    return new SubscriptionPurchaseResponse
                    {
                        SubscriptionId = newSub.Id,
                        OrderCode = orderCode,
                        Amount = price,
                        PaymentMethod = "WALLET",
                        IsCompleted = true,
                        Message = $"Thanh toán gói {plan.Name} bằng số dư ví thành công!"
                    };
                }
                catch (Exception ex)
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    _logger.LogError(ex, "Lỗi khi thanh toán gói qua Ví cho User {UserId}", userId);
                    throw;
                }
            }

            // ══════════════════════════════════════════════════════════════════════════
            // LỰA CHỌN 2: THANH TOÁN QUA VNPAY HOẶC VIETQR (TẠO LINK THANH TOÁN)
            // ══════════════════════════════════════════════════════════════════════════
            var vnpTmnCode = _configuration["VnPay:TmnCode"] ?? "CGXZLS0Z";
            var vnpHashSecret = _configuration["VnPay:HashSecret"] ?? "XNBCJFAKAZQSGTARRLGCHVZWCIOIGSHN";
            var vnpBaseUrl = _configuration["VnPay:BaseUrl"] ?? _configuration["VnPay:PaymentUrl"] ?? "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
            var vnpReturnUrl = request.ReturnUrl ?? _configuration["VnPay:SubscriptionReturnUrl"] ?? _configuration["VnPay:ReturnUrl"] ?? "https://localhost:7027/api/v1/subscription/payment-return";

            var qrBankBin = _configuration["VietQr:BankBin"] ?? "970405";
            var qrAccountNo = _configuration["VietQr:AccountNumber"] ?? "5402205457446";
            var qrAccountName = _configuration["VietQr:AccountName"] ?? "TAXKEEPVN";

            var checkoutUrl = VnPayHelper.CreatePaymentUrl(
                vnpBaseUrl,
                vnpTmnCode,
                vnpHashSecret,
                vnpReturnUrl,
                orderCode,
                price,
                $"SUB{orderCode}",
                now.AddMinutes(15),
                "127.0.0.1",
                request.ReturnUrl
            );

            var qrCode = VietQrHelper.GenerateVietQrUrl(
                qrBankBin,
                qrAccountNo,
                qrAccountName,
                price,
                $"SUB{orderCode}"
            );

            // Tạo pending subscription record
            var pendingSub = CreateSubscriptionRecord(userId, plan, cycle, price, method, "pending", orderCode.ToString(), now, null);
            pendingSub.Status = "pending";
            await _unitOfWork.Repository<UserSubscription>().AddAsync(pendingSub);
            await _unitOfWork.SaveChangesAsync();

            return new SubscriptionPurchaseResponse
            {
                SubscriptionId = pendingSub.Id,
                OrderCode = orderCode,
                Amount = price,
                PaymentMethod = method,
                IsCompleted = false,
                CheckoutUrl = method == "VNPAY" ? checkoutUrl : null,
                QrCode = method == "VIETQR" ? qrCode : null,
                Message = "Vui lòng hoàn tất thanh toán qua cổng."
            };
        }

        public async Task<bool> HandlePaymentCallbackAsync(long orderCode, bool isSuccess)
        {
            var subRepo = _unitOfWork.Repository<UserSubscription>();
            var refStr = orderCode.ToString();
            var subs = (await subRepo.FindAsync(s => s.PaymentReference == refStr && s.Status == "pending")).ToList();
            var pendingSub = subs.FirstOrDefault();

            if (pendingSub == null) return false;

            var now = DateTimeOffset.UtcNow;
            if (isSuccess)
            {
                await DeactivateCurrentActiveSubscriptionsAsync(pendingSub.UserId);

                pendingSub.Status = "active";
                pendingSub.PaymentStatus = "paid";
                pendingSub.StartsAt = now;
                pendingSub.ExpiresAt = pendingSub.BillingCycle == "yearly" ? now.AddYears(1) : now.AddMonths(1);
                pendingSub.UpdatedAt = now;
                subRepo.Update(pendingSub);
            }
            else
            {
                pendingSub.Status = "cancelled";
                pendingSub.PaymentStatus = "failed";
                pendingSub.UpdatedAt = now;
                subRepo.Update(pendingSub);
            }

            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<List<UserSubscriptionResponse>> GetSubscriptionHistoryAsync(Guid userId)
        {
            var subs = await _unitOfWork.Repository<UserSubscription>().FindAsync(s => s.UserId == userId);
            var plans = (await _unitOfWork.Repository<SubscriptionPlan>().GetAllAsync()).ToDictionary(p => p.Id);

            var list = new List<UserSubscriptionResponse>();
            foreach (var s in subs.OrderByDescending(s => s.CreatedAt))
            {
                if (plans.TryGetValue(s.PlanId, out var plan))
                {
                    s.Plan = plan;
                }
                list.Add(MapToResponse(s));
            }
            return list;
        }

        public async Task<bool> CancelSubscriptionAsync(Guid userId)
        {
            var active = await GetOrCreateActiveSubscriptionEntityAsync(userId);
            if (active.BillingCycle == "free")
            {
                throw new BadRequestException("CANNOT_CANCEL_FREE_PLAN", "Bạn đang sử dụng gói Basic miễn phí.");
            }

            active.Status = "cancelled";
            active.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<UserSubscription>().Update(active);

            // Gán lại gói Basic ngay lập tức
            await AutoAssignBasicPlanIfNoneAsync(userId);
            await _unitOfWork.SaveChangesAsync();

            return true;
        }

        private async Task DeactivateCurrentActiveSubscriptionsAsync(Guid userId)
        {
            var now = DateTimeOffset.UtcNow;
            var subRepo = _unitOfWork.Repository<UserSubscription>();
            var currentActive = await subRepo.FindAsync(s => s.UserId == userId && s.Status == "active");

            foreach (var s in currentActive)
            {
                s.Status = "cancelled";
                s.UpdatedAt = now;
                subRepo.Update(s);
            }
        }

        private static UserSubscription CreateSubscriptionRecord(
            Guid userId,
            SubscriptionPlan plan,
            string cycle,
            decimal amount,
            string method,
            string paymentStatus,
            string reference,
            DateTimeOffset now,
            DateTimeOffset? expiresAt,
            Guid? customId = null)
        {
            return new UserSubscription
            {
                Id = customId ?? Guid.NewGuid(),
                UserId = userId,
                PlanId = plan.Id,
                BillingCycle = cycle,
                Status = "active",
                AmountPaid = amount,
                PaymentMethod = method,
                PaymentStatus = paymentStatus,
                PaymentReference = reference,
                StartsAt = now,
                ExpiresAt = expiresAt,
                SnapshotMaxAiQuestionsPerMonth = plan.MaxAiQuestionsPerMonth,
                SnapshotChatHistoryRetentionDays = plan.ChatHistoryRetentionDays,
                SnapshotMaxOcrScansPerMonth = plan.MaxOcrScansPerMonth,
                SnapshotMaxIncomeSources = plan.MaxIncomeSources,
                SnapshotMaxDependents = plan.MaxDependents,
                SnapshotCanExportPdf = plan.CanExportPdf,
                SnapshotCanExportZip = plan.CanExportZip,
                SnapshotBookingDiscountPercent = plan.BookingDiscountPercent,
                SnapshotFreeConsultationsPerYear = plan.FreeConsultationsPerYear,
                CreatedAt = now,
                UpdatedAt = now,
                Plan = plan
            };
        }

        private static UserSubscriptionResponse MapToResponse(UserSubscription sub)
        {
            int? remainingDays = null;
            if (sub.ExpiresAt.HasValue)
            {
                var diff = (sub.ExpiresAt.Value - DateTimeOffset.UtcNow).TotalDays;
                remainingDays = Math.Max(0, (int)Math.Ceiling(diff));
            }

            return new UserSubscriptionResponse
            {
                Id = sub.Id,
                PlanId = sub.PlanId,
                PlanName = sub.Plan?.Name ?? "Gói Dịch Vụ",
                PlanSlug = sub.Plan?.Slug ?? "unknown",
                BillingCycle = sub.BillingCycle,
                Status = sub.Status,
                AmountPaid = sub.AmountPaid,
                PaymentMethod = sub.PaymentMethod,
                StartsAt = sub.StartsAt,
                ExpiresAt = sub.ExpiresAt,
                RemainingDays = remainingDays,
                MaxAiQuestionsPerMonth = sub.SnapshotMaxAiQuestionsPerMonth,
                ChatHistoryRetentionDays = sub.SnapshotChatHistoryRetentionDays,
                MaxOcrScansPerMonth = sub.SnapshotMaxOcrScansPerMonth,
                MaxIncomeSources = sub.SnapshotMaxIncomeSources,
                MaxDependents = sub.SnapshotMaxDependents,
                CanExportPdf = sub.SnapshotCanExportPdf,
                CanExportZip = sub.SnapshotCanExportZip,
                BookingDiscountPercent = sub.SnapshotBookingDiscountPercent,
                FreeConsultationsPerYear = sub.SnapshotFreeConsultationsPerYear
            };
        }
    }
}

