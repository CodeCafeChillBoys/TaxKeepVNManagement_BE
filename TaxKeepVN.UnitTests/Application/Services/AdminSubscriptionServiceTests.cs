using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TaxKeepVN.Application.DTOs.Subscriptions;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Implementations;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;
using Xunit;

namespace TaxKeepVN.UnitTests.Application.Services
{
    public class AdminSubscriptionServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IGenericRepository<UserSubscription>> _mockSubRepo;
        private readonly Mock<IGenericRepository<SubscriptionPlan>> _mockPlanRepo;
        private readonly Mock<IGenericRepository<User>> _mockUserRepo;
        private readonly Mock<IGenericRepository<SubscriptionQuotaUsage>> _mockUsageRepo;
        private readonly Mock<IGenericRepository<UserWallet>> _mockWalletRepo;
        private readonly Mock<IGenericRepository<WalletTransaction>> _mockTxnRepo;
        private readonly Mock<IGenericRepository<SystemNotification>> _mockNotifRepo;
        private readonly Mock<IUserSubscriptionService> _mockUserSubService;
        private readonly Mock<ILogger<AdminSubscriptionService>> _mockLogger;

        public AdminSubscriptionServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockSubRepo = new Mock<IGenericRepository<UserSubscription>>();
            _mockPlanRepo = new Mock<IGenericRepository<SubscriptionPlan>>();
            _mockUserRepo = new Mock<IGenericRepository<User>>();
            _mockUsageRepo = new Mock<IGenericRepository<SubscriptionQuotaUsage>>();
            _mockWalletRepo = new Mock<IGenericRepository<UserWallet>>();
            _mockTxnRepo = new Mock<IGenericRepository<WalletTransaction>>();
            _mockNotifRepo = new Mock<IGenericRepository<SystemNotification>>();
            _mockUserSubService = new Mock<IUserSubscriptionService>();
            _mockLogger = new Mock<ILogger<AdminSubscriptionService>>();

            _mockUnitOfWork.Setup(u => u.Repository<UserSubscription>()).Returns(_mockSubRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<SubscriptionPlan>()).Returns(_mockPlanRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(_mockUserRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<SubscriptionQuotaUsage>()).Returns(_mockUsageRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<UserWallet>()).Returns(_mockWalletRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<WalletTransaction>()).Returns(_mockTxnRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<SystemNotification>()).Returns(_mockNotifRepo.Object);
        }

        [Fact]
        public async Task GetUserSubscriptionsAsync_ShouldReturnPagedResultsWithFilters()
        {
            var user1 = new User { UserId = Guid.NewGuid(), FullName = "Nguyen Van A", Email = "a@test.com" };
            var plan1 = new SubscriptionPlan { Id = Guid.NewGuid(), Name = "Gói Standard", Slug = "standard" };
            var sub1 = new UserSubscription
            {
                Id = Guid.NewGuid(),
                UserId = user1.UserId,
                PlanId = plan1.Id,
                Status = "active",
                BillingCycle = "monthly",
                CreatedAt = DateTimeOffset.UtcNow
            };

            _mockSubRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<UserSubscription> { sub1 });
            _mockPlanRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<SubscriptionPlan> { plan1 });
            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User> { user1 });

            var service = new AdminSubscriptionService(_mockUnitOfWork.Object, _mockUserSubService.Object, _mockLogger.Object);

            var filter = new AdminSubscriptionFilterRequest
            {
                Search = "Nguyen",
                PlanSlug = "standard",
                Status = "active"
            };

            var result = await service.GetUserSubscriptionsAsync(filter);

            result.Should().NotBeNull();
            result.Items.Should().HaveCount(1);
            result.Items[0].UserFullName.Should().Be("Nguyen Van A");
            result.Items[0].PlanName.Should().Be("Gói Standard");
        }

        [Fact]
        public async Task GetSubscriptionByIdAsync_WhenExists_ShouldReturnDetails()
        {
            var subId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var planId = Guid.NewGuid();

            var sub = new UserSubscription
            {
                Id = subId,
                UserId = userId,
                PlanId = planId,
                Status = "active",
                AmountPaid = 49000m
            };

            var user = new User { UserId = userId, FullName = "Tran Thi B", Email = "b@test.com" };
            var plan = new SubscriptionPlan { Id = planId, Name = "Gói Standard", Slug = "standard" };
            var usage = new SubscriptionQuotaUsage
            {
                UserId = userId,
                PeriodMonth = DateTime.UtcNow.ToString("yyyy-MM"),
                AiQuestionsUsed = 15,
                OcrScansUsed = 5
            };

            _mockSubRepo.Setup(r => r.GetByIdAsync(subId)).ReturnsAsync(sub);
            _mockPlanRepo.Setup(r => r.GetByIdAsync(planId)).ReturnsAsync(plan);
            _mockUserRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);
            _mockUsageRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SubscriptionQuotaUsage, bool>>>()))
                .ReturnsAsync(new List<SubscriptionQuotaUsage> { usage });

            var service = new AdminSubscriptionService(_mockUnitOfWork.Object, _mockUserSubService.Object, _mockLogger.Object);

            var result = await service.GetSubscriptionByIdAsync(subId);

            result.Should().NotBeNull();
            result.Id.Should().Be(subId);
            result.UserFullName.Should().Be("Tran Thi B");
            result.CurrentMonthAiQuestionsUsed.Should().Be(15);
            result.CurrentMonthOcrScansUsed.Should().Be(5);
        }

        [Fact]
        public async Task GetSubscriptionByIdAsync_WhenNotFound_ShouldThrowNotFoundException()
        {
            _mockSubRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((UserSubscription?)null);

            var service = new AdminSubscriptionService(_mockUnitOfWork.Object, _mockUserSubService.Object, _mockLogger.Object);

            await Assert.ThrowsAsync<NotFoundException>(() => service.GetSubscriptionByIdAsync(Guid.NewGuid()));
        }

        [Fact]
        public async Task GetAnalyticsAsync_ShouldComputeRevenueMetrics()
        {
            var plan1 = new SubscriptionPlan { Id = Guid.NewGuid(), Name = "Gói Standard", Slug = "standard" };
            var plan2 = new SubscriptionPlan { Id = Guid.NewGuid(), Name = "Gói Premium", Slug = "premium" };

            var sub1 = new UserSubscription
            {
                Id = Guid.NewGuid(),
                PlanId = plan1.Id,
                Status = "active",
                PaymentStatus = "paid",
                BillingCycle = "monthly",
                AmountPaid = 49000m,
                CreatedAt = DateTimeOffset.UtcNow
            };
            var sub2 = new UserSubscription
            {
                Id = Guid.NewGuid(),
                PlanId = plan2.Id,
                Status = "active",
                PaymentStatus = "paid",
                BillingCycle = "yearly",
                AmountPaid = 990000m,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _mockSubRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<UserSubscription> { sub1, sub2 });
            _mockPlanRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<SubscriptionPlan> { plan1, plan2 });

            var service = new AdminSubscriptionService(_mockUnitOfWork.Object, _mockUserSubService.Object, _mockLogger.Object);

            var analytics = await service.GetAnalyticsAsync(null, null);

            analytics.Should().NotBeNull();
            analytics.TotalRevenue.Should().Be(1039000m);
            analytics.TotalPaidTransactions.Should().Be(2);
            analytics.TotalActiveSubscriptions.Should().Be(2);
            analytics.RevenueByPlan.Should().HaveCount(2);
            analytics.RevenueByCycle.Should().HaveCount(2);
            analytics.UserDistribution.Should().HaveCount(2);
        }

        [Fact]
        public async Task GiftSubscriptionAsync_WhenValid_ShouldCreateGiftSubscriptionAndSendNotification()
        {
            var userId = Guid.NewGuid();
            var planId = Guid.NewGuid();
            var adminId = Guid.NewGuid();

            var user = new User { UserId = userId, FullName = "VIP User", Email = "vip@test.com" };
            var plan = new SubscriptionPlan
            {
                Id = planId,
                Name = "Gói Premium",
                Slug = "premium",
                IsActive = true,
                MaxAiQuestionsPerMonth = -1
            };

            _mockUserRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);
            _mockPlanRepo.Setup(r => r.GetByIdAsync(planId)).ReturnsAsync(plan);
            _mockSubRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserSubscription, bool>>>()))
                .ReturnsAsync(new List<UserSubscription>());

            var service = new AdminSubscriptionService(_mockUnitOfWork.Object, _mockUserSubService.Object, _mockLogger.Object);

            var req = new GiftSubscriptionRequest
            {
                UserId = userId,
                PlanId = planId,
                DurationDays = 60,
                Reason = "Quà tặng sự kiện tri ân khách hàng"
            };

            var res = await service.GiftSubscriptionAsync(req, adminId);

            res.Should().NotBeNull();
            res.PlanName.Should().Be("Gói Premium");
            res.AmountPaid.Should().Be(0m);
            res.PaymentMethod.Should().Be("ADMIN_GRANT");
            res.RemainingDays.Should().Be(60);

            _mockSubRepo.Verify(r => r.AddAsync(It.Is<UserSubscription>(s => s.PaymentMethod == "ADMIN_GRANT" && s.UserId == userId)), Times.Once);
            _mockNotifRepo.Verify(r => r.AddAsync(It.Is<SystemNotification>(n => n.NotificationType == "SUBSCRIPTION_GIFT" && n.UserId == userId)), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task ExtendSubscriptionAsync_WhenActive_ShouldExtendExpiresAt()
        {
            var subId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var planId = Guid.NewGuid();
            var adminId = Guid.NewGuid();

            var now = DateTimeOffset.UtcNow;
            var sub = new UserSubscription
            {
                Id = subId,
                UserId = userId,
                PlanId = planId,
                BillingCycle = "monthly",
                Status = "active",
                StartsAt = now.AddDays(-10),
                ExpiresAt = now.AddDays(20)
            };

            var plan = new SubscriptionPlan { Id = planId, Name = "Gói Standard" };
            var user = new User { UserId = userId, FullName = "User 1" };

            _mockSubRepo.Setup(r => r.GetByIdAsync(subId)).ReturnsAsync(sub);
            _mockPlanRepo.Setup(r => r.GetByIdAsync(planId)).ReturnsAsync(plan);
            _mockUserRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

            var service = new AdminSubscriptionService(_mockUnitOfWork.Object, _mockUserSubService.Object, _mockLogger.Object);

            var req = new ExtendSubscriptionRequest
            {
                ExtensionDays = 15,
                Reason = "Bù ngày bảo trì hệ thống"
            };

            var res = await service.ExtendSubscriptionAsync(subId, req, adminId);

            res.Should().NotBeNull();
            res.RemainingDays.Should().Be(35); // 20 + 15 = 35 days
            _mockNotifRepo.Verify(r => r.AddAsync(It.Is<SystemNotification>(n => n.NotificationType == "SUBSCRIPTION_EXTENDED")), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task ExtendSubscriptionAsync_WhenFreePlan_ShouldThrowBadRequestException()
        {
            var subId = Guid.NewGuid();
            var sub = new UserSubscription
            {
                Id = subId,
                BillingCycle = "free",
                ExpiresAt = null,
                Status = "active"
            };

            _mockSubRepo.Setup(r => r.GetByIdAsync(subId)).ReturnsAsync(sub);

            var service = new AdminSubscriptionService(_mockUnitOfWork.Object, _mockUserSubService.Object, _mockLogger.Object);

            var req = new ExtendSubscriptionRequest { ExtensionDays = 30 };
            await Assert.ThrowsAsync<BadRequestException>(() => service.ExtendSubscriptionAsync(subId, req, Guid.NewGuid()));
        }

        [Fact]
        public async Task RefundSubscriptionAsync_WhenValid_ShouldCreditUserWalletAndRevertToBasic()
        {
            var subId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var planId = Guid.NewGuid();
            var adminId = Guid.NewGuid();

            var sub = new UserSubscription
            {
                Id = subId,
                UserId = userId,
                PlanId = planId,
                BillingCycle = "monthly",
                Status = "active",
                PaymentStatus = "paid",
                AmountPaid = 49000m
            };

            var plan = new SubscriptionPlan { Id = planId, Name = "Gói Standard" };
            var wallet = new UserWallet
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                WalletType = "USER",
                AvailableBalance = 10000m
            };

            _mockSubRepo.Setup(r => r.GetByIdAsync(subId)).ReturnsAsync(sub);
            _mockPlanRepo.Setup(r => r.GetByIdAsync(planId)).ReturnsAsync(plan);
            _mockWalletRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserWallet, bool>>>()))
                .ReturnsAsync((Expression<Func<UserWallet, bool>> predicate) =>
                {
                    var list = new List<UserWallet> { wallet };
                    return list.Where(predicate.Compile()).ToList();
                });

            var service = new AdminSubscriptionService(_mockUnitOfWork.Object, _mockUserSubService.Object, _mockLogger.Object);

            var req = new RefundSubscriptionRequest
            {
                RefundAmount = 49000m,
                Reason = "Khách hàng mua nhầm gói"
            };

            var success = await service.RefundSubscriptionAsync(subId, req, adminId);

            success.Should().BeTrue();
            wallet.AvailableBalance.Should().Be(59000m); // 10.000 + 49.000 = 59.000
            sub.Status.Should().Be("cancelled");
            sub.PaymentStatus.Should().Be("refunded");

            _mockTxnRepo.Verify(r => r.AddAsync(It.Is<WalletTransaction>(t => t.TransactionType == "SUBSCRIPTION_REFUND" && t.Amount == 49000m)), Times.Once);
            _mockUserSubService.Verify(s => s.AutoAssignBasicPlanIfNoneAsync(userId), Times.Once);
            _mockNotifRepo.Verify(r => r.AddAsync(It.Is<SystemNotification>(n => n.NotificationType == "SUBSCRIPTION_REFUNDED")), Times.Once);
            _mockUnitOfWork.Verify(u => u.CommitTransactionAsync(), Times.Once);
        }

        [Fact]
        public async Task RefundSubscriptionAsync_WhenAlreadyRefunded_ShouldThrowBadRequestException()
        {
            var subId = Guid.NewGuid();
            var sub = new UserSubscription
            {
                Id = subId,
                Status = "cancelled",
                PaymentStatus = "refunded",
                AmountPaid = 49000m
            };

            _mockSubRepo.Setup(r => r.GetByIdAsync(subId)).ReturnsAsync(sub);

            var service = new AdminSubscriptionService(_mockUnitOfWork.Object, _mockUserSubService.Object, _mockLogger.Object);

            var req = new RefundSubscriptionRequest { Reason = "Hoàn lại lần nữa" };
            await Assert.ThrowsAsync<BadRequestException>(() => service.RefundSubscriptionAsync(subId, req, Guid.NewGuid()));
        }

        [Fact]
        public async Task RefundSubscriptionAsync_WhenFreePlan_ShouldThrowBadRequestException()
        {
            var subId = Guid.NewGuid();
            var sub = new UserSubscription
            {
                Id = subId,
                Status = "active",
                PaymentStatus = "free",
                AmountPaid = 0m
            };

            _mockSubRepo.Setup(r => r.GetByIdAsync(subId)).ReturnsAsync(sub);

            var service = new AdminSubscriptionService(_mockUnitOfWork.Object, _mockUserSubService.Object, _mockLogger.Object);

            var req = new RefundSubscriptionRequest { Reason = "Hoàn tiền gói basic" };
            await Assert.ThrowsAsync<BadRequestException>(() => service.RefundSubscriptionAsync(subId, req, Guid.NewGuid()));
        }
    }
}
