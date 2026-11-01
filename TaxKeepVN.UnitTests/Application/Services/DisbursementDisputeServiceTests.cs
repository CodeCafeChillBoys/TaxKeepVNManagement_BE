using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Disputes;
using TaxKeepVN.Application.DTOs.Wallets;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Implementations;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Domain.IRepositories;
using Xunit;

namespace TaxKeepVN.UnitTests.Application.Services
{
    public class DisbursementDisputeServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<ILogger<DisbursementDisputeService>> _mockLogger;
        private readonly Mock<IGenericRepository<Booking>> _mockBookingRepo;
        private readonly Mock<IGenericRepository<BookingDispute>> _mockDisputeRepo;
        private readonly Mock<IGenericRepository<UserWallet>> _mockWalletRepo;
        private readonly DisbursementDisputeService _service;

        public DisbursementDisputeServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockLogger = new Mock<ILogger<DisbursementDisputeService>>();
            _mockBookingRepo = new Mock<IGenericRepository<Booking>>();
            _mockDisputeRepo = new Mock<IGenericRepository<BookingDispute>>();
            _mockWalletRepo = new Mock<IGenericRepository<UserWallet>>();

            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(_mockBookingRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<BookingDispute>()).Returns(_mockDisputeRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<UserWallet>()).Returns(_mockWalletRepo.Object);

            _service = new DisbursementDisputeService(_mockUnitOfWork.Object, _mockLogger.Object);
        }

        #region ConfirmSatisfactionAsync

        [Fact]
        public async Task UTCID01_ConfirmSatisfactionAsync_BookingNotFound_ThrowsNotFoundException()
        {
            _mockBookingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                            .ReturnsAsync((Booking?)null);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.ConfirmSatisfactionAsync(Guid.NewGuid(), Guid.NewGuid()));
        }

        [Fact]
        public async Task UTCID02_ConfirmSatisfactionAsync_ForbiddenUser_ThrowsForbiddenException()
        {
            var userId = Guid.NewGuid();
            var anotherUserId = Guid.NewGuid();
            var booking = new Booking { Id = Guid.NewGuid(), UserId = anotherUserId };

            _mockBookingRepo.Setup(r => r.GetByIdAsync(booking.Id)).ReturnsAsync(booking);

            var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
                _service.ConfirmSatisfactionAsync(userId, booking.Id));
            ex.ErrorCode.Should().Be("FORBIDDEN");
        }

        [Fact]
        public async Task UTCID03_ConfirmSatisfactionAsync_InvalidStatus_ThrowsBadRequestException()
        {
            var userId = Guid.NewGuid();
            var booking = new Booking { Id = Guid.NewGuid(), UserId = userId, Status = BookingStatus.IN_PROGRESS };

            _mockBookingRepo.Setup(r => r.GetByIdAsync(booking.Id)).ReturnsAsync(booking);

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.ConfirmSatisfactionAsync(userId, booking.Id));
            ex.ErrorCode.Should().Be("INVALID_STATUS");
        }

        [Fact]
        public async Task UTCID05_ConfirmSatisfactionAsync_UnderDispute_ThrowsBadRequestException()
        {
            var userId = Guid.NewGuid();
            var bookingId = Guid.NewGuid();
            var booking = new Booking { Id = bookingId, UserId = userId, Status = BookingStatus.COMPLETED };

            _mockBookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);

            var dispute = new BookingDispute { BookingId = bookingId, Status = "PENDING_EXPERT_RESPONSE" };
            _mockDisputeRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<BookingDispute, bool>>>()))
                            .ReturnsAsync(new List<BookingDispute> { dispute });

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.ConfirmSatisfactionAsync(userId, bookingId));
            ex.ErrorCode.Should().Be("UNDER_DISPUTE");
        }

        [Fact]
        public async Task UTCID06_ConfirmSatisfactionAsync_Success_DisbursesToExpertAndPlatform()
        {
            var userId = Guid.NewGuid();
            var expertUserId = Guid.NewGuid();
            var bookingId = Guid.NewGuid();
            var slotId = Guid.NewGuid();
            var profileId = Guid.NewGuid();

            var booking = new Booking
            {
                Id = bookingId,
                UserId = userId,
                Status = BookingStatus.COMPLETED,
                ExpertSlotId = slotId
            };
            _mockBookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockDisputeRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<BookingDispute, bool>>>()))
                            .ReturnsAsync(new List<BookingDispute>());

            var escrow = new EscrowTransaction
            {
                BookingId = bookingId,
                EscrowStatus = EscrowStatus.CoolingDown,
                Amount = 500000m,
                ExpertReceivable = 400000m,
                CommissionAmount = 100000m
            };
            var mockEscrowRepo = new Mock<IGenericRepository<EscrowTransaction>>();
            mockEscrowRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<EscrowTransaction, bool>>>()))
                          .ReturnsAsync(new List<EscrowTransaction> { escrow });
            _mockUnitOfWork.Setup(u => u.Repository<EscrowTransaction>()).Returns(mockEscrowRepo.Object);

            var mockSlotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            mockSlotRepo.Setup(r => r.GetByIdAsync(slotId))
                        .ReturnsAsync(new ExpertSlot { Id = slotId, ExpertProfileId = profileId });
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(mockSlotRepo.Object);

            var mockProfileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            mockProfileRepo.Setup(r => r.GetByIdAsync(profileId))
                           .ReturnsAsync(new ExpertProfile { Id = profileId, UserId = expertUserId });
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(mockProfileRepo.Object);

            var expertWallet = new UserWallet { Id = Guid.NewGuid(), UserId = expertUserId, WalletType = "EXPERT", AvailableBalance = 0 };
            var platformWallet = new UserWallet { Id = Guid.NewGuid(), WalletType = "PLATFORM", AvailableBalance = 0 };

            _mockWalletRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserWallet, bool>>>()))
                           .ReturnsAsync((Expression<Func<UserWallet, bool>> predicate) =>
                           {
                               var func = predicate.Compile();
                               var list = new List<UserWallet> { expertWallet, platformWallet };
                               return list.Where(func).ToList();
                           });

            var mockPolicyRepo = new Mock<IGenericRepository<ConsultationPlatformPolicy>>();
            mockPolicyRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationPlatformPolicy, bool>>>()))
                          .ReturnsAsync(new List<ConsultationPlatformPolicy> { new ConsultationPlatformPolicy { Id = 1, IsActive = true, PlatformCommissionRatePercent = 20 } });
            _mockUnitOfWork.Setup(u => u.Repository<ConsultationPlatformPolicy>()).Returns(mockPolicyRepo.Object);

            var mockTxRepo = new Mock<IGenericRepository<WalletTransaction>>();
            _mockUnitOfWork.Setup(u => u.Repository<WalletTransaction>()).Returns(mockTxRepo.Object);

            var mockNotifRepo = new Mock<IGenericRepository<SystemNotification>>();
            _mockUnitOfWork.Setup(u => u.Repository<SystemNotification>()).Returns(mockNotifRepo.Object);

            var mockUserRepo = new Mock<IGenericRepository<User>>();
            mockUserRepo.Setup(r => r.GetByIdAsync(expertUserId)).ReturnsAsync(new User { UserId = expertUserId, FullName = "Mr Expert", UserRole = "expert" });
            mockUserRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(new User { UserId = userId, FullName = "Taxpayer", UserRole = "user" });
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(mockUserRepo.Object);

            await _service.ConfirmSatisfactionAsync(userId, bookingId);

            escrow.EscrowStatus.Should().Be(EscrowStatus.Settled);
            booking.Status.Should().Be(BookingStatus.SETTLED);
            expertWallet.AvailableBalance.Should().Be(400000m);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.AtLeastOnce());
        }

        #endregion

        #region ProcessCoolDownDisbursementAsync

        [Fact]
        public async Task UTCID07_ProcessCoolDownDisbursementAsync_CooldownExpired_DisbursesSuccessfully()
        {
            var userId = Guid.NewGuid();
            var expertUserId = Guid.NewGuid();
            var bookingId = Guid.NewGuid();
            var slotId = Guid.NewGuid();
            var profileId = Guid.NewGuid();

            var booking = new Booking
            {
                Id = bookingId,
                UserId = userId,
                Status = BookingStatus.COMPLETED,
                ExpertSlotId = slotId,
                CompletedAt = DateTimeOffset.UtcNow.AddHours(-30)
            };
            _mockBookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockDisputeRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<BookingDispute, bool>>>()))
                            .ReturnsAsync(new List<BookingDispute>());

            var escrow = new EscrowTransaction
            {
                BookingId = bookingId,
                EscrowStatus = EscrowStatus.CoolingDown,
                Amount = 500000m,
                ExpertReceivable = 400000m,
                CommissionAmount = 100000m
            };
            var mockEscrowRepo = new Mock<IGenericRepository<EscrowTransaction>>();
            mockEscrowRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<EscrowTransaction, bool>>>()))
                          .ReturnsAsync(new List<EscrowTransaction> { escrow });
            _mockUnitOfWork.Setup(u => u.Repository<EscrowTransaction>()).Returns(mockEscrowRepo.Object);

            var mockSlotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            mockSlotRepo.Setup(r => r.GetByIdAsync(slotId))
                        .ReturnsAsync(new ExpertSlot { Id = slotId, ExpertProfileId = profileId });
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(mockSlotRepo.Object);

            var mockProfileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            mockProfileRepo.Setup(r => r.GetByIdAsync(profileId))
                           .ReturnsAsync(new ExpertProfile { Id = profileId, UserId = expertUserId });
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(mockProfileRepo.Object);

            var expertWallet = new UserWallet { Id = Guid.NewGuid(), UserId = expertUserId, WalletType = "EXPERT", AvailableBalance = 0 };
            var platformWallet = new UserWallet { Id = Guid.NewGuid(), WalletType = "PLATFORM", AvailableBalance = 0 };

            _mockWalletRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserWallet, bool>>>()))
                           .ReturnsAsync(new List<UserWallet> { expertWallet, platformWallet });

            var mockPolicyRepo = new Mock<IGenericRepository<ConsultationPlatformPolicy>>();
            mockPolicyRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationPlatformPolicy, bool>>>()))
                          .ReturnsAsync(new List<ConsultationPlatformPolicy> { new ConsultationPlatformPolicy { Id = 1, IsActive = true, EscrowCooldownHours = 24 } });
            _mockUnitOfWork.Setup(u => u.Repository<ConsultationPlatformPolicy>()).Returns(mockPolicyRepo.Object);

            var mockTxRepo = new Mock<IGenericRepository<WalletTransaction>>();
            _mockUnitOfWork.Setup(u => u.Repository<WalletTransaction>()).Returns(mockTxRepo.Object);

            var mockNotifRepo = new Mock<IGenericRepository<SystemNotification>>();
            _mockUnitOfWork.Setup(u => u.Repository<SystemNotification>()).Returns(mockNotifRepo.Object);

            var mockUserRepo = new Mock<IGenericRepository<User>>();
            mockUserRepo.Setup(r => r.GetByIdAsync(expertUserId)).ReturnsAsync(new User { UserId = expertUserId, FullName = "Mr Expert", UserRole = "expert" });
            mockUserRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(new User { UserId = userId, FullName = "Taxpayer", UserRole = "user" });
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(mockUserRepo.Object);

            var result = await _service.ProcessCoolDownDisbursementAsync(bookingId);

            result.Should().BeTrue();
            escrow.EscrowStatus.Should().Be(EscrowStatus.Settled);
        }

        [Fact]
        public async Task UTCID08_ProcessCoolDownDisbursementAsync_UnderDispute_ReturnsFalse()
        {
            var bookingId = Guid.NewGuid();
            var booking = new Booking { Id = bookingId, Status = BookingStatus.COMPLETED };
            _mockBookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);

            var dispute = new BookingDispute { BookingId = bookingId, Status = "PENDING_EXPERT_RESPONSE" };
            _mockDisputeRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<BookingDispute, bool>>>()))
                            .ReturnsAsync(new List<BookingDispute> { dispute });

            var result = await _service.ProcessCoolDownDisbursementAsync(bookingId);

            result.Should().BeFalse();
        }

        #endregion

        #region Disputes

        [Fact]
        public async Task UTCID09_CheckDisputeEligibilityAsync_CompletedWithinWindow_ReturnsCanDisputeTrue()
        {
            var userId = Guid.NewGuid();
            var bookingId = Guid.NewGuid();
            var booking = new Booking
            {
                Id = bookingId,
                UserId = userId,
                Status = BookingStatus.COMPLETED,
                CompletedAt = DateTimeOffset.UtcNow.AddHours(-2)
            };
            _mockBookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockDisputeRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<BookingDispute, bool>>>()))
                            .ReturnsAsync(new List<BookingDispute>());

            var mockPolicyRepo = new Mock<IGenericRepository<ConsultationPlatformPolicy>>();
            mockPolicyRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationPlatformPolicy, bool>>>()))
                          .ReturnsAsync(new List<ConsultationPlatformPolicy> { new ConsultationPlatformPolicy { DisputeWindowHours = 24 } });
            _mockUnitOfWork.Setup(u => u.Repository<ConsultationPlatformPolicy>()).Returns(mockPolicyRepo.Object);

            var result = await _service.CheckDisputeEligibilityAsync(userId, bookingId);

            result.Should().NotBeNull();
            result.CanDispute.Should().BeTrue();
        }

        [Fact]
        public async Task UTCID10_SubmitDisputeAsync_Valid_CreatesDisputeAndFreezesEscrow()
        {
            var userId = Guid.NewGuid();
            var expertUserId = Guid.NewGuid();
            var bookingId = Guid.NewGuid();
            var slotId = Guid.NewGuid();
            var profileId = Guid.NewGuid();

            var booking = new Booking
            {
                Id = bookingId,
                UserId = userId,
                BookingCode = "BK123",
                Status = BookingStatus.COMPLETED,
                ExpertSlotId = slotId,
                CompletedAt = DateTimeOffset.UtcNow.AddHours(-2)
            };
            _mockBookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockDisputeRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<BookingDispute, bool>>>()))
                            .ReturnsAsync(new List<BookingDispute>());

            var mockPolicyRepo = new Mock<IGenericRepository<ConsultationPlatformPolicy>>();
            mockPolicyRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationPlatformPolicy, bool>>>()))
                          .ReturnsAsync(new List<ConsultationPlatformPolicy> { new ConsultationPlatformPolicy { DisputeWindowHours = 24, DisputeExpertReplyHours = 24 } });
            _mockUnitOfWork.Setup(u => u.Repository<ConsultationPlatformPolicy>()).Returns(mockPolicyRepo.Object);

            var mockSlotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            mockSlotRepo.Setup(r => r.GetByIdAsync(slotId))
                        .ReturnsAsync(new ExpertSlot { Id = slotId, ExpertProfileId = profileId });
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(mockSlotRepo.Object);

            var mockProfileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            mockProfileRepo.Setup(r => r.GetByIdAsync(profileId))
                           .ReturnsAsync(new ExpertProfile { Id = profileId, UserId = expertUserId });
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(mockProfileRepo.Object);

            var escrow = new EscrowTransaction { BookingId = bookingId, EscrowStatus = EscrowStatus.CoolingDown, Amount = 500000 };
            var mockEscrowRepo = new Mock<IGenericRepository<EscrowTransaction>>();
            mockEscrowRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<EscrowTransaction, bool>>>()))
                          .ReturnsAsync(new List<EscrowTransaction> { escrow });
            _mockUnitOfWork.Setup(u => u.Repository<EscrowTransaction>()).Returns(mockEscrowRepo.Object);

            var mockNotifRepo = new Mock<IGenericRepository<SystemNotification>>();
            _mockUnitOfWork.Setup(u => u.Repository<SystemNotification>()).Returns(mockNotifRepo.Object);

            var mockUserRepo = new Mock<IGenericRepository<User>>();
            mockUserRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(new User { UserId = userId, FullName = "Taxpayer" });
            mockUserRepo.Setup(r => r.GetByIdAsync(expertUserId)).ReturnsAsync(new User { UserId = expertUserId, FullName = "Mr Expert" });
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(mockUserRepo.Object);

            var request = new CreateDisputeRequest
            {
                Reason = "EXPERT_ABSENT",
                Description = "Chuyen gia khong xuat hien trong buoi hen hop le"
            };

            var result = await _service.SubmitDisputeAsync(userId, bookingId, request);

            result.Should().NotBeNull();
            escrow.EscrowStatus.Should().Be(EscrowStatus.Disputed);
            booking.Status.Should().Be(BookingStatus.UNDER_DISPUTE);
            _mockDisputeRepo.Verify(r => r.AddAsync(It.IsAny<BookingDispute>()), Times.Once);
        }

        [Fact]
        public async Task UTCID11_ExpertRespondDisputeAsync_Valid_UpdatesDisputeResponse()
        {
            var expertUserId = Guid.NewGuid();
            var disputeId = Guid.NewGuid();
            var bookingId = Guid.NewGuid();

            var dispute = new BookingDispute
            {
                Id = disputeId,
                BookingId = bookingId,
                ExpertUserId = expertUserId,
                Status = "PENDING_EXPERT_RESPONSE"
            };
            _mockDisputeRepo.Setup(r => r.GetByIdAsync(disputeId)).ReturnsAsync(dispute);

            var booking = new Booking { Id = bookingId, BookingCode = "BK123" };
            _mockBookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);

            var mockUserRepo = new Mock<IGenericRepository<User>>();
            mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                        .ReturnsAsync(new List<User>());
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(mockUserRepo.Object);

            var request = new ExpertRespondDisputeRequest
            {
                Response = "Toi co mat dung gio tai Google Meet",
                EvidenceUrls = new List<string> { "https://evidence.com/shot.png" }
            };

            var result = await _service.ExpertRespondDisputeAsync(expertUserId, disputeId, request);

            result.Should().NotBeNull();
            dispute.Status.Should().Be("AWAITING_ADMIN_RESOLUTION");
            _mockDisputeRepo.Verify(r => r.Update(dispute), Times.Once);
        }

        [Fact]
        public async Task UTCID12_AdminResolveDisputeAsync_FullRefund_RefundsUser()
        {
            var adminId = Guid.NewGuid();
            var disputeId = Guid.NewGuid();
            var bookingId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var expertUserId = Guid.NewGuid();

            var dispute = new BookingDispute
            {
                Id = disputeId,
                BookingId = bookingId,
                UserId = userId,
                ExpertUserId = expertUserId,
                Status = "AWAITING_ADMIN_RESOLUTION"
            };
            _mockDisputeRepo.Setup(r => r.GetByIdAsync(disputeId)).ReturnsAsync(dispute);

            var booking = new Booking { Id = bookingId, UserId = userId, BookingCode = "BK100", Status = BookingStatus.UNDER_DISPUTE };
            _mockBookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);

            var escrow = new EscrowTransaction { BookingId = bookingId, EscrowStatus = EscrowStatus.Disputed, Amount = 500000m };
            var mockEscrowRepo = new Mock<IGenericRepository<EscrowTransaction>>();
            mockEscrowRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<EscrowTransaction, bool>>>()))
                          .ReturnsAsync(new List<EscrowTransaction> { escrow });
            _mockUnitOfWork.Setup(u => u.Repository<EscrowTransaction>()).Returns(mockEscrowRepo.Object);

            var mockPolicyRepo = new Mock<IGenericRepository<ConsultationPlatformPolicy>>();
            mockPolicyRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationPlatformPolicy, bool>>>()))
                          .ReturnsAsync(new List<ConsultationPlatformPolicy> { new ConsultationPlatformPolicy { PlatformCommissionRatePercent = 20 } });
            _mockUnitOfWork.Setup(u => u.Repository<ConsultationPlatformPolicy>()).Returns(mockPolicyRepo.Object);

            var userWallet = new UserWallet { Id = Guid.NewGuid(), UserId = userId, WalletType = "USER", AvailableBalance = 0 };
            _mockWalletRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserWallet, bool>>>()))
                           .ReturnsAsync(new List<UserWallet> { userWallet });

            var mockTxRepo = new Mock<IGenericRepository<WalletTransaction>>();
            _mockUnitOfWork.Setup(u => u.Repository<WalletTransaction>()).Returns(mockTxRepo.Object);

            var mockNotifRepo = new Mock<IGenericRepository<SystemNotification>>();
            _mockUnitOfWork.Setup(u => u.Repository<SystemNotification>()).Returns(mockNotifRepo.Object);

            var mockUserRepo = new Mock<IGenericRepository<User>>();
            mockUserRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(new User { UserId = userId, FullName = "Taxpayer" });
            mockUserRepo.Setup(r => r.GetByIdAsync(expertUserId)).ReturnsAsync(new User { UserId = expertUserId, FullName = "Mr Expert" });
            mockUserRepo.Setup(r => r.GetByIdAsync(adminId)).ReturnsAsync(new User { UserId = adminId, FullName = "Admin User" });
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(mockUserRepo.Object);

            var request = new AdminResolveDisputeRequest
            {
                DecisionType = "FULL_REFUND",
                Resolution = "Khach hang duoc hoan tien 100% do chuyen gia vang mat"
            };

            var result = await _service.AdminResolveDisputeAsync(adminId, disputeId, request);

            result.Should().NotBeNull();
            escrow.EscrowStatus.Should().Be(EscrowStatus.Refunded);
            userWallet.AvailableBalance.Should().Be(500000m);
        }

        [Fact]
        public async Task UTCID13_AdminResolveDisputeAsync_SettleExpert_SettlesToExpert()
        {
            var adminId = Guid.NewGuid();
            var disputeId = Guid.NewGuid();
            var bookingId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var expertUserId = Guid.NewGuid();

            var dispute = new BookingDispute
            {
                Id = disputeId,
                BookingId = bookingId,
                UserId = userId,
                ExpertUserId = expertUserId,
                Status = "AWAITING_ADMIN_RESOLUTION"
            };
            _mockDisputeRepo.Setup(r => r.GetByIdAsync(disputeId)).ReturnsAsync(dispute);

            var booking = new Booking { Id = bookingId, UserId = userId, BookingCode = "BK100", Status = BookingStatus.UNDER_DISPUTE };
            _mockBookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);

            var escrow = new EscrowTransaction { BookingId = bookingId, EscrowStatus = EscrowStatus.Disputed, Amount = 500000m };
            var mockEscrowRepo = new Mock<IGenericRepository<EscrowTransaction>>();
            mockEscrowRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<EscrowTransaction, bool>>>()))
                          .ReturnsAsync(new List<EscrowTransaction> { escrow });
            _mockUnitOfWork.Setup(u => u.Repository<EscrowTransaction>()).Returns(mockEscrowRepo.Object);

            var mockPolicyRepo = new Mock<IGenericRepository<ConsultationPlatformPolicy>>();
            mockPolicyRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationPlatformPolicy, bool>>>()))
                          .ReturnsAsync(new List<ConsultationPlatformPolicy> { new ConsultationPlatformPolicy { PlatformCommissionRatePercent = 20 } });
            _mockUnitOfWork.Setup(u => u.Repository<ConsultationPlatformPolicy>()).Returns(mockPolicyRepo.Object);

            var expertWallet = new UserWallet { Id = Guid.NewGuid(), UserId = expertUserId, WalletType = "EXPERT", AvailableBalance = 0 };
            var platformWallet = new UserWallet { Id = Guid.NewGuid(), WalletType = "PLATFORM", AvailableBalance = 0 };
            _mockWalletRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserWallet, bool>>>()))
                           .ReturnsAsync((Expression<Func<UserWallet, bool>> predicate) =>
                           {
                               var func = predicate.Compile();
                               return new List<UserWallet> { expertWallet, platformWallet }.Where(func).ToList();
                           });

            var mockTxRepo = new Mock<IGenericRepository<WalletTransaction>>();
            _mockUnitOfWork.Setup(u => u.Repository<WalletTransaction>()).Returns(mockTxRepo.Object);

            var mockNotifRepo = new Mock<IGenericRepository<SystemNotification>>();
            _mockUnitOfWork.Setup(u => u.Repository<SystemNotification>()).Returns(mockNotifRepo.Object);

            var mockUserRepo = new Mock<IGenericRepository<User>>();
            mockUserRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(new User { UserId = userId, FullName = "Taxpayer", UserRole = "user" });
            mockUserRepo.Setup(r => r.GetByIdAsync(expertUserId)).ReturnsAsync(new User { UserId = expertUserId, FullName = "Mr Expert", UserRole = "expert" });
            mockUserRepo.Setup(r => r.GetByIdAsync(adminId)).ReturnsAsync(new User { UserId = adminId, FullName = "Admin User", UserRole = "admin" });
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(mockUserRepo.Object);

            var request = new AdminResolveDisputeRequest
            {
                DecisionType = "SETTLE_EXPERT",
                Resolution = "Bac bo khieu nai, giai ngan cho chuyen gia"
            };

            var result = await _service.AdminResolveDisputeAsync(adminId, disputeId, request);

            result.Should().NotBeNull();
            escrow.EscrowStatus.Should().Be(EscrowStatus.Settled);
            expertWallet.AvailableBalance.Should().Be(400000m); // 500k - 20%
        }

        #endregion

        #region Withdrawals

        [Fact]
        public async Task UTCID14_RequestWithdrawalAsync_NotVerified_ThrowsBadRequestException()
        {
            var expertUserId = Guid.NewGuid();
            var user = new User { UserId = expertUserId, IsVerified = false };
            var mockUserRepo = new Mock<IGenericRepository<User>>();
            mockUserRepo.Setup(r => r.GetByIdAsync(expertUserId)).ReturnsAsync(user);
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(mockUserRepo.Object);

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.RequestWithdrawalAsync(expertUserId, new CreateWithdrawalRequest { Amount = 1000000 }));
            ex.ErrorCode.Should().Be("EKYC_REQUIRED");
        }

        [Fact]
        public async Task UTCID15_RequestWithdrawalAsync_Valid_CreatesPendingWithdrawal()
        {
            var expertUserId = Guid.NewGuid();
            var user = new User { UserId = expertUserId, IsVerified = true, TaxIdNumber = "0109998888", FullName = "Expert A" };
            var mockUserRepo = new Mock<IGenericRepository<User>>();
            mockUserRepo.Setup(r => r.GetByIdAsync(expertUserId)).ReturnsAsync(user);
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(mockUserRepo.Object);

            var mockPolicyRepo = new Mock<IGenericRepository<ConsultationPlatformPolicy>>();
            mockPolicyRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationPlatformPolicy, bool>>>()))
                          .ReturnsAsync(new List<ConsultationPlatformPolicy> { new ConsultationPlatformPolicy { ExpertMinWithdrawalVnd = 500000m } });
            _mockUnitOfWork.Setup(u => u.Repository<ConsultationPlatformPolicy>()).Returns(mockPolicyRepo.Object);

            var wallet = new UserWallet { Id = Guid.NewGuid(), UserId = expertUserId, WalletType = "EXPERT", AvailableBalance = 1000000m };
            _mockWalletRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserWallet, bool>>>()))
                           .ReturnsAsync(new List<UserWallet> { wallet });

            var mockWithdrawalRepo = new Mock<IGenericRepository<WithdrawalRequest>>();
            _mockUnitOfWork.Setup(u => u.Repository<WithdrawalRequest>()).Returns(mockWithdrawalRepo.Object);

            var mockTxRepo = new Mock<IGenericRepository<WalletTransaction>>();
            _mockUnitOfWork.Setup(u => u.Repository<WalletTransaction>()).Returns(mockTxRepo.Object);

            var mockNotifRepo = new Mock<IGenericRepository<SystemNotification>>();
            _mockUnitOfWork.Setup(u => u.Repository<SystemNotification>()).Returns(mockNotifRepo.Object);

            var request = new CreateWithdrawalRequest
            {
                Amount = 600000m,
                BankName = "MBBank",
                AccountNumber = "0987654321",
                AccountName = "NGUYEN VAN EXPERT"
            };

            var result = await _service.RequestWithdrawalAsync(expertUserId, request);

            result.Should().NotBeNull();
            result.Amount.Should().Be(600000m);
            result.Status.Should().Be("PENDING");
            wallet.AvailableBalance.Should().Be(400000m);
            wallet.FrozenBalance.Should().Be(600000m);
        }

        [Fact]
        public async Task UTCID16_AdminProcessWithdrawalAsync_Approved_CompletesWithdrawal()
        {
            var adminId = Guid.NewGuid();
            var withdrawalId = Guid.NewGuid();
            var expertUserId = Guid.NewGuid();

            var withdrawal = new WithdrawalRequest
            {
                Id = withdrawalId,
                UserId = expertUserId,
                Amount = 500000m,
                Status = "PENDING",
                BankName = "MBBANK",
                AccountNumber = "0123456789"
            };
            var mockWithdrawalRepo = new Mock<IGenericRepository<WithdrawalRequest>>();
            mockWithdrawalRepo.Setup(r => r.GetByIdAsync(withdrawalId)).ReturnsAsync(withdrawal);
            _mockUnitOfWork.Setup(u => u.Repository<WithdrawalRequest>()).Returns(mockWithdrawalRepo.Object);

            var user = new User { UserId = expertUserId, UserRole = "expert", FullName = "Expert B" };
            var mockUserRepo = new Mock<IGenericRepository<User>>();
            mockUserRepo.Setup(r => r.GetByIdAsync(expertUserId)).ReturnsAsync(user);
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(mockUserRepo.Object);

            var wallet = new UserWallet { Id = Guid.NewGuid(), UserId = expertUserId, WalletType = "EXPERT", FrozenBalance = 500000m };
            _mockWalletRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserWallet, bool>>>()))
                           .ReturnsAsync(new List<UserWallet> { wallet });

            var mockNotifRepo = new Mock<IGenericRepository<SystemNotification>>();
            _mockUnitOfWork.Setup(u => u.Repository<SystemNotification>()).Returns(mockNotifRepo.Object);

            var request = new AdminProcessWithdrawalRequest
            {
                Action = "APPROVE",
                TransactionReference = "BANK_REF_999",
                AdminNotes = "Chuyen khoan thanh cong"
            };

            var result = await _service.AdminProcessWithdrawalAsync(adminId, withdrawalId, request);

            result.Should().NotBeNull();
            result.Status.Should().Be("COMPLETED");
            withdrawal.Status.Should().Be("COMPLETED");
            wallet.FrozenBalance.Should().Be(0m);
            wallet.TotalWithdrawn.Should().Be(500000m);
        }

        #endregion

        #region GetMyWalletAsync

        [Fact]
        public async Task UTCID04_GetMyWalletAsync_NoExistingWallet_CreatesAndReturnsWallet()
        {
            var userId = Guid.NewGuid();
            var user = new User { UserId = userId, UserRole = "user" };
            var mockUserRepo = new Mock<IGenericRepository<User>>();
            mockUserRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(mockUserRepo.Object);

            var mockTxRepo = new Mock<IGenericRepository<WalletTransaction>>();
            mockTxRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<WalletTransaction, bool>>>()))
                      .ReturnsAsync(new List<WalletTransaction>());
            _mockUnitOfWork.Setup(u => u.Repository<WalletTransaction>()).Returns(mockTxRepo.Object);

            _mockWalletRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserWallet, bool>>>()))
                           .ReturnsAsync(new List<UserWallet>());

            var result = await _service.GetMyWalletAsync(userId, "personal");

            result.Should().NotBeNull();
            result.AvailableBalance.Should().Be(0);
            _mockWalletRepo.Verify(r => r.AddAsync(It.IsAny<UserWallet>()), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion

        #region Extended Dispute & Withdrawal Tests

        [Fact]
        public async Task UTCID17_AdminResolveDisputeAsync_Partial_SplitsUserAndExpert()
        {
            var adminId = Guid.NewGuid();
            var disputeId = Guid.NewGuid();
            var bookingId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var expertUserId = Guid.NewGuid();

            var dispute = new BookingDispute
            {
                Id = disputeId,
                BookingId = bookingId,
                UserId = userId,
                ExpertUserId = expertUserId,
                Status = "AWAITING_ADMIN_RESOLUTION"
            };
            _mockDisputeRepo.Setup(r => r.GetByIdAsync(disputeId)).ReturnsAsync(dispute);

            var booking = new Booking { Id = bookingId, UserId = userId, BookingCode = "BK100", Status = BookingStatus.UNDER_DISPUTE };
            _mockBookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);

            var escrow = new EscrowTransaction { BookingId = bookingId, EscrowStatus = EscrowStatus.Disputed, Amount = 1000000m };
            var mockEscrowRepo = new Mock<IGenericRepository<EscrowTransaction>>();
            mockEscrowRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<EscrowTransaction, bool>>>()))
                          .ReturnsAsync(new List<EscrowTransaction> { escrow });
            _mockUnitOfWork.Setup(u => u.Repository<EscrowTransaction>()).Returns(mockEscrowRepo.Object);

            var mockPolicyRepo = new Mock<IGenericRepository<ConsultationPlatformPolicy>>();
            mockPolicyRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationPlatformPolicy, bool>>>()))
                          .ReturnsAsync(new List<ConsultationPlatformPolicy> { new ConsultationPlatformPolicy { PlatformCommissionRatePercent = 20 } });
            _mockUnitOfWork.Setup(u => u.Repository<ConsultationPlatformPolicy>()).Returns(mockPolicyRepo.Object);

            var userWallet = new UserWallet { Id = Guid.NewGuid(), UserId = userId, WalletType = "USER", AvailableBalance = 0 };
            var expertWallet = new UserWallet { Id = Guid.NewGuid(), UserId = expertUserId, WalletType = "EXPERT", AvailableBalance = 0 };
            var platformWallet = new UserWallet { Id = Guid.NewGuid(), WalletType = "PLATFORM", AvailableBalance = 0 };
            _mockWalletRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserWallet, bool>>>()))
                           .ReturnsAsync((Expression<Func<UserWallet, bool>> predicate) =>
                           {
                               var func = predicate.Compile();
                               return new List<UserWallet> { userWallet, expertWallet, platformWallet }.Where(func).ToList();
                           });

            var mockTxRepo = new Mock<IGenericRepository<WalletTransaction>>();
            _mockUnitOfWork.Setup(u => u.Repository<WalletTransaction>()).Returns(mockTxRepo.Object);

            var mockNotifRepo = new Mock<IGenericRepository<SystemNotification>>();
            _mockUnitOfWork.Setup(u => u.Repository<SystemNotification>()).Returns(mockNotifRepo.Object);

            var mockUserRepo = new Mock<IGenericRepository<User>>();
            mockUserRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(new User { UserId = userId, FullName = "Taxpayer", UserRole = "user" });
            mockUserRepo.Setup(r => r.GetByIdAsync(expertUserId)).ReturnsAsync(new User { UserId = expertUserId, FullName = "Mr Expert", UserRole = "expert" });
            mockUserRepo.Setup(r => r.GetByIdAsync(adminId)).ReturnsAsync(new User { UserId = adminId, FullName = "Admin User", UserRole = "admin" });
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(mockUserRepo.Object);

            var request = new AdminResolveDisputeRequest
            {
                DecisionType = "PARTIAL",
                RefundRatePercent = 50m,
                Resolution = "Phan chia 50-50"
            };

            var result = await _service.AdminResolveDisputeAsync(adminId, disputeId, request);

            result.Should().NotBeNull();
            escrow.EscrowStatus.Should().Be(EscrowStatus.PartiallyRefunded);
            userWallet.AvailableBalance.Should().Be(500000m);
            expertWallet.AvailableBalance.Should().Be(400000m);
        }

        [Fact]
        public async Task UTCID18_GetDisputeByIdAsync_Found_ReturnsDetail()
        {
            var disputeId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var bookingId = Guid.NewGuid();

            var dispute = new BookingDispute
            {
                Id = disputeId,
                BookingId = bookingId,
                UserId = userId,
                Status = "PENDING_EXPERT_RESPONSE",
                Reason = "OTHER",
                Description = "No dung khieu nai chi tiet"
            };
            _mockDisputeRepo.Setup(r => r.GetByIdAsync(disputeId)).ReturnsAsync(dispute);

            var booking = new Booking { Id = bookingId, BookingCode = "BK999", UserId = userId };
            _mockBookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);

            var mockUserRepo = new Mock<IGenericRepository<User>>();
            mockUserRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(new User { UserId = userId, FullName = "Taxpayer" });
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(mockUserRepo.Object);

            var result = await _service.GetDisputeByIdAsync(userId, "user", disputeId);

            result.Should().NotBeNull();
            result.Id.Should().Be(disputeId);
            result.BookingCode.Should().Be("BK999");
        }

        [Fact]
        public async Task UTCID19_GetDisputesAsync_Admin_ReturnsPagedResult()
        {
            var adminId = Guid.NewGuid();
            var list = new List<BookingDispute>
            {
                new BookingDispute { Id = Guid.NewGuid(), BookingId = Guid.NewGuid(), Status = "AWAITING_ADMIN_RESOLUTION", CreatedAt = DateTimeOffset.UtcNow }
            };
            _mockDisputeRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(list);

            var mockBookingRepo = new Mock<IGenericRepository<Booking>>();
            mockBookingRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Booking, bool>>>()))
                           .ReturnsAsync(new List<Booking>());
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(mockBookingRepo.Object);

            var mockUserRepo = new Mock<IGenericRepository<User>>();
            mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                        .ReturnsAsync(new List<User>());
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(mockUserRepo.Object);

            var result = await _service.GetDisputesAsync(adminId, "admin", page: 1, size: 10);

            result.Should().NotBeNull();
            result.Items.Should().HaveCount(1);
        }

        [Fact]
        public async Task UTCID20_AdminProcessWithdrawalAsync_Reject_RefundsFrozenBalance()
        {
            var adminId = Guid.NewGuid();
            var withdrawalId = Guid.NewGuid();
            var expertUserId = Guid.NewGuid();

            var withdrawal = new WithdrawalRequest
            {
                Id = withdrawalId,
                UserId = expertUserId,
                Amount = 500000m,
                Status = "PENDING",
                BankName = "MBBANK",
                AccountNumber = "0123456789"
            };
            var mockWithdrawalRepo = new Mock<IGenericRepository<WithdrawalRequest>>();
            mockWithdrawalRepo.Setup(r => r.GetByIdAsync(withdrawalId)).ReturnsAsync(withdrawal);
            _mockUnitOfWork.Setup(u => u.Repository<WithdrawalRequest>()).Returns(mockWithdrawalRepo.Object);

            var user = new User { UserId = expertUserId, UserRole = "expert", FullName = "Expert B" };
            var mockUserRepo = new Mock<IGenericRepository<User>>();
            mockUserRepo.Setup(r => r.GetByIdAsync(expertUserId)).ReturnsAsync(user);
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(mockUserRepo.Object);

            var wallet = new UserWallet { Id = Guid.NewGuid(), UserId = expertUserId, WalletType = "EXPERT", AvailableBalance = 200000m, FrozenBalance = 500000m };
            _mockWalletRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserWallet, bool>>>()))
                           .ReturnsAsync(new List<UserWallet> { wallet });

            var mockTxRepo = new Mock<IGenericRepository<WalletTransaction>>();
            _mockUnitOfWork.Setup(u => u.Repository<WalletTransaction>()).Returns(mockTxRepo.Object);

            var mockNotifRepo = new Mock<IGenericRepository<SystemNotification>>();
            _mockUnitOfWork.Setup(u => u.Repository<SystemNotification>()).Returns(mockNotifRepo.Object);

            var request = new AdminProcessWithdrawalRequest
            {
                Action = "REJECT",
                RejectionReason = "So tai khoan khong hop le",
                AdminNotes = "So tai khoan khong hop le"
            };

            var result = await _service.AdminProcessWithdrawalAsync(adminId, withdrawalId, request);

            result.Should().NotBeNull();
            result.Status.Should().Be("REJECTED");
            withdrawal.Status.Should().Be("REJECTED");
            wallet.FrozenBalance.Should().Be(0m);
            wallet.AvailableBalance.Should().Be(700000m);
        }

        [Fact]
        public async Task UTCID21_RequestUserRefundWithdrawalAsync_Valid_CreatesPendingUserWithdrawal()
        {
            var userId = Guid.NewGuid();
            var user = new User { UserId = userId, UserRole = "user", FullName = "Taxpayer User" };
            var mockUserRepo = new Mock<IGenericRepository<User>>();
            mockUserRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(mockUserRepo.Object);

            var wallet = new UserWallet { Id = Guid.NewGuid(), UserId = userId, WalletType = "USER", AvailableBalance = 200000m };
            _mockWalletRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserWallet, bool>>>()))
                           .ReturnsAsync(new List<UserWallet> { wallet });

            var mockWithdrawalRepo = new Mock<IGenericRepository<WithdrawalRequest>>();
            _mockUnitOfWork.Setup(u => u.Repository<WithdrawalRequest>()).Returns(mockWithdrawalRepo.Object);

            var mockTxRepo = new Mock<IGenericRepository<WalletTransaction>>();
            _mockUnitOfWork.Setup(u => u.Repository<WalletTransaction>()).Returns(mockTxRepo.Object);

            var mockNotifRepo = new Mock<IGenericRepository<SystemNotification>>();
            _mockUnitOfWork.Setup(u => u.Repository<SystemNotification>()).Returns(mockNotifRepo.Object);

            var request = new CreateWithdrawalRequest
            {
                Amount = 100000m,
                BankName = "Techcombank",
                AccountNumber = "1903333333",
                AccountName = "TAXPAYER USER"
            };

            var result = await _service.RequestUserRefundWithdrawalAsync(userId, request);

            result.Should().NotBeNull();
            result.Amount.Should().Be(100000m);
            result.Status.Should().Be("PENDING");
            wallet.AvailableBalance.Should().Be(100000m);
            wallet.FrozenBalance.Should().Be(100000m);
        }

        [Fact]
        public async Task UTCID22_GetWithdrawalsAsync_Admin_ReturnsPagedResult()
        {
            var adminId = Guid.NewGuid();
            var withdrawals = new List<WithdrawalRequest>
            {
                new WithdrawalRequest { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Amount = 500000m, Status = "PENDING", CreatedAt = DateTimeOffset.UtcNow }
            };
            var mockWithdrawalRepo = new Mock<IGenericRepository<WithdrawalRequest>>();
            mockWithdrawalRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(withdrawals);
            _mockUnitOfWork.Setup(u => u.Repository<WithdrawalRequest>()).Returns(mockWithdrawalRepo.Object);

            var mockUserRepo = new Mock<IGenericRepository<User>>();
            mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                        .ReturnsAsync(new List<User>());
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(mockUserRepo.Object);

            var result = await _service.GetWithdrawalsAsync(adminId, "admin", page: 1, size: 10);

            result.Should().NotBeNull();
            result.Items.Should().HaveCount(1);
        }

        [Fact]
        public async Task UTCID23_GetMyWalletTransactionsAsync_Valid_ReturnsPagedTransactions()
        {
            var userId = Guid.NewGuid();
            var user = new User { UserId = userId, UserRole = "user" };
            var mockUserRepo = new Mock<IGenericRepository<User>>();
            mockUserRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(mockUserRepo.Object);

            var txs = new List<WalletTransaction>
            {
                new WalletTransaction { Id = Guid.NewGuid(), UserId = userId, Amount = 100000m, TransactionType = "TOPUP", CreatedAt = DateTimeOffset.UtcNow }
            };
            var mockTxRepo = new Mock<IGenericRepository<WalletTransaction>>();
            mockTxRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<WalletTransaction, bool>>>()))
                      .ReturnsAsync(txs);
            _mockUnitOfWork.Setup(u => u.Repository<WalletTransaction>()).Returns(mockTxRepo.Object);

            var result = await _service.GetMyWalletTransactionsAsync(userId, page: 1, size: 10);

            result.Should().NotBeNull();
            result.Items.Should().HaveCount(1);
        }

        [Fact]
        public async Task UTCID24_GetDisputeByBookingIdAsync_Found_ReturnsDisputeDetail()
        {
            var bookingId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var dispute = new BookingDispute { BookingId = bookingId, UserId = userId, Status = "PENDING_EXPERT_RESPONSE" };
            _mockDisputeRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<BookingDispute, bool>>>()))
                            .ReturnsAsync(new List<BookingDispute> { dispute });

            var booking = new Booking { Id = bookingId, UserId = userId, BookingCode = "BK777" };
            _mockBookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);

            var mockUserRepo = new Mock<IGenericRepository<User>>();
            mockUserRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(new User { UserId = userId, FullName = "Taxpayer" });
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(mockUserRepo.Object);

            var result = await _service.GetDisputeByBookingIdAsync(userId, "user", bookingId);

            result.Should().NotBeNull();
            result!.BookingCode.Should().Be("BK777");
        }

        [Fact]
        public async Task UTCID25_RequestWithdrawalAsync_InsufficientBalance_ThrowsBadRequestException()
        {
            var expertUserId = Guid.NewGuid();
            var user = new User { UserId = expertUserId, IsVerified = true, TaxIdNumber = "0109998888", FullName = "Expert A" };
            var mockUserRepo = new Mock<IGenericRepository<User>>();
            mockUserRepo.Setup(r => r.GetByIdAsync(expertUserId)).ReturnsAsync(user);
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(mockUserRepo.Object);

            var mockPolicyRepo = new Mock<IGenericRepository<ConsultationPlatformPolicy>>();
            mockPolicyRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationPlatformPolicy, bool>>>()))
                          .ReturnsAsync(new List<ConsultationPlatformPolicy> { new ConsultationPlatformPolicy { ExpertMinWithdrawalVnd = 500000m } });
            _mockUnitOfWork.Setup(u => u.Repository<ConsultationPlatformPolicy>()).Returns(mockPolicyRepo.Object);

            var wallet = new UserWallet { Id = Guid.NewGuid(), UserId = expertUserId, WalletType = "EXPERT", AvailableBalance = 100000m };
            _mockWalletRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserWallet, bool>>>()))
                           .ReturnsAsync(new List<UserWallet> { wallet });

            var request = new CreateWithdrawalRequest
            {
                Amount = 600000m,
                BankName = "MBBank",
                AccountNumber = "0987654321",
                AccountName = "NGUYEN VAN EXPERT"
            };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.RequestWithdrawalAsync(expertUserId, request));
            ex.ErrorCode.Should().Be("INSUFFICIENT_BALANCE");
        }

        [Fact]
        public async Task UTCID26_GetExpertPendingEscrowDetailsAsync_NoProfile_ReturnsEmpty()
        {
            var expertUserId = Guid.NewGuid();
            var mockProfileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            mockProfileRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertProfile, bool>>>()))
                           .ReturnsAsync(new List<ExpertProfile>());
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(mockProfileRepo.Object);

            var result = await _service.GetExpertPendingEscrowDetailsAsync(expertUserId);

            result.Should().BeEmpty();
        }

        [Fact]
        public async Task UTCID27_GetMyWalletAsync_Expert_ReturnsPendingEscrowsAndTransactions()
        {
            var expertUserId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var slotId = Guid.NewGuid();
            var bookingId = Guid.NewGuid();
            var taxpayerId = Guid.NewGuid();
            var walletId = Guid.NewGuid();

            var expert = new User { UserId = expertUserId, UserRole = "expert", FullName = "Expert A" };
            var taxpayer = new User { UserId = taxpayerId, FullName = "Taxpayer B" };
            var mockUserRepo = new Mock<IGenericRepository<User>>();
            mockUserRepo.Setup(r => r.GetByIdAsync(expertUserId)).ReturnsAsync(expert);
            mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                        .ReturnsAsync(new List<User> { taxpayer });
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(mockUserRepo.Object);

            var mockProfileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            mockProfileRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertProfile, bool>>>()))
                           .ReturnsAsync(new List<ExpertProfile> { new ExpertProfile { Id = profileId, UserId = expertUserId } });
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(mockProfileRepo.Object);

            var mockSlotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            mockSlotRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertSlot, bool>>>()))
                        .ReturnsAsync(new List<ExpertSlot>
                        {
                            new ExpertSlot
                            {
                                Id = slotId,
                                ExpertProfileId = profileId,
                                SlotDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                                StartTime = new TimeOnly(9, 0),
                                EndTime = new TimeOnly(10, 0)
                            }
                        });
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(mockSlotRepo.Object);

            var booking = new Booking
            {
                Id = bookingId,
                UserId = taxpayerId,
                ExpertSlotId = slotId,
                BookingCode = "BK100",
                CompletedAt = DateTimeOffset.UtcNow
            };
            _mockBookingRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Booking, bool>>>()))
                            .ReturnsAsync(new List<Booking> { booking });

            var mockEscrowRepo = new Mock<IGenericRepository<EscrowTransaction>>();
            mockEscrowRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<EscrowTransaction, bool>>>()))
                          .ReturnsAsync(new List<EscrowTransaction>
                          {
                              new EscrowTransaction
                              {
                                  Id = Guid.NewGuid(),
                                  BookingId = bookingId,
                                  EscrowStatus = EscrowStatus.CoolingDown,
                                  Amount = 250000m,
                                  ExpertReceivable = 200000m,
                                  CreatedAt = DateTimeOffset.UtcNow
                              },
                              new EscrowTransaction
                              {
                                  Id = Guid.NewGuid(),
                                  BookingId = bookingId,
                                  EscrowStatus = EscrowStatus.HeldInEscrow,
                                  Amount = 300000m,
                                  ExpertReceivable = 240000m,
                                  CreatedAt = DateTimeOffset.UtcNow
                              }
                          });
            _mockUnitOfWork.Setup(u => u.Repository<EscrowTransaction>()).Returns(mockEscrowRepo.Object);

            var mockPolicyRepo = new Mock<IGenericRepository<ConsultationPlatformPolicy>>();
            mockPolicyRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationPlatformPolicy, bool>>>()))
                          .ReturnsAsync(new List<ConsultationPlatformPolicy> { new ConsultationPlatformPolicy { EscrowCooldownHours = 24 } });
            _mockUnitOfWork.Setup(u => u.Repository<ConsultationPlatformPolicy>()).Returns(mockPolicyRepo.Object);

            var wallet = new UserWallet { Id = walletId, UserId = expertUserId, WalletType = "EXPERT", AvailableBalance = 1500000m };
            _mockWalletRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserWallet, bool>>>()))
                           .ReturnsAsync(new List<UserWallet> { wallet });

            var mockWalletTxRepo = new Mock<IGenericRepository<WalletTransaction>>();
            mockWalletTxRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<WalletTransaction, bool>>>()))
                            .ReturnsAsync(new List<WalletTransaction>
                            {
                                new WalletTransaction
                                {
                                    Id = Guid.NewGuid(),
                                    WalletId = walletId,
                                    UserId = expertUserId,
                                    Amount = 500000m,
                                    TransactionType = "EARNED",
                                    CreatedAt = DateTimeOffset.UtcNow
                                }
                            });
            _mockUnitOfWork.Setup(u => u.Repository<WalletTransaction>()).Returns(mockWalletTxRepo.Object);

            var result = await _service.GetMyWalletAsync(expertUserId, "EXPERT");

            result.Should().NotBeNull();
            result.AvailableBalance.Should().Be(1500000m);
            result.PendingEscrowReceivable.Should().Be(440000m);
            result.RecentTransactions.Should().NotBeEmpty();
        }

        [Fact]
        public async Task UTCID28_GetMyWalletTransactionsAsync_Expert_FiltersTransactions()
        {
            var expertUserId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var slotId = Guid.NewGuid();
            var bookingId = Guid.NewGuid();
            var taxpayerId = Guid.NewGuid();
            var walletId = Guid.NewGuid();

            var expert = new User { UserId = expertUserId, UserRole = "expert", FullName = "Expert A" };
            var taxpayer = new User { UserId = taxpayerId, FullName = "Taxpayer B" };
            var mockUserRepo = new Mock<IGenericRepository<User>>();
            mockUserRepo.Setup(r => r.GetByIdAsync(expertUserId)).ReturnsAsync(expert);
            mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                        .ReturnsAsync(new List<User> { taxpayer });
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(mockUserRepo.Object);

            var mockProfileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            mockProfileRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertProfile, bool>>>()))
                           .ReturnsAsync(new List<ExpertProfile> { new ExpertProfile { Id = profileId, UserId = expertUserId } });
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(mockProfileRepo.Object);

            var mockSlotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            mockSlotRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertSlot, bool>>>()))
                        .ReturnsAsync(new List<ExpertSlot>
                        {
                            new ExpertSlot
                            {
                                Id = slotId,
                                ExpertProfileId = profileId,
                                SlotDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                                StartTime = new TimeOnly(9, 0),
                                EndTime = new TimeOnly(10, 0)
                            }
                        });
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(mockSlotRepo.Object);

            var booking = new Booking
            {
                Id = bookingId,
                UserId = taxpayerId,
                ExpertSlotId = slotId,
                BookingCode = "BK100",
                CompletedAt = DateTimeOffset.UtcNow
            };
            _mockBookingRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Booking, bool>>>()))
                            .ReturnsAsync(new List<Booking> { booking });

            var mockEscrowRepo = new Mock<IGenericRepository<EscrowTransaction>>();
            mockEscrowRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<EscrowTransaction, bool>>>()))
                          .ReturnsAsync(new List<EscrowTransaction>
                          {
                              new EscrowTransaction
                              {
                                  Id = Guid.NewGuid(),
                                  BookingId = bookingId,
                                  EscrowStatus = EscrowStatus.HeldInEscrow,
                                  Amount = 300000m,
                                  ExpertReceivable = 240000m,
                                  CreatedAt = DateTimeOffset.UtcNow
                              }
                          });
            _mockUnitOfWork.Setup(u => u.Repository<EscrowTransaction>()).Returns(mockEscrowRepo.Object);

            var mockPolicyRepo = new Mock<IGenericRepository<ConsultationPlatformPolicy>>();
            mockPolicyRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationPlatformPolicy, bool>>>()))
                          .ReturnsAsync(new List<ConsultationPlatformPolicy> { new ConsultationPlatformPolicy { EscrowCooldownHours = 24 } });
            _mockUnitOfWork.Setup(u => u.Repository<ConsultationPlatformPolicy>()).Returns(mockPolicyRepo.Object);

            var wallet = new UserWallet { Id = walletId, UserId = expertUserId, WalletType = "EXPERT", AvailableBalance = 1500000m };
            _mockWalletRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserWallet, bool>>>()))
                           .ReturnsAsync(new List<UserWallet> { wallet });

            var mockWalletTxRepo = new Mock<IGenericRepository<WalletTransaction>>();
            mockWalletTxRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<WalletTransaction, bool>>>()))
                            .ReturnsAsync(new List<WalletTransaction>
                            {
                                new WalletTransaction
                                {
                                    Id = Guid.NewGuid(),
                                    WalletId = walletId,
                                    UserId = expertUserId,
                                    Amount = 500000m,
                                    TransactionType = "WITHDRAWAL",
                                    CreatedAt = DateTimeOffset.UtcNow
                                }
                            });
            _mockUnitOfWork.Setup(u => u.Repository<WalletTransaction>()).Returns(mockWalletTxRepo.Object);

            var resEscrow = await _service.GetMyWalletTransactionsAsync(expertUserId, 1, 10, "PENDING_ESCROW");
            var resWithdrawal = await _service.GetMyWalletTransactionsAsync(expertUserId, 1, 10, "WITHDRAWAL");
            var resOther = await _service.GetMyWalletTransactionsAsync(expertUserId, 1, 10, "OTHER");

            resEscrow.Items.Should().NotBeEmpty();
            resWithdrawal.Items.Should().NotBeEmpty();
            resOther.Items.Should().BeEmpty();
        }

        [Fact]
        public async Task UTCID29_RequestUserRefundWithdrawalAsync_ValidationsAndSuccess()
        {
            var userId = Guid.NewGuid();
            var user = new User { UserId = userId, UserRole = "user", FullName = "Regular User" };
            var mockUserRepo = new Mock<IGenericRepository<User>>();
            mockUserRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);
            mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                        .ReturnsAsync(new List<User> { new User { UserId = Guid.NewGuid(), UserRole = "admin" } });
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(mockUserRepo.Object);

            var wallet = new UserWallet { Id = Guid.NewGuid(), UserId = userId, WalletType = "USER", AvailableBalance = 200000m };
            _mockWalletRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserWallet, bool>>>()))
                           .ReturnsAsync(new List<UserWallet> { wallet });

            var mockNotificationRepo = new Mock<IGenericRepository<SystemNotification>>();
            _mockUnitOfWork.Setup(u => u.Repository<SystemNotification>()).Returns(mockNotificationRepo.Object);

            var mockWithdrawalRepo = new Mock<IGenericRepository<WithdrawalRequest>>();
            _mockUnitOfWork.Setup(u => u.Repository<WithdrawalRequest>()).Returns(mockWithdrawalRepo.Object);

            var mockWalletTxRepo = new Mock<IGenericRepository<WalletTransaction>>();
            _mockUnitOfWork.Setup(u => u.Repository<WalletTransaction>()).Returns(mockWalletTxRepo.Object);

            // Min amount validation (< 50,000)
            var exMin = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.RequestUserRefundWithdrawalAsync(userId, new CreateWithdrawalRequest { Amount = 10000m }));
            exMin.ErrorCode.Should().Be("MIN_WITHDRAWAL_NOT_MET");

            // Insufficient balance
            var exBal = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.RequestUserRefundWithdrawalAsync(userId, new CreateWithdrawalRequest { Amount = 500000m }));
            exBal.ErrorCode.Should().Be("INSUFFICIENT_BALANCE");

            // Missing bank info
            var exBank = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.RequestUserRefundWithdrawalAsync(userId, new CreateWithdrawalRequest { Amount = 100000m }));
            exBank.ErrorCode.Should().Be("BANK_INFO_REQUIRED");

            // Success
            var result = await _service.RequestUserRefundWithdrawalAsync(userId, new CreateWithdrawalRequest
            {
                Amount = 100000m,
                BankName = "Vietcombank",
                AccountNumber = "0123456789",
                AccountName = "REGULAR USER"
            });

            result.Should().NotBeNull();
            result.Status.Should().Be("PENDING");
        }

        [Fact]
        public async Task UTCID30_ProcessWithdrawalAsync_RejectValidationAndSuccess()
        {
            var adminId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var withdrawalId = Guid.NewGuid();
            var user = new User { UserId = userId, UserRole = "user", FullName = "Test User" };

            var mockUserRepo = new Mock<IGenericRepository<User>>();
            mockUserRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(mockUserRepo.Object);

            var withdrawal = new WithdrawalRequest
            {
                Id = withdrawalId,
                UserId = userId,
                Amount = 100000m,
                BankName = "Techcombank",
                AccountNumber = "190333",
                Status = "PENDING"
            };
            var mockWithdrawalRepo = new Mock<IGenericRepository<WithdrawalRequest>>();
            mockWithdrawalRepo.Setup(r => r.GetByIdAsync(withdrawalId)).ReturnsAsync(withdrawal);
            _mockUnitOfWork.Setup(u => u.Repository<WithdrawalRequest>()).Returns(mockWithdrawalRepo.Object);

            var wallet = new UserWallet { Id = Guid.NewGuid(), UserId = userId, WalletType = "USER", AvailableBalance = 50000m, FrozenBalance = 100000m };
            _mockWalletRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserWallet, bool>>>()))
                           .ReturnsAsync(new List<UserWallet> { wallet });

            var mockWalletTxRepo = new Mock<IGenericRepository<WalletTransaction>>();
            _mockUnitOfWork.Setup(u => u.Repository<WalletTransaction>()).Returns(mockWalletTxRepo.Object);

            var mockNotificationRepo = new Mock<IGenericRepository<SystemNotification>>();
            _mockUnitOfWork.Setup(u => u.Repository<SystemNotification>()).Returns(mockNotificationRepo.Object);

            // Rejection reason missing
            var exReason = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.AdminProcessWithdrawalAsync(adminId, withdrawalId, new AdminProcessWithdrawalRequest { Action = "REJECT", RejectionReason = "" }));
            exReason.ErrorCode.Should().Be("REASON_REQUIRED");

            // Success rejection
            var result = await _service.AdminProcessWithdrawalAsync(adminId, withdrawalId, new AdminProcessWithdrawalRequest { Action = "REJECT", RejectionReason = "Sai thông tin STK" });
            result.Should().NotBeNull();
            result.Status.Should().Be("REJECTED");
            wallet.AvailableBalance.Should().Be(150000m);
        }

        [Fact]
        public async Task UTCID31_GetWithdrawalsAsync_NonAdmin_FiltersByUserIdAndStatus()
        {
            var userId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();

            var withdrawals = new List<WithdrawalRequest>
            {
                new WithdrawalRequest { Id = Guid.NewGuid(), UserId = userId, Status = "PENDING", CreatedAt = DateTimeOffset.UtcNow },
                new WithdrawalRequest { Id = Guid.NewGuid(), UserId = otherUserId, Status = "PENDING", CreatedAt = DateTimeOffset.UtcNow },
                new WithdrawalRequest { Id = Guid.NewGuid(), UserId = userId, Status = "COMPLETED", CreatedAt = DateTimeOffset.UtcNow }
            };

            var mockWithdrawalRepo = new Mock<IGenericRepository<WithdrawalRequest>>();
            mockWithdrawalRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(withdrawals);
            _mockUnitOfWork.Setup(u => u.Repository<WithdrawalRequest>()).Returns(mockWithdrawalRepo.Object);

            var mockUserRepo = new Mock<IGenericRepository<User>>();
            mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                        .ReturnsAsync(new List<User> { new User { UserId = userId, FullName = "My User" } });
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(mockUserRepo.Object);

            var result = await _service.GetWithdrawalsAsync(userId, "user", "PENDING", 1, 10);

            result.Should().NotBeNull();
            result.Items.Should().HaveCount(1);
            result.Items[0].UserId.Should().Be(userId);
            result.Items[0].Status.Should().Be("PENDING");
        }

        #endregion
    }
}
