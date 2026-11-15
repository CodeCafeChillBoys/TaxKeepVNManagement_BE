using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Subscriptions;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Helpers;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    public class AdminSubscriptionService : IAdminSubscriptionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IUserSubscriptionService _userSubscriptionService;
        private readonly ILogger<AdminSubscriptionService> _logger;

        public AdminSubscriptionService(
            IUnitOfWork unitOfWork,
            IUserSubscriptionService userSubscriptionService,
            ILogger<AdminSubscriptionService> logger)
        {
            _unitOfWork = unitOfWork;
            _userSubscriptionService = userSubscriptionService;
            _logger = logger;
        }

        public async Task<PagedResult<AdminUserSubscriptionResponse>> GetUserSubscriptionsAsync(AdminSubscriptionFilterRequest filter)
        {
            int page = filter.Page < 1 ? 1 : filter.Page;
            int pageSize = filter.PageSize < 1 ? 10 : (filter.PageSize > 100 ? 100 : filter.PageSize);

            var now = DateTimeOffset.UtcNow;
            var allSubs = (await _unitOfWork.Repository<UserSubscription>().GetAllAsync()).ToList();
            var plans = (await _unitOfWork.Repository<SubscriptionPlan>().GetAllAsync()).ToDictionary(p => p.Id);

            var userIds = allSubs.Select(s => s.UserId).Distinct().ToList();
            var users = (await _unitOfWork.Repository<User>().FindAsync(u => userIds.Contains(u.UserId)))
                .ToDictionary(u => u.UserId);

            // Gán navigation objects
            foreach (var sub in allSubs)
            {
                if (plans.TryGetValue(sub.PlanId, out var plan)) sub.Plan = plan;
                if (users.TryGetValue(sub.UserId, out var user)) sub.User = user;
            }

            var query = allSubs.AsEnumerable();

            // 1. Tìm kiếm (Họ tên, Email, Số điện thoại)
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var s = filter.Search.Trim().ToLower();
                query = query.Where(sub =>
                    (sub.User?.FullName?.ToLower().Contains(s) ?? false) ||
                    (sub.User?.Email?.ToLower().Contains(s) ?? false) ||
                    (sub.User?.PhoneNumber?.ToLower().Contains(s) ?? false));
            }

            // 2. Lọc theo PlanId
            if (filter.PlanId.HasValue && filter.PlanId.Value != Guid.Empty)
            {
                query = query.Where(sub => sub.PlanId == filter.PlanId.Value);
            }

            // 3. Lọc theo PlanSlug
            if (!string.IsNullOrWhiteSpace(filter.PlanSlug))
            {
                var slug = filter.PlanSlug.Trim().ToLower();
                query = query.Where(sub => sub.Plan?.Slug?.ToLower() == slug);
            }

            // 4. Lọc theo Status
            if (!string.IsNullOrWhiteSpace(filter.Status))
            {
                var st = filter.Status.Trim().ToLower();
                query = query.Where(sub => sub.Status?.ToLower() == st);
            }

            // 5. Lọc theo BillingCycle
            if (!string.IsNullOrWhiteSpace(filter.BillingCycle))
            {
                var cycle = filter.BillingCycle.Trim().ToLower();
                query = query.Where(sub => sub.BillingCycle?.ToLower() == cycle);
            }

            // 6. Lọc những người sắp hết hạn trong X ngày tới
            if (filter.ExpiringInDays.HasValue && filter.ExpiringInDays.Value > 0)
            {
                var maxExpiry = now.AddDays(filter.ExpiringInDays.Value);
                query = query.Where(sub => sub.Status == "active"
                                        && sub.ExpiresAt.HasValue
                                        && sub.ExpiresAt.Value >= now
                                        && sub.ExpiresAt.Value <= maxExpiry);
            }

            var list = query.OrderByDescending(s => s.CreatedAt).ToList();
            var totalItems = list.Count;
            var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            var pagedItems = list.Skip((page - 1) * pageSize).Take(pageSize).Select(MapToAdminResponse).ToList();

            return new PagedResult<AdminUserSubscriptionResponse>
            {
                Items = pagedItems,
                Pagination = new PaginationMeta
                {
                    Page = page,
                    PageSize = pageSize,
                    TotalItems = totalItems,
                    TotalPages = totalPages
                }
            };
        }

        public async Task<AdminUserSubscriptionResponse> GetSubscriptionByIdAsync(Guid subscriptionId)
        {
            var sub = await _unitOfWork.Repository<UserSubscription>().GetByIdAsync(subscriptionId);
            if (sub == null)
            {
                throw new NotFoundException("Không tìm thấy thông tin gói đăng ký của người dùng.");
            }

            sub.Plan = await _unitOfWork.Repository<SubscriptionPlan>().GetByIdAsync(sub.PlanId);
            sub.User = await _unitOfWork.Repository<User>().GetByIdAsync(sub.UserId);

            var res = MapToAdminResponse(sub);

            // Lấy lượng quota đã dùng trong tháng hiện tại
            var monthStr = DateTime.UtcNow.ToString("yyyy-MM");
            var usages = await _unitOfWork.Repository<SubscriptionQuotaUsage>()
                .FindAsync(q => q.UserId == sub.UserId && q.PeriodMonth == monthStr);
            var currentUsage = usages.FirstOrDefault();

            res.CurrentMonthAiQuestionsUsed = currentUsage?.AiQuestionsUsed ?? 0;
            res.CurrentMonthOcrScansUsed = currentUsage?.OcrScansUsed ?? 0;

            return res;
        }

        public async Task<SubscriptionAnalyticsResponse> GetAnalyticsAsync(DateTimeOffset? fromDate, DateTimeOffset? toDate)
        {
            var allSubs = (await _unitOfWork.Repository<UserSubscription>().GetAllAsync()).ToList();
            var plans = (await _unitOfWork.Repository<SubscriptionPlan>().GetAllAsync()).ToDictionary(p => p.Id);

            foreach (var s in allSubs)
            {
                if (plans.TryGetValue(s.PlanId, out var plan)) s.Plan = plan;
            }

            var query = allSubs.AsEnumerable();
            if (fromDate.HasValue) query = query.Where(s => s.CreatedAt >= fromDate.Value);
            if (toDate.HasValue) query = query.Where(s => s.CreatedAt <= toDate.Value);

            var filteredSubs = query.ToList();

            // Chỉ tính doanh thu các gói đã thanh toán thành công và chưa bị hoàn tiền
            var paidSubs = filteredSubs.Where(s => s.PaymentStatus == "paid" && s.AmountPaid > 0).ToList();

            var totalRevenue = paidSubs.Sum(s => s.AmountPaid);
            var totalActiveSubscriptions = filteredSubs.Count(s => s.Status == "active");
            var totalPaidTransactions = paidSubs.Count;

            // 1. Doanh thu theo gói
            var revenueByPlan = paidSubs
                .GroupBy(s => s.PlanId)
                .Select(g =>
                {
                    var plan = plans.TryGetValue(g.Key, out var p) ? p : null;
                    return new PlanRevenueSummaryDto
                    {
                        PlanId = g.Key,
                        PlanName = plan?.Name ?? "Gói dịch vụ",
                        PlanSlug = plan?.Slug ?? "unknown",
                        TotalRevenue = g.Sum(x => x.AmountPaid),
                        PurchaseCount = g.Count()
                    };
                })
                .OrderByDescending(r => r.TotalRevenue)
                .ToList();

            // 2. Doanh thu theo chu kỳ
            var revenueByCycle = paidSubs
                .GroupBy(s => s.BillingCycle)
                .Select(g => new CycleRevenueSummaryDto
                {
                    BillingCycle = g.Key,
                    TotalRevenue = g.Sum(x => x.AmountPaid),
                    PurchaseCount = g.Count()
                })
                .OrderByDescending(r => r.TotalRevenue)
                .ToList();

            // 3. Phân bổ người dùng đang hoạt động (Active User Distribution)
            var activeSubs = filteredSubs.Where(s => s.Status == "active").ToList();
            var totalActive = activeSubs.Count;
            var userDistribution = activeSubs
                .GroupBy(s => s.PlanId)
                .Select(g =>
                {
                    var plan = plans.TryGetValue(g.Key, out var p) ? p : null;
                    var count = g.Count();
                    return new UserPlanDistributionDto
                    {
                        PlanId = g.Key,
                        PlanName = plan?.Name ?? "Gói dịch vụ",
                        PlanSlug = plan?.Slug ?? "unknown",
                        ActiveUserCount = count,
                        Percentage = totalActive > 0 ? Math.Round((double)count * 100 / totalActive, 2) : 0
                    };
                })
                .OrderByDescending(d => d.ActiveUserCount)
                .ToList();

            // 4. Xu hướng doanh thu theo tháng (Monthly Trends)
            var monthlyTrends = paidSubs
                .GroupBy(s => s.CreatedAt.ToString("yyyy-MM"))
                .Select(g => new MonthlyRevenueTrendDto
                {
                    YearMonth = g.Key,
                    Revenue = g.Sum(x => x.AmountPaid),
                    PurchaseCount = g.Count()
                })
                .OrderBy(m => m.YearMonth)
                .ToList();

            return new SubscriptionAnalyticsResponse
            {
                TotalRevenue = totalRevenue,
                TotalActiveSubscriptions = totalActiveSubscriptions,
                TotalPaidTransactions = totalPaidTransactions,
                RevenueByPlan = revenueByPlan,
                RevenueByCycle = revenueByCycle,
                UserDistribution = userDistribution,
                MonthlyTrends = monthlyTrends
            };
        }

        public async Task<AdminUserSubscriptionResponse> GiftSubscriptionAsync(GiftSubscriptionRequest request, Guid adminId)
        {
            var user = await _unitOfWork.Repository<User>().GetByIdAsync(request.UserId);
            if (user == null)
            {
                throw new NotFoundException("Người dùng nhận gói không tồn tại trong hệ thống.");
            }

            var plan = await _unitOfWork.Repository<SubscriptionPlan>().GetByIdAsync(request.PlanId);
            if (plan == null || !plan.IsActive)
            {
                throw new NotFoundException("Gói dịch vụ không tồn tại hoặc đã ngừng cung cấp.");
            }

            var now = DateTimeOffset.UtcNow;
            var expiresAt = now.AddDays(request.DurationDays);

            // Hủy gói active hiện tại của user
            var currentActive = await _unitOfWork.Repository<UserSubscription>()
                .FindAsync(s => s.UserId == request.UserId && s.Status == "active");
            foreach (var cur in currentActive)
            {
                cur.Status = "cancelled";
                cur.UpdatedAt = now;
                _unitOfWork.Repository<UserSubscription>().Update(cur);
            }

            // Kích hoạt gói mới dưới dạng Quà tặng Admin
            var newSub = new UserSubscription
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                PlanId = plan.Id,
                BillingCycle = request.DurationDays >= 365 ? "yearly" : "monthly",
                Status = "active",
                AmountPaid = 0m,
                PaymentMethod = "ADMIN_GRANT",
                PaymentStatus = "paid",
                PaymentReference = $"GIFT_{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
                StartsAt = now,
                ExpiresAt = expiresAt,
                SnapshotMaxAiQuestionsPerMonth = plan.MaxAiQuestionsPerMonth,
                SnapshotChatHistoryRetentionDays = plan.ChatHistoryRetentionDays,
                SnapshotMaxOcrScansPerMonth = plan.MaxOcrScansPerMonth,
                SnapshotCanExportPdf = plan.CanExportPdf,
                SnapshotCanExportZip = plan.CanExportZip,
                SnapshotBookingDiscountPercent = plan.BookingDiscountPercent,
                SnapshotFreeConsultationsPerYear = plan.FreeConsultationsPerYear,
                CreatedAt = now,
                UpdatedAt = now,
                Plan = plan,
                User = user
            };

            await _unitOfWork.Repository<UserSubscription>().AddAsync(newSub);

            // Gửi thông báo In-App cho người dùng
            await _unitOfWork.Repository<SystemNotification>().AddAsync(new SystemNotification
            {
                NotificationId = Guid.NewGuid(),
                UserId = request.UserId,
                Title = "Bạn nhận được quà tặng gói dịch vụ",
                Message = $"Ban quản trị đã kích hoạt gói '{plan.Name}' cho bạn (thời hạn {request.DurationDays} ngày, hết hạn vào {expiresAt:dd/MM/yyyy}). Lý do: {request.Reason}",
                NotificationType = "SUBSCRIPTION_GIFT",
                IsRead = false,
                TargetActionUrl = "/subscription",
                CreatedAt = now.UtcDateTime
            });

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Admin {AdminId} đã tặng gói {PlanName} ({Days} ngày) cho User {UserId}. Lý do: {Reason}",
                adminId, plan.Name, request.DurationDays, request.UserId, request.Reason);

            return MapToAdminResponse(newSub);
        }

        public async Task<AdminUserSubscriptionResponse> ExtendSubscriptionAsync(Guid subscriptionId, ExtendSubscriptionRequest request, Guid adminId)
        {
            var sub = await _unitOfWork.Repository<UserSubscription>().GetByIdAsync(subscriptionId);
            if (sub == null)
            {
                throw new NotFoundException("Không tìm thấy thông tin gói đăng ký.");
            }

            if (sub.BillingCycle == "free" || !sub.ExpiresAt.HasValue)
            {
                throw new BadRequestException("CANNOT_EXTEND_FREE_PLAN",
                    "Không thể gia hạn gói Basic miễn phí vĩnh viễn. Vui lòng sử dụng tính năng 'Tặng gói' để nâng cấp cho người dùng.");
            }

            if (sub.Status != "active")
            {
                throw new BadRequestException("SUBSCRIPTION_NOT_ACTIVE",
                    "Chỉ có thể gia hạn gói đang ở trạng thái Hoạt động (active).");
            }

            var now = DateTimeOffset.UtcNow;
            var baseDate = sub.ExpiresAt.Value > now ? sub.ExpiresAt.Value : now;
            sub.ExpiresAt = baseDate.AddDays(request.ExtensionDays);
            sub.UpdatedAt = now;

            _unitOfWork.Repository<UserSubscription>().Update(sub);

            sub.Plan = await _unitOfWork.Repository<SubscriptionPlan>().GetByIdAsync(sub.PlanId);
            sub.User = await _unitOfWork.Repository<User>().GetByIdAsync(sub.UserId);

            // Gửi thông báo In-App cho người dùng
            await _unitOfWork.Repository<SystemNotification>().AddAsync(new SystemNotification
            {
                NotificationId = Guid.NewGuid(),
                UserId = sub.UserId,
                Title = "Gói dịch vụ đã được gia hạn",
                Message = $"Ban quản trị đã gia hạn thêm {request.ExtensionDays} ngày cho gói '{sub.Plan?.Name}'. Hạn sử dụng mới đến {sub.ExpiresAt.Value:dd/MM/yyyy}. Lý do: {request.Reason}",
                NotificationType = "SUBSCRIPTION_EXTENDED",
                IsRead = false,
                TargetActionUrl = "/subscription",
                CreatedAt = now.UtcDateTime
            });

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Admin {AdminId} đã gia hạn thêm {Days} ngày cho gói #{SubId} của User {UserId}",
                adminId, request.ExtensionDays, sub.Id, sub.UserId);

            return MapToAdminResponse(sub);
        }

        public async Task<bool> RefundSubscriptionAsync(Guid subscriptionId, RefundSubscriptionRequest request, Guid adminId)
        {
            var sub = await _unitOfWork.Repository<UserSubscription>().GetByIdAsync(subscriptionId);
            if (sub == null)
            {
                throw new NotFoundException("Không tìm thấy thông tin gói đăng ký.");
            }

            if (sub.PaymentStatus == "refunded")
            {
                throw new BadRequestException("ALREADY_REFUNDED", "Gói này đã được hoàn tiền trước đó.");
            }

            if (sub.AmountPaid <= 0)
            {
                throw new BadRequestException("CANNOT_REFUND_FREE_PLAN", "Gói miễn phí không có số tiền để hoàn trả.");
            }

            var refundAmount = request.RefundAmount.HasValue && request.RefundAmount.Value > 0
                ? request.RefundAmount.Value
                : sub.AmountPaid;

            if (refundAmount > sub.AmountPaid)
            {
                throw new BadRequestException("INVALID_REFUND_AMOUNT",
                    $"Số tiền hoàn ({refundAmount:N0} đ) không được vượt quá số tiền người dùng đã thanh toán ({sub.AmountPaid:N0} đ).");
            }

            sub.Plan = await _unitOfWork.Repository<SubscriptionPlan>().GetByIdAsync(sub.PlanId);

            var now = DateTimeOffset.UtcNow;

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // 1. Hoàn tiền vào Ví cá nhân của User
                var wallets = await _unitOfWork.Repository<UserWallet>().FindAsync(w => w.UserId == sub.UserId && w.WalletType == "USER");
                var userWallet = wallets.FirstOrDefault();

                if (userWallet == null)
                {
                    userWallet = new UserWallet
                    {
                        Id = Guid.NewGuid(),
                        UserId = sub.UserId,
                        WalletType = "USER",
                        AvailableBalance = 0m,
                        FrozenBalance = 0m,
                        TotalEarned = 0m,
                        TotalWithdrawn = 0m,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    await _unitOfWork.Repository<UserWallet>().AddAsync(userWallet);
                }

                var before = userWallet.AvailableBalance;
                userWallet.AvailableBalance += refundAmount;
                userWallet.UpdatedAt = now;
                _unitOfWork.Repository<UserWallet>().Update(userWallet);

                // 2. Ghi biến động số dư ví User
                await _unitOfWork.Repository<WalletTransaction>().AddAsync(new WalletTransaction
                {
                    Id = Guid.NewGuid(),
                    WalletId = userWallet.Id,
                    UserId = sub.UserId,
                    Amount = refundAmount,
                    BalanceBefore = before,
                    BalanceAfter = userWallet.AvailableBalance,
                    TransactionType = "SUBSCRIPTION_REFUND",
                    ReferenceId = sub.Id,
                    Description = $"Hoàn tiền gói {sub.Plan?.Name ?? "dịch vụ"}. Lý do: {request.Reason}",
                    CreatedAt = now
                });

                // 3. Trừ doanh thu từ Ví Nền tảng (Platform Wallet) nếu có
                var platWallets = await _unitOfWork.Repository<UserWallet>().FindAsync(w => w.WalletType == "PLATFORM");
                var platformWallet = platWallets.FirstOrDefault();
                if (platformWallet != null && platformWallet.AvailableBalance >= refundAmount)
                {
                    var platBefore = platformWallet.AvailableBalance;
                    platformWallet.AvailableBalance -= refundAmount;
                    platformWallet.UpdatedAt = now;
                    _unitOfWork.Repository<UserWallet>().Update(platformWallet);

                    await _unitOfWork.Repository<WalletTransaction>().AddAsync(new WalletTransaction
                    {
                        Id = Guid.NewGuid(),
                        WalletId = platformWallet.Id,
                        UserId = platformWallet.UserId,
                        Amount = -refundAmount,
                        BalanceBefore = platBefore,
                        BalanceAfter = platformWallet.AvailableBalance,
                        TransactionType = "PLATFORM_SUBSCRIPTION_REFUND",
                        ReferenceId = sub.Id,
                        Description = $"Trừ doanh thu do hoàn tiền gói {sub.Plan?.Name ?? "dịch vụ"} cho User {sub.UserId}",
                        CreatedAt = now
                    });
                }

                // 4. Cập nhật trạng thái subscription
                sub.Status = "cancelled";
                sub.PaymentStatus = "refunded";
                sub.UpdatedAt = now;
                _unitOfWork.Repository<UserSubscription>().Update(sub);

                // 5. Chuyển người dùng về lại gói Basic mặc định
                await _userSubscriptionService.AutoAssignBasicPlanIfNoneAsync(sub.UserId);

                // 6. Gửi thông báo In-App cho người dùng
                await _unitOfWork.Repository<SystemNotification>().AddAsync(new SystemNotification
                {
                    NotificationId = Guid.NewGuid(),
                    UserId = sub.UserId,
                    Title = "Hoàn tiền gói dịch vụ thành công",
                    Message = $"Yêu cầu hoàn tiền gói '{sub.Plan?.Name}' đã được duyệt. Số tiền {refundAmount:N0} đ đã được hoàn vào ví của bạn. Tài khoản của bạn đã được chuyển về gói Basic. Lý do: {request.Reason}",
                    NotificationType = "SUBSCRIPTION_REFUNDED",
                    IsRead = false,
                    TargetActionUrl = "/subscription",
                    CreatedAt = now.UtcDateTime
                });

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransactionAsync();

                _logger.LogInformation("Admin {AdminId} đã hoàn tiền {Amount:N0} đ cho gói #{SubId} của User {UserId}",
                    adminId, refundAmount, sub.Id, sub.UserId);

                return true;
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                _logger.LogError(ex, "Lỗi khi hoàn tiền gói #{SubId} cho User {UserId}", sub.Id, sub.UserId);
                throw;
            }
        }

        private static AdminUserSubscriptionResponse MapToAdminResponse(UserSubscription sub)
        {
            int? remainingDays = null;
            if (sub.ExpiresAt.HasValue)
            {
                var diff = (sub.ExpiresAt.Value - DateTimeOffset.UtcNow).TotalDays;
                remainingDays = Math.Max(0, (int)Math.Ceiling(diff));
            }

            return new AdminUserSubscriptionResponse
            {
                Id = sub.Id,
                UserId = sub.UserId,
                UserFullName = sub.User?.FullName ?? "N/A",
                UserEmail = sub.User?.Email ?? "N/A",
                UserPhoneNumber = sub.User?.PhoneNumber,
                PlanId = sub.PlanId,
                PlanName = sub.Plan?.Name ?? "Gói Dịch Vụ",
                PlanSlug = sub.Plan?.Slug ?? "unknown",
                BillingCycle = sub.BillingCycle,
                Status = sub.Status,
                AmountPaid = sub.AmountPaid,
                PaymentMethod = sub.PaymentMethod,
                PaymentStatus = sub.PaymentStatus,
                PaymentReference = sub.PaymentReference,
                StartsAt = sub.StartsAt,
                ExpiresAt = sub.ExpiresAt,
                RemainingDays = remainingDays,
                CreatedAt = sub.CreatedAt,
                UpdatedAt = sub.UpdatedAt,
                SnapshotMaxAiQuestionsPerMonth = sub.SnapshotMaxAiQuestionsPerMonth,
                SnapshotChatHistoryRetentionDays = sub.SnapshotChatHistoryRetentionDays,
                SnapshotMaxOcrScansPerMonth = sub.SnapshotMaxOcrScansPerMonth,
                SnapshotCanExportPdf = sub.SnapshotCanExportPdf,
                SnapshotCanExportZip = sub.SnapshotCanExportZip,
                SnapshotBookingDiscountPercent = sub.SnapshotBookingDiscountPercent,
                SnapshotFreeConsultationsPerYear = sub.SnapshotFreeConsultationsPerYear
            };
        }
    }
}
