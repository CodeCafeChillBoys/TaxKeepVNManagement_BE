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
        private readonly Mock<IGenericRepository<IncomeSource>> _mockIncomeRepo;
        private readonly Mock<IGenericRepository<Dependent>> _mockDepRepo;
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
            _mockIncomeRepo = new Mock<IGenericRepository<IncomeSource>>();
            _mockDepRepo = new Mock<IGenericRepository<Dependent>>();
            _mockConfig = new Mock<IConfiguration>();
            _mockLogger = new Mock<ILogger<UserSubscriptionService>>();

            _mockUnitOfWork.Setup(u => u.Repository<SubscriptionPlan>()).Returns(_mockPlanRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<UserSubscription>()).Returns(_mockSubRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<SubscriptionQuotaUsage>()).Returns(_mockUsageRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<UserWallet>()).Returns(_mockWalletRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<WalletTransaction>()).Returns(_mockTxnRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<IncomeSource>()).Returns(_mockIncomeRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(_mockDepRepo.Object);
        }

        // ══════════════════════════════════════════════════════════════════════
        // 1. SubscriptionPlanService Tests
        // ══════════════════════════════════════════════════════════════════════
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
        public async Task DeletePlanAsync_WhenDeletingBasicPlan_ShouldThrowBadRequestException()
        {
            var basicPlan = new SubscriptionPlan { Id = Guid.NewGuid(), Slug = "basic", Name = "Gói Basic" };
            _mockPlanRepo.Setup(r => r.GetByIdAsync(basicPlan.Id)).ReturnsAsync(basicPlan);

            var service = new SubscriptionPlanService(_mockUnitOfWork.Object);

            await Assert.ThrowsAsync<BadRequestException>(() => service.DeletePlanAsync(basicPlan.Id));
        }

        // ══════════════════════════════════════════════════════════════════════
        // 2. UserSubscriptionService Tests
        // ══════════════════════════════════════════════════════════════════════
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

        // ══════════════════════════════════════════════════════════════════════
        // 3. SubscriptionQuotaService Tests
        // ══════════════════════════════════════════════════════════════════════
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
        public async Task CheckCanAddIncomeSourceAsync_WhenLimitExceeded_ShouldThrowBadRequestException()
        {
            var userId = Guid.NewGuid();
            var sub = new UserSubscription
            {
                UserId = userId,
                SnapshotMaxIncomeSources = 1 // Basic chỉ cho phép 1
            };

            var userSubService = new Mock<IUserSubscriptionService>();
            userSubService.Setup(s => s.GetOrCreateActiveSubscriptionEntityAsync(userId)).ReturnsAsync(sub);

            var service = new SubscriptionQuotaService(_mockUnitOfWork.Object, userSubService.Object);

            // Đang có 1 nguồn, cố gắng thêm nguồn thứ 2
            await Assert.ThrowsAsync<BadRequestException>(() => service.CheckCanAddIncomeSourceAsync(userId, 1));
        }
    }
}

