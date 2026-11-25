using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
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
    public class SubscriptionServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IGenericRepository<SubscriptionPlan>> _mockPlanRepo;
        private readonly Mock<IGenericRepository<UserSubscription>> _mockSubRepo;
        private readonly Mock<IGenericRepository<SubscriptionQuotaUsage>> _mockUsageRepo;
        private readonly Mock<IGenericRepository<UserWallet>> _mockWalletRepo;
        private readonly Mock<IGenericRepository<WalletTransaction>> _mockTxnRepo;
        private readonly Mock<IConfiguration> _mockConfig;
        private readonly Mock<ILogger<UserSubscriptionService>> _mockLogger;

        public SubscriptionServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockPlanRepo = new Mock<IGenericRepository<SubscriptionPlan>>();
            _mockSubRepo = new Mock<IGenericRepository<UserSubscription>>();
            _mockUsageRepo = new Mock<IGenericRepository<SubscriptionQuotaUsage>>();
            _mockWalletRepo = new Mock<IGenericRepository<UserWallet>>();
            _mockTxnRepo = new Mock<IGenericRepository<WalletTransaction>>();
            _mockConfig = new Mock<IConfiguration>();
            _mockLogger = new Mock<ILogger<UserSubscriptionService>>();

            _mockUnitOfWork.Setup(u => u.Repository<SubscriptionPlan>()).Returns(_mockPlanRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<UserSubscription>()).Returns(_mockSubRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<SubscriptionQuotaUsage>()).Returns(_mockUsageRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<UserWallet>()).Returns(_mockWalletRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<WalletTransaction>()).Returns(_mockTxnRepo.Object);
        }

        // ══════════════════════════════════════════════════════════════════════
        // 1. SubscriptionPlanService Tests
        // ══════════════════════════════════════════════════════════════════════
        [Fact]
        public async Task GetAllActivePlansAsync_WhenCacheHit_ShouldReturnCachedWithoutQueryingDb()
        {
            var cached = new List<SubscriptionPlanResponse>
            {
                new() { Id = Guid.NewGuid(), Name = "Cached Plan", Slug = "cached", IsActive = true }
            };
            var mockRedis = new Mock<IRedisCacheService>();
            mockRedis.Setup(r => r.GetAsync<List<SubscriptionPlanResponse>>("taxkeep:subscription:plans"))
                .ReturnsAsync(cached);

            var service = new SubscriptionPlanService(_mockUnitOfWork.Object, mockRedis.Object);

            var result = await service.GetAllActivePlansAsync();

            result.Should().BeSameAs(cached);
            _mockPlanRepo.Verify(r => r.FindAsync(It.IsAny<Expression<Func<SubscriptionPlan, bool>>>()), Times.Never);
        }

        [Fact]
        public async Task GetAllActivePlansAsync_WhenCacheMiss_ShouldQueryActivePlansAndSetCache()
        {
            var mockRedis = new Mock<IRedisCacheService>();
            mockRedis.Setup(r => r.GetAsync<List<SubscriptionPlanResponse>>("taxkeep:subscription:plans"))
                .ReturnsAsync((List<SubscriptionPlanResponse>?)null);

            var p1 = new SubscriptionPlan { Id = Guid.NewGuid(), Name = "P2", Slug = "p2", DisplayOrder = 2, IsActive = true };
            var p2 = new SubscriptionPlan { Id = Guid.NewGuid(), Name = "P1", Slug = "p1", DisplayOrder = 1, IsActive = true };
            var pInactive = new SubscriptionPlan { Id = Guid.NewGuid(), Name = "Inactive", Slug = "inactive", DisplayOrder = 3, IsActive = false };

            _mockPlanRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<SubscriptionPlan> { p1, p2, pInactive });
            _mockPlanRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SubscriptionPlan, bool>>>()))
                .ReturnsAsync((Expression<Func<SubscriptionPlan, bool>> pred) =>
                    new[] { p1, p2, pInactive }.Where(pred.Compile()).ToList());

            var service = new SubscriptionPlanService(_mockUnitOfWork.Object, mockRedis.Object);

            var result = await service.GetAllActivePlansAsync();

            result.Should().HaveCount(2);
            result[0].Slug.Should().Be("p1");
            result[1].Slug.Should().Be("p2");
            mockRedis.Verify(r => r.SetAsync("taxkeep:subscription:plans", It.IsAny<List<SubscriptionPlanResponse>>(), TimeSpan.FromHours(24)), Times.Once);
        }

        [Fact]
        public async Task GetAllPlansForAdminAsync_ShouldReturnAllPlansOrderedByDisplayOrder()
        {
            var p1 = new SubscriptionPlan { Id = Guid.NewGuid(), Name = "P2", Slug = "p2", DisplayOrder = 2, IsActive = false };
            var p2 = new SubscriptionPlan { Id = Guid.NewGuid(), Name = "P1", Slug = "p1", DisplayOrder = 1, IsActive = true };
            _mockPlanRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<SubscriptionPlan> { p1, p2 });

            var service = new SubscriptionPlanService(_mockUnitOfWork.Object);

            var result = await service.GetAllPlansForAdminAsync();

            result.Should().HaveCount(2);
            result[0].Slug.Should().Be("p1");
            result[1].Slug.Should().Be("p2");
        }

        [Fact]
        public async Task GetPlanByIdAsync_WhenExists_ShouldReturnPlan()
        {
            var plan = new SubscriptionPlan { Id = Guid.NewGuid(), Name = "Standard", Slug = "standard" };
            _mockPlanRepo.Setup(r => r.GetByIdAsync(plan.Id)).ReturnsAsync(plan);

            var service = new SubscriptionPlanService(_mockUnitOfWork.Object);

            var result = await service.GetPlanByIdAsync(plan.Id);

            result.Id.Should().Be(plan.Id);
            result.Slug.Should().Be("standard");
        }

        [Fact]
        public async Task GetPlanByIdAsync_WhenNotFound_ShouldThrowNotFoundException()
        {
            _mockPlanRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((SubscriptionPlan?)null);

            var service = new SubscriptionPlanService(_mockUnitOfWork.Object);

            await Assert.ThrowsAsync<NotFoundException>(() => service.GetPlanByIdAsync(Guid.NewGuid()));
        }

        [Fact]
        public async Task CreatePlanAsync_WhenValid_ShouldAddPlanAndInvalidateCache()
        {
            var mockRedis = new Mock<IRedisCacheService>();
            _mockPlanRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SubscriptionPlan, bool>>>()))
                .ReturnsAsync(new List<SubscriptionPlan>());

            var service = new SubscriptionPlanService(_mockUnitOfWork.Object, mockRedis.Object);
            var req = new CreateSubscriptionPlanRequest
            {
                Name = "  Gói Pro  ",
                Slug = "  PRO  ",
                PriceMonthly = 199000m,
                PriceYearly = 1990000m,
                IsActive = true
            };

            var res = await service.CreatePlanAsync(req);

            res.Name.Should().Be("Gói Pro");
            res.Slug.Should().Be("pro");
            _mockPlanRepo.Verify(r => r.AddAsync(It.Is<SubscriptionPlan>(p => p.Slug == "pro" && p.Name == "Gói Pro")), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
            mockRedis.Verify(r => r.RemoveAsync("taxkeep:subscription:plans"), Times.Once);
        }

        [Fact]
        public async Task CreatePlanAsync_WhenSlugAlreadyExists_ShouldThrowConflictException()
        {
            var existing = new List<SubscriptionPlan> { new SubscriptionPlan { Slug = "standard" } };
            _mockPlanRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SubscriptionPlan, bool>>>()))
                .ReturnsAsync(existing);

            var service = new SubscriptionPlanService(_mockUnitOfWork.Object);

            var req = new CreateSubscriptionPlanRequest { Name = "Gói Tiêu Chuẩn", Slug = "standard" };
            await Assert.ThrowsAsync<ConflictException>(() => service.CreatePlanAsync(req));
        }

        [Fact]
        public async Task UpdatePlanAsync_WhenNotFound_ShouldThrowNotFoundException()
        {
            _mockPlanRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((SubscriptionPlan?)null);
            var service = new SubscriptionPlanService(_mockUnitOfWork.Object);

            await Assert.ThrowsAsync<NotFoundException>(() => service.UpdatePlanAsync(Guid.NewGuid(), new UpdateSubscriptionPlanRequest()));
        }

        [Fact]
        public async Task UpdatePlanAsync_WhenValid_ShouldUpdateFieldsAndInvalidateCache()
        {
            var mockRedis = new Mock<IRedisCacheService>();
            var plan = new SubscriptionPlan
            {
                Id = Guid.NewGuid(),
                Name = "Old Name",
                Slug = "standard",
                PriceMonthly = 49000m,
                CanExportPdf = false,
                IsActive = true
            };
            _mockPlanRepo.Setup(r => r.GetByIdAsync(plan.Id)).ReturnsAsync(plan);

            var service = new SubscriptionPlanService(_mockUnitOfWork.Object, mockRedis.Object);
            var req = new UpdateSubscriptionPlanRequest
            {
                Name = " New Name ",
                PriceMonthly = 59000m,
                CanExportPdf = true,
                IsActive = false
            };

            var res = await service.UpdatePlanAsync(plan.Id, req);

            res.Name.Should().Be("New Name");
            res.PriceMonthly.Should().Be(59000m);
            res.CanExportPdf.Should().BeTrue();
            res.IsActive.Should().BeFalse();
            _mockPlanRepo.Verify(r => r.Update(plan), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
            mockRedis.Verify(r => r.RemoveAsync("taxkeep:subscription:plans"), Times.Once);
        }

        [Fact]
        public async Task DeletePlanAsync_WhenNotFound_ShouldThrowNotFoundException()
        {
            _mockPlanRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((SubscriptionPlan?)null);
            var service = new SubscriptionPlanService(_mockUnitOfWork.Object);

            await Assert.ThrowsAsync<NotFoundException>(() => service.DeletePlanAsync(Guid.NewGuid()));
        }

        [Fact]
        public async Task DeletePlanAsync_WhenDeletingBasicPlan_ShouldThrowBadRequestException()
        {
            var basicPlan = new SubscriptionPlan { Id = Guid.NewGuid(), Slug = "basic", Name = "Gói Basic" };
            _mockPlanRepo.Setup(r => r.GetByIdAsync(basicPlan.Id)).ReturnsAsync(basicPlan);

            var service = new SubscriptionPlanService(_mockUnitOfWork.Object);

            await Assert.ThrowsAsync<BadRequestException>(() => service.DeletePlanAsync(basicPlan.Id));
        }

        [Fact]
        public async Task DeletePlanAsync_WhenValidNonBasicPlan_ShouldSoftDeleteAndInvalidateCache()
        {
            var mockRedis = new Mock<IRedisCacheService>();
            var plan = new SubscriptionPlan { Id = Guid.NewGuid(), Slug = "standard", Name = "Standard", IsActive = true };
            _mockPlanRepo.Setup(r => r.GetByIdAsync(plan.Id)).ReturnsAsync(plan);

            var service = new SubscriptionPlanService(_mockUnitOfWork.Object, mockRedis.Object);

            var result = await service.DeletePlanAsync(plan.Id);

            result.Should().BeTrue();
            plan.IsActive.Should().BeFalse();
            _mockPlanRepo.Verify(r => r.Update(plan), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
            mockRedis.Verify(r => r.RemoveAsync("taxkeep:subscription:plans"), Times.Once);
        }

        [Fact]
        public async Task SeedDefaultPlansIfEmptyAsync_WhenEmpty_ShouldSeedThreeDefaultPlans()
        {
            _mockPlanRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<SubscriptionPlan>());

            var service = new SubscriptionPlanService(_mockUnitOfWork.Object);

            await service.SeedDefaultPlansIfEmptyAsync();

            _mockPlanRepo.Verify(r => r.AddAsync(It.Is<SubscriptionPlan>(p => p.Slug == "basic")), Times.Once);
            _mockPlanRepo.Verify(r => r.AddAsync(It.Is<SubscriptionPlan>(p => p.Slug == "standard")), Times.Once);
            _mockPlanRepo.Verify(r => r.AddAsync(It.Is<SubscriptionPlan>(p => p.Slug == "premium")), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task SeedDefaultPlansIfEmptyAsync_WhenNotEmpty_ShouldDoNothing()
        {
            _mockPlanRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<SubscriptionPlan> { new() { Slug = "basic" } });

            var service = new SubscriptionPlanService(_mockUnitOfWork.Object);

            await service.SeedDefaultPlansIfEmptyAsync();

            _mockPlanRepo.Verify(r => r.AddAsync(It.IsAny<SubscriptionPlan>()), Times.Never);
        }

        // ══════════════════════════════════════════════════════════════════════
        // 2. UserSubscriptionService Tests
        // ══════════════════════════════════════════════════════════════════════
        [Fact]
        public async Task GetActiveSubscriptionAsync_WhenActiveExistsWithNullPlan_ShouldLoadPlanAndReturn()
        {
            var userId = Guid.NewGuid();
            var plan = new SubscriptionPlan { Id = Guid.NewGuid(), Name = "Standard", Slug = "standard" };
            var sub = new UserSubscription
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PlanId = plan.Id,
                Status = "active",
                BillingCycle = "monthly",
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(10),
                Plan = null
            };
            _mockSubRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserSubscription, bool>>>()))
                .ReturnsAsync(new List<UserSubscription> { sub });
            _mockPlanRepo.Setup(r => r.GetByIdAsync(plan.Id)).ReturnsAsync(plan);

            var service = new UserSubscriptionService(_mockUnitOfWork.Object, _mockConfig.Object, new Mock<ISubscriptionPlanService>().Object, _mockLogger.Object);

            var res = await service.GetActiveSubscriptionAsync(userId);

            res.Should().NotBeNull();
            res!.PlanName.Should().Be("Standard");
            res.RemainingDays!.Value.Should().BeGreaterThanOrEqualTo(9);
        }

        [Fact]
        public async Task GetOrCreateActiveSubscriptionEntityAsync_WhenExpired_ShouldMarkExpiredAndAssignBasic()
        {
            var userId = Guid.NewGuid();
            var expiredSub = new UserSubscription
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Status = "active",
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1)
            };
            var basicPlan = new SubscriptionPlan { Id = Guid.NewGuid(), Name = "Basic", Slug = "basic", MaxAiQuestionsPerMonth = 20 };

            _mockSubRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserSubscription, bool>>>()))
                .ReturnsAsync(new List<UserSubscription> { expiredSub });
            _mockPlanRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SubscriptionPlan, bool>>>()))
                .ReturnsAsync(new List<SubscriptionPlan> { basicPlan });

            var service = new UserSubscriptionService(_mockUnitOfWork.Object, _mockConfig.Object, new Mock<ISubscriptionPlanService>().Object, _mockLogger.Object);

            var res = await service.GetOrCreateActiveSubscriptionEntityAsync(userId);

            expiredSub.Status.Should().Be("expired");
            _mockSubRepo.Verify(r => r.Update(expiredSub), Times.Once);
            res.PlanId.Should().Be(basicPlan.Id);
            res.BillingCycle.Should().Be("free");
        }

        [Fact]
        public async Task AutoAssignBasicPlanIfNoneAsync_WhenBasicMissing_ShouldSeedThenAssign()
        {
            var userId = Guid.NewGuid();
            var basicPlan = new SubscriptionPlan { Id = Guid.NewGuid(), Name = "Basic", Slug = "basic" };
            int callCount = 0;
            _mockPlanRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SubscriptionPlan, bool>>>()))
                .ReturnsAsync(() =>
                {
                    callCount++;
                    return callCount == 1 ? new List<SubscriptionPlan>() : new List<SubscriptionPlan> { basicPlan };
                });

            var mockPlanService = new Mock<ISubscriptionPlanService>();
            var service = new UserSubscriptionService(_mockUnitOfWork.Object, _mockConfig.Object, mockPlanService.Object, _mockLogger.Object);

            var res = await service.AutoAssignBasicPlanIfNoneAsync(userId);

            mockPlanService.Verify(s => s.SeedDefaultPlansIfEmptyAsync(), Times.Once);
            res.PlanId.Should().Be(basicPlan.Id);
            res.Status.Should().Be("active");
            _mockSubRepo.Verify(r => r.AddAsync(It.IsAny<UserSubscription>()), Times.Once);
        }

        [Fact]
        public async Task PurchaseAsync_WhenPlanNotFoundOrInactive_ShouldThrowNotFoundException()
        {
            _mockPlanRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new SubscriptionPlan { IsActive = false });
            var service = new UserSubscriptionService(_mockUnitOfWork.Object, _mockConfig.Object, new Mock<ISubscriptionPlanService>().Object, _mockLogger.Object);

            await Assert.ThrowsAsync<NotFoundException>(() => service.PurchaseAsync(Guid.NewGuid(), new PurchaseSubscriptionRequest()));
        }

        [Fact]
        public async Task PurchaseAsync_WhenInvalidBillingCycle_ShouldThrowBadRequestException()
        {
            var plan = new SubscriptionPlan { Id = Guid.NewGuid(), IsActive = true };
            _mockPlanRepo.Setup(r => r.GetByIdAsync(plan.Id)).ReturnsAsync(plan);
            var service = new UserSubscriptionService(_mockUnitOfWork.Object, _mockConfig.Object, new Mock<ISubscriptionPlanService>().Object, _mockLogger.Object);

            var ex = await Assert.ThrowsAsync<BadRequestException>(() => service.PurchaseAsync(Guid.NewGuid(), new PurchaseSubscriptionRequest
            {
                PlanId = plan.Id,
                BillingCycle = "weekly"
            }));
            ex.ErrorCode.Should().Be("INVALID_BILLING_CYCLE");
        }

        [Fact]
        public async Task PurchaseAsync_WhenFreePlan_ShouldDeactivateOldAndActivateFreeImmediately()
        {
            var userId = Guid.NewGuid();
            var plan = new SubscriptionPlan { Id = Guid.NewGuid(), Name = "Basic", PriceMonthly = 0m, IsActive = true };
            var oldActive = new UserSubscription { Id = Guid.NewGuid(), UserId = userId, Status = "active" };

            _mockPlanRepo.Setup(r => r.GetByIdAsync(plan.Id)).ReturnsAsync(plan);
            _mockSubRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserSubscription, bool>>>()))
                .ReturnsAsync(new List<UserSubscription> { oldActive });

            var service = new UserSubscriptionService(_mockUnitOfWork.Object, _mockConfig.Object, new Mock<ISubscriptionPlanService>().Object, _mockLogger.Object);

            var res = await service.PurchaseAsync(userId, new PurchaseSubscriptionRequest { PlanId = plan.Id, BillingCycle = "monthly" });

            res.IsCompleted.Should().BeTrue();
            res.PaymentMethod.Should().Be("FREE");
            oldActive.Status.Should().Be("cancelled");
            _mockSubRepo.Verify(r => r.AddAsync(It.Is<UserSubscription>(s => s.PaymentMethod == "FREE" && s.Status == "active")), Times.Once);
        }
        [Fact]
        public async Task PurchaseAsync_WhenWalletBalanceInsufficient_ShouldThrowBadRequestException()
        {
            var userId = Guid.NewGuid();
            var planId = Guid.NewGuid();
            var plan = new SubscriptionPlan
            {
                Id = planId,
                Name = "Gói Standard",
                Slug = "standard",
                PriceMonthly = 49000m,
                IsActive = true
            };

            _mockPlanRepo.Setup(r => r.GetByIdAsync(planId)).ReturnsAsync(plan);

            var wallet = new UserWallet
            {
                UserId = userId,
                WalletType = "USER",
                AvailableBalance = 10000m // Chỉ có 10.000đ nhưng gói giá 49.000đ
            };
            _mockWalletRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserWallet, bool>>>()))
                .ReturnsAsync(new List<UserWallet> { wallet });

            var planService = new Mock<ISubscriptionPlanService>();
            var service = new UserSubscriptionService(_mockUnitOfWork.Object, _mockConfig.Object, planService.Object, _mockLogger.Object);

            var req = new PurchaseSubscriptionRequest
            {
                PlanId = planId,
                BillingCycle = "monthly",
                PaymentMethod = "WALLET"
            };

            await Assert.ThrowsAsync<BadRequestException>(() => service.PurchaseAsync(userId, req));
        }

        [Fact]
        public async Task PurchaseAsync_WhenWalletBalanceSufficient_ShouldDeductBalanceAndActivate()
        {
            var userId = Guid.NewGuid();
            var planId = Guid.NewGuid();
            var plan = new SubscriptionPlan
            {
                Id = planId,
                Name = "Gói Standard",
                Slug = "standard",
                PriceMonthly = 49000m,
                IsActive = true,
                MaxAiQuestionsPerMonth = 100
            };

            _mockPlanRepo.Setup(r => r.GetByIdAsync(planId)).ReturnsAsync(plan);

            var wallet = new UserWallet
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                WalletType = "USER",
                AvailableBalance = 100000m // Có 100.000đ
            };
            _mockWalletRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserWallet, bool>>>()))
                .ReturnsAsync(new List<UserWallet> { wallet });
            _mockSubRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserSubscription, bool>>>()))
                .ReturnsAsync(new List<UserSubscription>());

            var planService = new Mock<ISubscriptionPlanService>();
            var service = new UserSubscriptionService(_mockUnitOfWork.Object, _mockConfig.Object, planService.Object, _mockLogger.Object);

            var req = new PurchaseSubscriptionRequest
            {
                PlanId = planId,
                BillingCycle = "monthly",
                PaymentMethod = "WALLET"
            };

            var res = await service.PurchaseAsync(userId, req);

            res.Should().NotBeNull();
            res.IsCompleted.Should().BeTrue();
            wallet.AvailableBalance.Should().Be(51000m); // 100.000 - 49.000 = 51.000
            _mockTxnRepo.Verify(r => r.AddAsync(It.Is<WalletTransaction>(t => t.TransactionType == "PAYMENT_SUBSCRIPTION")), Times.Once);
        }

        [Fact]
        public async Task PurchaseAsync_WhenWalletYearly_ShouldUseYearlyPriceAndExpireInOneYear()
        {
            var userId = Guid.NewGuid();
            var plan = new SubscriptionPlan { Id = Guid.NewGuid(), Name = "Premium", PriceMonthly = 99000m, PriceYearly = 990000m, IsActive = true };
            var wallet = new UserWallet { Id = Guid.NewGuid(), UserId = userId, WalletType = "USER", AvailableBalance = 1500000m };

            _mockPlanRepo.Setup(r => r.GetByIdAsync(plan.Id)).ReturnsAsync(plan);
            _mockWalletRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserWallet, bool>>>()))
                .ReturnsAsync(new List<UserWallet> { wallet });
            _mockSubRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserSubscription, bool>>>()))
                .ReturnsAsync(new List<UserSubscription>());

            var service = new UserSubscriptionService(_mockUnitOfWork.Object, _mockConfig.Object, new Mock<ISubscriptionPlanService>().Object, _mockLogger.Object);

            var res = await service.PurchaseAsync(userId, new PurchaseSubscriptionRequest { PlanId = plan.Id, BillingCycle = "yearly", PaymentMethod = "WALLET" });

            res.Amount.Should().Be(990000m);
            wallet.AvailableBalance.Should().Be(510000m);
            _mockSubRepo.Verify(r => r.AddAsync(It.Is<UserSubscription>(s =>
                s.BillingCycle == "yearly" &&
                s.ExpiresAt.HasValue &&
                (s.ExpiresAt.Value - s.StartsAt).TotalDays >= 364)), Times.Once);
        }

        [Fact]
        public async Task PurchaseAsync_WhenWalletSaveThrows_ShouldRollbackAndRethrow()
        {
            var userId = Guid.NewGuid();
            var plan = new SubscriptionPlan { Id = Guid.NewGuid(), Name = "Standard", PriceMonthly = 49000m, IsActive = true };
            var wallet = new UserWallet { Id = Guid.NewGuid(), UserId = userId, WalletType = "USER", AvailableBalance = 100000m };

            _mockPlanRepo.Setup(r => r.GetByIdAsync(plan.Id)).ReturnsAsync(plan);
            _mockWalletRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserWallet, bool>>>()))
                .ReturnsAsync(new List<UserWallet> { wallet });
            _mockSubRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserSubscription, bool>>>()))
                .ReturnsAsync(new List<UserSubscription>());
            _mockUnitOfWork.Setup(u => u.SaveChangesAsync()).ThrowsAsync(new Exception("DB fail"));

            var service = new UserSubscriptionService(_mockUnitOfWork.Object, _mockConfig.Object, new Mock<ISubscriptionPlanService>().Object, _mockLogger.Object);

            await Assert.ThrowsAsync<Exception>(() => service.PurchaseAsync(userId, new PurchaseSubscriptionRequest { PlanId = plan.Id, BillingCycle = "monthly", PaymentMethod = "WALLET" }));
            _mockUnitOfWork.Verify(u => u.RollbackTransactionAsync(), Times.Once);
        }

        [Theory]
        [InlineData("VNPAY", true, false)]
        [InlineData("VIETQR", false, true)]
        public async Task PurchaseAsync_WhenExternalPaymentMethod_ShouldCreatePendingAndReturnCorrectUrl(string method, bool expectCheckoutUrl, bool expectQrCode)
        {
            var userId = Guid.NewGuid();
            var plan = new SubscriptionPlan { Id = Guid.NewGuid(), Name = "Standard", PriceMonthly = 49000m, IsActive = true };
            _mockPlanRepo.Setup(r => r.GetByIdAsync(plan.Id)).ReturnsAsync(plan);

            var service = new UserSubscriptionService(_mockUnitOfWork.Object, _mockConfig.Object, new Mock<ISubscriptionPlanService>().Object, _mockLogger.Object);

            var res = await service.PurchaseAsync(userId, new PurchaseSubscriptionRequest { PlanId = plan.Id, BillingCycle = "monthly", PaymentMethod = method });

            res.IsCompleted.Should().BeFalse();
            (res.CheckoutUrl != null).Should().Be(expectCheckoutUrl);
            (res.QrCode != null).Should().Be(expectQrCode);
            _mockSubRepo.Verify(r => r.AddAsync(It.Is<UserSubscription>(s => s.Status == "pending" && s.PaymentStatus == "pending")), Times.Once);
        }

        [Fact]
        public async Task HandlePaymentCallbackAsync_WhenNoPendingFound_ShouldReturnFalse()
        {
            _mockSubRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserSubscription, bool>>>()))
                .ReturnsAsync(new List<UserSubscription>());

            var service = new UserSubscriptionService(_mockUnitOfWork.Object, _mockConfig.Object, new Mock<ISubscriptionPlanService>().Object, _mockLogger.Object);

            var result = await service.HandlePaymentCallbackAsync(123456, true);
            result.Should().BeFalse();
        }

        [Fact]
        public async Task HandlePaymentCallbackAsync_WhenSuccess_ShouldActivatePendingAndDeactivateOld()
        {
            var userId = Guid.NewGuid();
            var pending = new UserSubscription { Id = Guid.NewGuid(), UserId = userId, BillingCycle = "monthly", Status = "pending", PaymentReference = "99999" };
            var oldActive = new UserSubscription { Id = Guid.NewGuid(), UserId = userId, Status = "active" };

            int findCalls = 0;
            _mockSubRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserSubscription, bool>>>()))
                .ReturnsAsync(() =>
                {
                    findCalls++;
                    return findCalls == 1 ? new List<UserSubscription> { pending } : new List<UserSubscription> { oldActive };
                });

            var service = new UserSubscriptionService(_mockUnitOfWork.Object, _mockConfig.Object, new Mock<ISubscriptionPlanService>().Object, _mockLogger.Object);

            var ok = await service.HandlePaymentCallbackAsync(99999, true);

            ok.Should().BeTrue();
            pending.Status.Should().Be("active");
            pending.PaymentStatus.Should().Be("paid");
            pending.ExpiresAt.Should().NotBeNull();
            oldActive.Status.Should().Be("cancelled");
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task HandlePaymentCallbackAsync_WhenFailed_ShouldCancelPendingAndMarkFailed()
        {
            var pending = new UserSubscription { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Status = "pending", PaymentReference = "88888" };
            _mockSubRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserSubscription, bool>>>()))
                .ReturnsAsync(new List<UserSubscription> { pending });

            var service = new UserSubscriptionService(_mockUnitOfWork.Object, _mockConfig.Object, new Mock<ISubscriptionPlanService>().Object, _mockLogger.Object);

            var ok = await service.HandlePaymentCallbackAsync(88888, false);

            ok.Should().BeTrue();
            pending.Status.Should().Be("cancelled");
            pending.PaymentStatus.Should().Be("failed");
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task GetSubscriptionHistoryAsync_ShouldReturnOrderedByCreatedAtDescWithPlanNames()
        {
            var userId = Guid.NewGuid();
            var plan = new SubscriptionPlan { Id = Guid.NewGuid(), Name = "Standard", Slug = "standard" };
            var older = new UserSubscription { Id = Guid.NewGuid(), UserId = userId, PlanId = plan.Id, CreatedAt = DateTimeOffset.UtcNow.AddDays(-10) };
            var newer = new UserSubscription { Id = Guid.NewGuid(), UserId = userId, PlanId = plan.Id, CreatedAt = DateTimeOffset.UtcNow };

            _mockSubRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserSubscription, bool>>>()))
                .ReturnsAsync(new List<UserSubscription> { older, newer });
            _mockPlanRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<SubscriptionPlan> { plan });

            var service = new UserSubscriptionService(_mockUnitOfWork.Object, _mockConfig.Object, new Mock<ISubscriptionPlanService>().Object, _mockLogger.Object);

            var history = await service.GetSubscriptionHistoryAsync(userId);

            history.Should().HaveCount(2);
            history[0].Id.Should().Be(newer.Id);
            history[0].PlanName.Should().Be("Standard");
            history[1].Id.Should().Be(older.Id);
        }

        // ══════════════════════════════════════════════════════════════════════
        // 3. SubscriptionQuotaService Tests
        // ══════════════════════════════════════════════════════════════════════
        [Fact]
        public async Task GetQuotaStatusAsync_WhenLimitedAndUsageExists_ShouldCalculateRemainingCorrectly()
        {
            var userId = Guid.NewGuid();
            var sub = new UserSubscription
            {
                UserId = userId,
                Plan = new SubscriptionPlan { Name = "Standard", Slug = "standard" },
                BillingCycle = "monthly",
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(15),
                SnapshotMaxAiQuestionsPerMonth = 100,
                SnapshotMaxOcrScansPerMonth = 30,
                SnapshotCanExportPdf = true,
                SnapshotCanExportZip = false,
                SnapshotBookingDiscountPercent = 5,
                SnapshotFreeConsultationsPerYear = 1,
                SnapshotChatHistoryRetentionDays = 365
            };
            var usage = new SubscriptionQuotaUsage { UserId = userId, PeriodMonth = DateTime.UtcNow.ToString("yyyy-MM"), AiQuestionsUsed = 25, OcrScansUsed = 10 };

            var userSubService = new Mock<IUserSubscriptionService>();
            userSubService.Setup(s => s.GetOrCreateActiveSubscriptionEntityAsync(userId)).ReturnsAsync(sub);
            _mockUsageRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SubscriptionQuotaUsage, bool>>>()))
                .ReturnsAsync(new List<SubscriptionQuotaUsage> { usage });

            var service = new SubscriptionQuotaService(_mockUnitOfWork.Object, userSubService.Object);

            var status = await service.GetQuotaStatusAsync(userId);

            status.PlanSlug.Should().Be("standard");
            status.AiQuestionsRemaining.Should().Be(75);
            status.OcrScansRemaining.Should().Be(20);
            status.RemainingDays!.Value.Should().BeGreaterThanOrEqualTo(14);
            status.CanExportPdf.Should().BeTrue();
            status.BookingDiscountPercent.Should().Be(5);
        }

        [Fact]
        public async Task GetQuotaStatusAsync_WhenUnlimitedAndUsageMissing_ShouldCreateUsageAndReturnNullRemaining()
        {
            var userId = Guid.NewGuid();
            var sub = new UserSubscription
            {
                UserId = userId,
                Plan = new SubscriptionPlan { Name = "Premium", Slug = "premium" },
                BillingCycle = "free",
                ExpiresAt = null,
                SnapshotMaxAiQuestionsPerMonth = -1,
                SnapshotMaxOcrScansPerMonth = -1
            };

            var userSubService = new Mock<IUserSubscriptionService>();
            userSubService.Setup(s => s.GetOrCreateActiveSubscriptionEntityAsync(userId)).ReturnsAsync(sub);
            _mockUsageRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SubscriptionQuotaUsage, bool>>>()))
                .ReturnsAsync(new List<SubscriptionQuotaUsage>());

            var service = new SubscriptionQuotaService(_mockUnitOfWork.Object, userSubService.Object);

            var status = await service.GetQuotaStatusAsync(userId);

            status.AiQuestionsRemaining.Should().BeNull();
            status.OcrScansRemaining.Should().BeNull();
            status.RemainingDays.Should().BeNull();
            _mockUsageRepo.Verify(r => r.AddAsync(It.IsAny<SubscriptionQuotaUsage>()), Times.Once);
        }

        [Fact]
        public async Task CheckAndConsumeAiQuestionAsync_WhenWithinLimit_ShouldIncrementAndSave()
        {
            var userId = Guid.NewGuid();
            var sub = new UserSubscription { UserId = userId, SnapshotMaxAiQuestionsPerMonth = 20 };
            var usage = new SubscriptionQuotaUsage { UserId = userId, AiQuestionsUsed = 5 };

            var userSubService = new Mock<IUserSubscriptionService>();
            userSubService.Setup(s => s.GetOrCreateActiveSubscriptionEntityAsync(userId)).ReturnsAsync(sub);
            _mockUsageRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SubscriptionQuotaUsage, bool>>>()))
                .ReturnsAsync(new List<SubscriptionQuotaUsage> { usage });

            var service = new SubscriptionQuotaService(_mockUnitOfWork.Object, userSubService.Object);

            await service.CheckAndConsumeAiQuestionAsync(userId);

            usage.AiQuestionsUsed.Should().Be(6);
            _mockUsageRepo.Verify(r => r.Update(usage), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task CheckAndConsumeOcrScanAsync_WhenCountZero_ShouldReturnEarlyWithoutSaving()
        {
            var userSubService = new Mock<IUserSubscriptionService>();
            var service = new SubscriptionQuotaService(_mockUnitOfWork.Object, userSubService.Object);

            await service.CheckAndConsumeOcrScanAsync(Guid.NewGuid(), 0);

            userSubService.Verify(s => s.GetOrCreateActiveSubscriptionEntityAsync(It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task CheckCanExportPdfAsync_WhenSupported_ShouldNotThrow()
        {
            var userId = Guid.NewGuid();
            var userSubService = new Mock<IUserSubscriptionService>();
            userSubService.Setup(s => s.GetOrCreateActiveSubscriptionEntityAsync(userId))
                .ReturnsAsync(new UserSubscription { UserId = userId, SnapshotCanExportPdf = true });

            var service = new SubscriptionQuotaService(_mockUnitOfWork.Object, userSubService.Object);
            await service.CheckCanExportPdfAsync(userId);
        }

        [Fact]
        public async Task CheckCanExportZipAsync_WhenNotSupported_ShouldThrowBadRequestException()
        {
            var userId = Guid.NewGuid();
            var userSubService = new Mock<IUserSubscriptionService>();
            userSubService.Setup(s => s.GetOrCreateActiveSubscriptionEntityAsync(userId))
                .ReturnsAsync(new UserSubscription { UserId = userId, SnapshotCanExportZip = false });

            var service = new SubscriptionQuotaService(_mockUnitOfWork.Object, userSubService.Object);

            var ex = await Assert.ThrowsAsync<BadRequestException>(() => service.CheckCanExportZipAsync(userId));
            ex.ErrorCode.Should().Be("EXPORT_ZIP_NOT_ALLOWED");
        }

        [Fact]
        public async Task CheckCanExportZipAsync_WhenSupported_ShouldNotThrow()
        {
            var userId = Guid.NewGuid();
            var userSubService = new Mock<IUserSubscriptionService>();
            userSubService.Setup(s => s.GetOrCreateActiveSubscriptionEntityAsync(userId))
                .ReturnsAsync(new UserSubscription { UserId = userId, SnapshotCanExportZip = true });

            var service = new SubscriptionQuotaService(_mockUnitOfWork.Object, userSubService.Object);
            await service.CheckCanExportZipAsync(userId);
        }

        [Fact]
        public async Task GetBookingDiscountPercentAsync_ShouldReturnSnapshotPercent()
        {
            var userId = Guid.NewGuid();
            var userSubService = new Mock<IUserSubscriptionService>();
            userSubService.Setup(s => s.GetOrCreateActiveSubscriptionEntityAsync(userId))
                .ReturnsAsync(new UserSubscription { UserId = userId, SnapshotBookingDiscountPercent = 10 });

            var service = new SubscriptionQuotaService(_mockUnitOfWork.Object, userSubService.Object);
            var discount = await service.GetBookingDiscountPercentAsync(userId);

            discount.Should().Be(10);
        }
        [Fact]
        public async Task CheckAndConsumeAiQuestionAsync_WhenLimitReached_ShouldThrowBadRequestException()
        {
            var userId = Guid.NewGuid();
            var sub = new UserSubscription
            {
                UserId = userId,
                SnapshotMaxAiQuestionsPerMonth = 20
            };

            var userSubService = new Mock<IUserSubscriptionService>();
            userSubService.Setup(s => s.GetOrCreateActiveSubscriptionEntityAsync(userId)).ReturnsAsync(sub);

            var usage = new SubscriptionQuotaUsage
            {
                UserId = userId,
                PeriodMonth = DateTime.UtcNow.ToString("yyyy-MM"),
                AiQuestionsUsed = 20 // Đã dùng hết 20 câu
            };
            _mockUsageRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SubscriptionQuotaUsage, bool>>>()))
                .ReturnsAsync(new List<SubscriptionQuotaUsage> { usage });

            var service = new SubscriptionQuotaService(_mockUnitOfWork.Object, userSubService.Object);

            await Assert.ThrowsAsync<BadRequestException>(() => service.CheckAndConsumeAiQuestionAsync(userId));
        }

        [Fact]
        public async Task CheckCanExportPdfAsync_WhenPlanDoesNotSupport_ShouldThrowBadRequestException()
        {
            var userId = Guid.NewGuid();
            var basicSub = new UserSubscription
            {
                UserId = userId,
                SnapshotCanExportPdf = false // Basic không hỗ trợ PDF
            };

            var userSubService = new Mock<IUserSubscriptionService>();
            userSubService.Setup(s => s.GetOrCreateActiveSubscriptionEntityAsync(userId)).ReturnsAsync(basicSub);

            var service = new SubscriptionQuotaService(_mockUnitOfWork.Object, userSubService.Object);

            await Assert.ThrowsAsync<BadRequestException>(() => service.CheckCanExportPdfAsync(userId));
        }

        [Fact]
        public async Task CheckAndConsumeOcrScanAsync_WhenLimitReached_ShouldThrowBadRequestException()
        {
            var userId = Guid.NewGuid();
            var sub = new UserSubscription
            {
                UserId = userId,
                SnapshotMaxOcrScansPerMonth = 5 // Basic: 5 scans
            };

            var userSubService = new Mock<IUserSubscriptionService>();
            userSubService.Setup(s => s.GetOrCreateActiveSubscriptionEntityAsync(userId)).ReturnsAsync(sub);

            var usage = new SubscriptionQuotaUsage
            {
                UserId = userId,
                PeriodMonth = DateTime.UtcNow.ToString("yyyy-MM"),
                OcrScansUsed = 5 // Đã dùng hết 5 lượt
            };
            _mockUsageRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SubscriptionQuotaUsage, bool>>>()))
                .ReturnsAsync(new List<SubscriptionQuotaUsage> { usage });

            var service = new SubscriptionQuotaService(_mockUnitOfWork.Object, userSubService.Object);

            var ex = await Assert.ThrowsAsync<BadRequestException>(() => service.CheckAndConsumeOcrScanAsync(userId));
            ex.ErrorCode.Should().Be("OCR_QUOTA_EXCEEDED");
        }

        [Fact]
        public async Task CheckAndConsumeOcrScanAsync_WhenBatchExceedsRemaining_ShouldThrowBadRequestException()
        {
            var userId = Guid.NewGuid();
            var sub = new UserSubscription
            {
                UserId = userId,
                SnapshotMaxOcrScansPerMonth = 5
            };

            var userSubService = new Mock<IUserSubscriptionService>();
            userSubService.Setup(s => s.GetOrCreateActiveSubscriptionEntityAsync(userId)).ReturnsAsync(sub);

            var usage = new SubscriptionQuotaUsage
            {
                UserId = userId,
                PeriodMonth = DateTime.UtcNow.ToString("yyyy-MM"),
                OcrScansUsed = 4 // Còn 1 lượt
            };
            _mockUsageRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SubscriptionQuotaUsage, bool>>>()))
                .ReturnsAsync(new List<SubscriptionQuotaUsage> { usage });

            var service = new SubscriptionQuotaService(_mockUnitOfWork.Object, userSubService.Object);

            // Upload 2 files -> vượt quá 1 lượt còn lại
            var ex = await Assert.ThrowsAsync<BadRequestException>(() => service.CheckAndConsumeOcrScanAsync(userId, 2));
            ex.ErrorCode.Should().Be("OCR_QUOTA_EXCEEDED");
        }

        [Fact]
        public async Task CheckAndConsumeOcrScanAsync_WhenValid_IncrementsOcrScansUsed()
        {
            var userId = Guid.NewGuid();
            var sub = new UserSubscription
            {
                UserId = userId,
                SnapshotMaxOcrScansPerMonth = 5
            };

            var userSubService = new Mock<IUserSubscriptionService>();
            userSubService.Setup(s => s.GetOrCreateActiveSubscriptionEntityAsync(userId)).ReturnsAsync(sub);

            var usage = new SubscriptionQuotaUsage
            {
                UserId = userId,
                PeriodMonth = DateTime.UtcNow.ToString("yyyy-MM"),
                OcrScansUsed = 2
            };
            _mockUsageRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SubscriptionQuotaUsage, bool>>>()))
                .ReturnsAsync(new List<SubscriptionQuotaUsage> { usage });

            var service = new SubscriptionQuotaService(_mockUnitOfWork.Object, userSubService.Object);

            await service.CheckAndConsumeOcrScanAsync(userId, 2);

            usage.OcrScansUsed.Should().Be(4);
            _mockUsageRepo.Verify(r => r.Update(usage), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task CheckAndConsumeOcrScanAsync_WhenUnlimited_StillIncrementsUsageAccurately()
        {
            var userId = Guid.NewGuid();
            var sub = new UserSubscription
            {
                UserId = userId,
                SnapshotMaxOcrScansPerMonth = -1 // Không giới hạn
            };

            var userSubService = new Mock<IUserSubscriptionService>();
            userSubService.Setup(s => s.GetOrCreateActiveSubscriptionEntityAsync(userId)).ReturnsAsync(sub);

            var usage = new SubscriptionQuotaUsage
            {
                UserId = userId,
                PeriodMonth = DateTime.UtcNow.ToString("yyyy-MM"),
                OcrScansUsed = 10
            };
            _mockUsageRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SubscriptionQuotaUsage, bool>>>()))
                .ReturnsAsync(new List<SubscriptionQuotaUsage> { usage });

            var service = new SubscriptionQuotaService(_mockUnitOfWork.Object, userSubService.Object);

            await service.CheckAndConsumeOcrScanAsync(userId, 3);

            usage.OcrScansUsed.Should().Be(13);
            _mockUsageRepo.Verify(r => r.Update(usage), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }
    }
}

