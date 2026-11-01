using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TaxKeepVN.Application.DTOs.Consultations;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Helpers;
using TaxKeepVN.Application.Service.Implementations;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Domain.IRepositories;
using Xunit;

namespace TaxKeepVN.UnitTests.Application.Services
{
    public class ConsultationServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<ILogger<ConsultationService>> _mockLogger;
        private readonly Mock<IConsultationRealtimeNotifier> _mockNotifier;
        private readonly ConsultationPlatformPolicy _defaultPolicy;
        private readonly ConsultationService _service;

        public ConsultationServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockLogger = new Mock<ILogger<ConsultationService>>();
            _mockNotifier = new Mock<IConsultationRealtimeNotifier>();

            _defaultPolicy = new ConsultationPlatformPolicy
            {
                Id = 1,
                IsActive = true,
                SessionOpenBeforeMinutes = 10,
                SessionNoShowMinutes = 15,
                RatingWindowDays = 7,
                EscrowCooldownHours = 24
            };

            var policyRepo = new Mock<IGenericRepository<ConsultationPlatformPolicy>>();
            policyRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationPlatformPolicy, bool>>>()))
                      .ReturnsAsync(new List<ConsultationPlatformPolicy> { _defaultPolicy });
            _mockUnitOfWork.Setup(u => u.Repository<ConsultationPlatformPolicy>()).Returns(policyRepo.Object);

            var notifRepo = new Mock<IGenericRepository<SystemNotification>>();
            _mockUnitOfWork.Setup(u => u.Repository<SystemNotification>()).Returns(notifRepo.Object);

            var escrowRepo = new Mock<IGenericRepository<EscrowTransaction>>();
            escrowRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<EscrowTransaction, bool>>>()))
                      .ReturnsAsync(new List<EscrowTransaction>());
            _mockUnitOfWork.Setup(u => u.Repository<EscrowTransaction>()).Returns(escrowRepo.Object);

            var walletRepo = new Mock<IGenericRepository<UserWallet>>();
            walletRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserWallet, bool>>>()))
                      .ReturnsAsync(new List<UserWallet>());
            _mockUnitOfWork.Setup(u => u.Repository<UserWallet>()).Returns(walletRepo.Object);

            var txRepo = new Mock<IGenericRepository<WalletTransaction>>();
            _mockUnitOfWork.Setup(u => u.Repository<WalletTransaction>()).Returns(txRepo.Object);

            var sessionRepo = new Mock<IGenericRepository<ConsultationSession>>();
            sessionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationSession, bool>>>()))
                       .ReturnsAsync(new List<ConsultationSession>());
            _mockUnitOfWork.Setup(u => u.Repository<ConsultationSession>()).Returns(sessionRepo.Object);

            _service = new ConsultationService(_mockUnitOfWork.Object, _mockLogger.Object, _mockNotifier.Object);
        }

        #region JoinSessionAsync

        [Fact]
        public async Task UTCID01_JoinSessionAsync_BookingNotFound_ThrowsNotFoundException()
        {
            var bookingRepo = new Mock<IGenericRepository<Booking>>();
            bookingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Booking?)null);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(bookingRepo.Object);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.JoinSessionAsync(Guid.NewGuid(), "taxpayer", Guid.NewGuid()));
        }

        [Fact]
        public async Task UTCID02_JoinSessionAsync_SlotNotFound_ThrowsNotFoundException()
        {
            var bookingId = Guid.NewGuid();
            var booking = new Booking { Id = bookingId, ExpertSlotId = Guid.NewGuid(), UserId = Guid.NewGuid() };
            var bookingRepo = new Mock<IGenericRepository<Booking>>();
            bookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(bookingRepo.Object);

            var slotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            slotRepo.Setup(r => r.GetByIdAsync(booking.ExpertSlotId)).ReturnsAsync((ExpertSlot?)null);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(slotRepo.Object);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.JoinSessionAsync(booking.UserId, "taxpayer", bookingId));
        }

        [Fact]
        public async Task UTCID03_JoinSessionAsync_ProfileNotFound_ThrowsNotFoundException()
        {
            var bookingId = Guid.NewGuid();
            var slotId = Guid.NewGuid();
            var booking = new Booking { Id = bookingId, ExpertSlotId = slotId, UserId = Guid.NewGuid() };
            var bookingRepo = new Mock<IGenericRepository<Booking>>();
            bookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(bookingRepo.Object);

            var slot = new ExpertSlot { Id = slotId, ExpertProfileId = Guid.NewGuid() };
            var slotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            slotRepo.Setup(r => r.GetByIdAsync(slotId)).ReturnsAsync(slot);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(slotRepo.Object);

            var profileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            profileRepo.Setup(r => r.GetByIdAsync(slot.ExpertProfileId)).ReturnsAsync((ExpertProfile?)null);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(profileRepo.Object);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.JoinSessionAsync(booking.UserId, "taxpayer", bookingId));
        }

        [Fact]
        public async Task UTCID04_JoinSessionAsync_UnrelatedUser_ThrowsForbiddenException()
        {
            var bookingId = Guid.NewGuid();
            var slotId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var clientUserId = Guid.NewGuid();
            var expertUserId = Guid.NewGuid();
            var hackerId = Guid.NewGuid();

            var booking = new Booking { Id = bookingId, UserId = clientUserId, ExpertSlotId = slotId };
            var bookingRepo = new Mock<IGenericRepository<Booking>>();
            bookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(bookingRepo.Object);

            var slot = new ExpertSlot { Id = slotId, ExpertProfileId = profileId };
            var slotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            slotRepo.Setup(r => r.GetByIdAsync(slotId)).ReturnsAsync(slot);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(slotRepo.Object);

            var profile = new ExpertProfile { Id = profileId, UserId = expertUserId };
            var profileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            profileRepo.Setup(r => r.GetByIdAsync(profileId)).ReturnsAsync(profile);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(profileRepo.Object);

            var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
                _service.JoinSessionAsync(hackerId, "taxpayer", bookingId));
            ex.ErrorCode.Should().Be("FORBIDDEN");
        }

        [Fact]
        public async Task UTCID05_JoinSessionAsync_InvalidStatus_ThrowsBadRequestException()
        {
            var userId = Guid.NewGuid();
            var bookingId = Guid.NewGuid();
            var slotId = Guid.NewGuid();
            var profileId = Guid.NewGuid();

            var booking = new Booking { Id = bookingId, UserId = userId, ExpertSlotId = slotId, Status = BookingStatus.PENDING_PAYMENT };
            var bookingRepo = new Mock<IGenericRepository<Booking>>();
            bookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(bookingRepo.Object);

            var slot = new ExpertSlot { Id = slotId, ExpertProfileId = profileId };
            var slotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            slotRepo.Setup(r => r.GetByIdAsync(slotId)).ReturnsAsync(slot);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(slotRepo.Object);

            var profile = new ExpertProfile { Id = profileId, UserId = Guid.NewGuid() };
            var profileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            profileRepo.Setup(r => r.GetByIdAsync(profileId)).ReturnsAsync(profile);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(profileRepo.Object);

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.JoinSessionAsync(userId, "taxpayer", bookingId));
            ex.ErrorCode.Should().Be("INVALID_STATUS");
        }

        [Fact]
        public async Task UTCID06_JoinSessionAsync_RoomNotOpenYet_ThrowsBadRequestException()
        {
            var userId = Guid.NewGuid();
            var bookingId = Guid.NewGuid();
            var slotId = Guid.NewGuid();
            var profileId = Guid.NewGuid();

            var booking = new Booking { Id = bookingId, UserId = userId, ExpertSlotId = slotId, Status = BookingStatus.CONFIRMED };
            var bookingRepo = new Mock<IGenericRepository<Booking>>();
            bookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(bookingRepo.Object);

            // Slot is 3 days in the future
            var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3));
            var slot = new ExpertSlot
            {
                Id = slotId,
                ExpertProfileId = profileId,
                SlotDate = futureDate,
                StartTime = new TimeOnly(14, 0),
                EndTime = new TimeOnly(15, 0)
            };
            var slotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            slotRepo.Setup(r => r.GetByIdAsync(slotId)).ReturnsAsync(slot);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(slotRepo.Object);

            var profile = new ExpertProfile { Id = profileId, UserId = Guid.NewGuid() };
            var profileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            profileRepo.Setup(r => r.GetByIdAsync(profileId)).ReturnsAsync(profile);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(profileRepo.Object);

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.JoinSessionAsync(userId, "taxpayer", bookingId));
            ex.ErrorCode.Should().Be("ROOM_NOT_OPEN_YET");
        }

        [Fact]
        public async Task UTCID07_JoinSessionAsync_ClientFirstCheckIn_CreatesSessionAndWaitsExpert()
        {
            var userId = Guid.NewGuid();
            var expertUserId = Guid.NewGuid();
            var bookingId = Guid.NewGuid();
            var slotId = Guid.NewGuid();
            var profileId = Guid.NewGuid();

            var booking = new Booking
            {
                Id = bookingId,
                BookingCode = "BK-20250101-JOIN01",
                UserId = userId,
                ExpertSlotId = slotId,
                Status = BookingStatus.CONFIRMED
            };
            var bookingRepo = new Mock<IGenericRepository<Booking>>();
            bookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(bookingRepo.Object);

            // Slot is right now (open)
            var nowVn = DateTime.UtcNow.AddHours(7);
            var slot = new ExpertSlot
            {
                Id = slotId,
                ExpertProfileId = profileId,
                SlotDate = DateOnly.FromDateTime(nowVn),
                StartTime = TimeOnly.FromDateTime(nowVn.AddMinutes(5)),
                EndTime = TimeOnly.FromDateTime(nowVn.AddMinutes(65))
            };
            var slotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            slotRepo.Setup(r => r.GetByIdAsync(slotId)).ReturnsAsync(slot);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(slotRepo.Object);

            var profile = new ExpertProfile { Id = profileId, UserId = expertUserId };
            var profileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            profileRepo.Setup(r => r.GetByIdAsync(profileId)).ReturnsAsync(profile);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(profileRepo.Object);

            var sessionRepo = new Mock<IGenericRepository<ConsultationSession>>();
            sessionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationSession, bool>>>()))
                       .ReturnsAsync(new List<ConsultationSession>());
            _mockUnitOfWork.Setup(u => u.Repository<ConsultationSession>()).Returns(sessionRepo.Object);

            var result = await _service.JoinSessionAsync(userId, "taxpayer", bookingId);

            result.Should().NotBeNull();
            result.BothPresent.Should().BeFalse();
            result.ParticipantRole.Should().Be("taxpayer");
            sessionRepo.Verify(r => r.AddAsync(It.IsAny<ConsultationSession>()), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task UTCID08_JoinSessionAsync_BothPresent_StartsSessionAndNotifies()
        {
            var userId = Guid.NewGuid();
            var expertUserId = Guid.NewGuid();
            var bookingId = Guid.NewGuid();
            var slotId = Guid.NewGuid();
            var profileId = Guid.NewGuid();

            var booking = new Booking
            {
                Id = bookingId,
                BookingCode = "BK-20250101-JOIN02",
                UserId = userId,
                ExpertSlotId = slotId,
                Status = BookingStatus.CONFIRMED
            };
            var bookingRepo = new Mock<IGenericRepository<Booking>>();
            bookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(bookingRepo.Object);

            var nowVn = DateTime.UtcNow.AddHours(7);
            var slot = new ExpertSlot
            {
                Id = slotId,
                ExpertProfileId = profileId,
                SlotDate = DateOnly.FromDateTime(nowVn),
                StartTime = TimeOnly.FromDateTime(nowVn.AddMinutes(5)),
                EndTime = TimeOnly.FromDateTime(nowVn.AddMinutes(65))
            };
            var slotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            slotRepo.Setup(r => r.GetByIdAsync(slotId)).ReturnsAsync(slot);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(slotRepo.Object);

            var profile = new ExpertProfile { Id = profileId, UserId = expertUserId };
            var profileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            profileRepo.Setup(r => r.GetByIdAsync(profileId)).ReturnsAsync(profile);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(profileRepo.Object);

            // User already checked in
            var existingSession = new ConsultationSession
            {
                Id = Guid.NewGuid(),
                BookingId = bookingId,
                UserCheckInAt = DateTimeOffset.UtcNow.AddMinutes(-2),
                RoomName = "TaxKeepVN_BK_20250101_JOIN02",
                RoomUrl = "https://meet.jit.si/TaxKeepVN_BK_20250101_JOIN02"
            };
            var sessionRepo = new Mock<IGenericRepository<ConsultationSession>>();
            sessionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationSession, bool>>>()))
                       .ReturnsAsync(new List<ConsultationSession> { existingSession });
            _mockUnitOfWork.Setup(u => u.Repository<ConsultationSession>()).Returns(sessionRepo.Object);

            var result = await _service.JoinSessionAsync(expertUserId, "expert", bookingId);

            result.Should().NotBeNull();
            result.BothPresent.Should().BeTrue();
            booking.Status.Should().Be(BookingStatus.IN_PROGRESS);
            _mockNotifier.Verify(n => n.NotifySessionStartedAsync(bookingId, It.IsAny<DateTimeOffset>()), Times.Once);
        }

        #endregion

        #region GetSessionAsync

        [Fact]
        public async Task UTCID09_GetSessionAsync_ForbiddenUser_ThrowsForbiddenException()
        {
            var bookingId = Guid.NewGuid();
            var slotId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var clientUserId = Guid.NewGuid();
            var expertUserId = Guid.NewGuid();
            var hackerId = Guid.NewGuid();

            var booking = new Booking { Id = bookingId, UserId = clientUserId, ExpertSlotId = slotId };
            var bookingRepo = new Mock<IGenericRepository<Booking>>();
            bookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(bookingRepo.Object);

            var slot = new ExpertSlot { Id = slotId, ExpertProfileId = profileId };
            var slotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            slotRepo.Setup(r => r.GetByIdAsync(slotId)).ReturnsAsync(slot);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(slotRepo.Object);

            var profile = new ExpertProfile { Id = profileId, UserId = expertUserId };
            var profileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            profileRepo.Setup(r => r.GetByIdAsync(profileId)).ReturnsAsync(profile);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(profileRepo.Object);

            var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
                _service.GetSessionAsync(hackerId, "taxpayer", bookingId));
            ex.ErrorCode.Should().Be("FORBIDDEN");
        }

        [Fact]
        public async Task UTCID10_GetSessionAsync_Valid_ReturnsSessionResponse()
        {
            var bookingId = Guid.NewGuid();
            var slotId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var clientUserId = Guid.NewGuid();
            var expertUserId = Guid.NewGuid();

            var booking = new Booking
            {
                Id = bookingId,
                BookingCode = "BK-20250101-GET01",
                UserId = clientUserId,
                ExpertSlotId = slotId,
                TopicTitle = "Quyết toán thuế 2024",
                ProblemDescription = "Chi tiết hồ sơ",
                SessionType = SessionType.ONLINE_MEETING,
                Status = BookingStatus.IN_PROGRESS
            };
            var bookingRepo = new Mock<IGenericRepository<Booking>>();
            bookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(bookingRepo.Object);

            var nowVn = DateTime.UtcNow.AddHours(7);
            var slot = new ExpertSlot
            {
                Id = slotId,
                ExpertProfileId = profileId,
                SlotDate = DateOnly.FromDateTime(nowVn),
                StartTime = new TimeOnly(14, 0),
                EndTime = new TimeOnly(15, 0)
            };
            var slotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            slotRepo.Setup(r => r.GetByIdAsync(slotId)).ReturnsAsync(slot);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(slotRepo.Object);

            var profile = new ExpertProfile { Id = profileId, UserId = expertUserId };
            var profileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            profileRepo.Setup(r => r.GetByIdAsync(profileId)).ReturnsAsync(profile);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(profileRepo.Object);

            var session = new ConsultationSession
            {
                Id = Guid.NewGuid(),
                BookingId = bookingId,
                UserCheckInAt = DateTimeOffset.UtcNow.AddMinutes(-20),
                ExpertCheckInAt = DateTimeOffset.UtcNow.AddMinutes(-18),
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-18),
                RoomName = "TaxKeepVN_BK_20250101_GET01",
                RoomUrl = "https://meet.jit.si/TaxKeepVN_BK_20250101_GET01"
            };
            var sessionRepo = new Mock<IGenericRepository<ConsultationSession>>();
            sessionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationSession, bool>>>()))
                       .ReturnsAsync(new List<ConsultationSession> { session });
            _mockUnitOfWork.Setup(u => u.Repository<ConsultationSession>()).Returns(sessionRepo.Object);

            var result = await _service.GetSessionAsync(clientUserId, "taxpayer", bookingId);

            result.Should().NotBeNull();
            result.BookingCode.Should().Be("BK-20250101-GET01");
            result.IsUserPresent.Should().BeTrue();
            result.IsExpertPresent.Should().BeTrue();
            result.ScheduledDurationMinutes.Should().Be(60);
        }

        #endregion

        #region GetSharedTaxDossierAsync

        [Fact]
        public async Task UTCID11_GetSharedTaxDossierAsync_InvalidStatus_ThrowsForbiddenException()
        {
            var bookingId = Guid.NewGuid();
            var slotId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var clientUserId = Guid.NewGuid();
            var expertUserId = Guid.NewGuid();

            var booking = new Booking
            {
                Id = bookingId,
                UserId = clientUserId,
                ExpertSlotId = slotId,
                Status = BookingStatus.PENDING_PAYMENT // Unconfirmed
            };
            var bookingRepo = new Mock<IGenericRepository<Booking>>();
            bookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(bookingRepo.Object);

            var slot = new ExpertSlot { Id = slotId, ExpertProfileId = profileId };
            var slotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            slotRepo.Setup(r => r.GetByIdAsync(slotId)).ReturnsAsync(slot);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(slotRepo.Object);

            var profile = new ExpertProfile { Id = profileId, UserId = expertUserId };
            var profileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            profileRepo.Setup(r => r.GetByIdAsync(profileId)).ReturnsAsync(profile);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(profileRepo.Object);

            var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
                _service.GetSharedTaxDossierAsync(expertUserId, "expert", bookingId));
            ex.ErrorCode.Should().Be("FORBIDDEN");
        }

        [Fact]
        public async Task UTCID12_GetSharedTaxDossierAsync_Valid_ReturnsCompleteTaxDossier()
        {
            var bookingId = Guid.NewGuid();
            var slotId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var clientUserId = Guid.NewGuid();
            var expertUserId = Guid.NewGuid();

            var booking = new Booking
            {
                Id = bookingId,
                UserId = clientUserId,
                ExpertSlotId = slotId,
                Status = BookingStatus.CONFIRMED,
                BookingCode = "BK-20250101-DOSSIER"
            };
            var bookingRepo = new Mock<IGenericRepository<Booking>>();
            bookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(bookingRepo.Object);

            var slot = new ExpertSlot { Id = slotId, ExpertProfileId = profileId, SlotDate = new DateOnly(2025, 5, 1) };
            var slotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            slotRepo.Setup(r => r.GetByIdAsync(slotId)).ReturnsAsync(slot);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(slotRepo.Object);

            var profile = new ExpertProfile { Id = profileId, UserId = expertUserId };
            var profileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            profileRepo.Setup(r => r.GetByIdAsync(profileId)).ReturnsAsync(profile);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(profileRepo.Object);

            var userRepo = new Mock<IGenericRepository<User>>();
            userRepo.Setup(r => r.GetByIdAsync(clientUserId)).ReturnsAsync(new User { UserId = clientUserId, FullName = "Client User", Email = "client@taxkeep.vn" });
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(userRepo.Object);

            var incomeSourceRepo = new Mock<IGenericRepository<IncomeSource>>();
            incomeSourceRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<IncomeSource, bool>>>()))
                            .ReturnsAsync(new List<IncomeSource>
                            {
                                new IncomeSource { Id = Guid.NewGuid(), CompanyName = "FPT Software", TotalIncome = 200000000m }
                            });
            _mockUnitOfWork.Setup(u => u.Repository<IncomeSource>()).Returns(incomeSourceRepo.Object);

            var incomeRepo = new Mock<IGenericRepository<Income>>();
            incomeRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Income, bool>>>()))
                      .ReturnsAsync(new List<Income>
                      {
                          new Income { Id = Guid.NewGuid(), Month = 1, Year = 2025, TotalTaxableIncome = 20000000m, TaxAlreadyDeducted = 1000000m }
                      });
            _mockUnitOfWork.Setup(u => u.Repository<Income>()).Returns(incomeRepo.Object);

            var dependentRepo = new Mock<IGenericRepository<Dependent>>();
            dependentRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Dependent, bool>>>()))
                         .ReturnsAsync(new List<Dependent>
                         {
                             new Dependent { Id = Guid.NewGuid(), FullName = "Nguyen Con A", Relationship = DependentRelationship.CHILD, CurrentGroup = DependentGroup.CHILD_UNDER_18 }
                         });
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(dependentRepo.Object);

            var periodRepo = new Mock<IGenericRepository<TaxPeriod>>();
            periodRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TaxPeriod, bool>>>()))
                      .ReturnsAsync(new List<TaxPeriod>());
            _mockUnitOfWork.Setup(u => u.Repository<TaxPeriod>()).Returns(periodRepo.Object);

            var docRepo = new Mock<IGenericRepository<Document>>();
            docRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Document, bool>>>()))
                   .ReturnsAsync(new List<Document>());
            _mockUnitOfWork.Setup(u => u.Repository<Document>()).Returns(docRepo.Object);

            var dossierRepo = new Mock<IGenericRepository<TaxSettlementDossier>>();
            dossierRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TaxSettlementDossier, bool>>>()))
                       .ReturnsAsync(new List<TaxSettlementDossier>
                       {
                           new TaxSettlementDossier { Id = Guid.NewGuid(), TaxYear = 2025, TotalGrossIncome = 240000000m, TotalTaxWithheld = 12000000m }
                       });
            _mockUnitOfWork.Setup(u => u.Repository<TaxSettlementDossier>()).Returns(dossierRepo.Object);

            var result = await _service.GetSharedTaxDossierAsync(expertUserId, "expert", bookingId);

            result.Should().NotBeNull();
            result.TaxYear.Should().Be(2025);
            result.TaxpayerFullName.Should().Be("Client User");
            result.IncomeSources.Should().HaveCount(1);
            result.Dependents.Should().HaveCount(1);
            result.DraftSettlement.Should().NotBeNull();
            result.DraftSettlement!.TotalGrossIncome.Should().Be(240000000m);
        }

        #endregion

        #region UpdateNotesAsync & CompleteSessionAsync

        [Fact]
        public async Task UTCID13_UpdateNotesAsync_Valid_UpdatesNotesAndNotifies()
        {
            var bookingId = Guid.NewGuid();
            var slotId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var clientUserId = Guid.NewGuid();
            var expertUserId = Guid.NewGuid();

            var booking = new Booking { Id = bookingId, UserId = clientUserId, ExpertSlotId = slotId, Status = BookingStatus.IN_PROGRESS, BookingCode = "BK-20250101-NOTES" };
            var bookingRepo = new Mock<IGenericRepository<Booking>>();
            bookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(bookingRepo.Object);

            var slot = new ExpertSlot { Id = slotId, ExpertProfileId = profileId, SlotDate = new DateOnly(2025, 5, 1) };
            var slotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            slotRepo.Setup(r => r.GetByIdAsync(slotId)).ReturnsAsync(slot);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(slotRepo.Object);

            var profile = new ExpertProfile { Id = profileId, UserId = expertUserId };
            var profileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            profileRepo.Setup(r => r.GetByIdAsync(profileId)).ReturnsAsync(profile);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(profileRepo.Object);

            var session = new ConsultationSession { Id = Guid.NewGuid(), BookingId = bookingId, SharedNotes = "Ghi chú cũ" };
            var sessionRepo = new Mock<IGenericRepository<ConsultationSession>>();
            sessionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationSession, bool>>>()))
                       .ReturnsAsync(new List<ConsultationSession> { session });
            _mockUnitOfWork.Setup(u => u.Repository<ConsultationSession>()).Returns(sessionRepo.Object);

            var request = new UpdateConsultationNotesRequest { SharedNotes = "Ghi chú mới bổ sung tài liệu quyết toán thuế TNCN" };
            var result = await _service.UpdateNotesAsync(expertUserId, "expert", bookingId, request);

            result.Should().NotBeNull();
            session.SharedNotes.Should().Be("Ghi chú mới bổ sung tài liệu quyết toán thuế TNCN");
            _mockNotifier.Verify(n => n.NotifyNotesUpdatedAsync(bookingId, session.SharedNotes), Times.Once);
        }

        [Fact]
        public async Task UTCID14_CompleteSessionAsync_NotesTooShort_ThrowsBadRequestException()
        {
            var bookingId = Guid.NewGuid();
            var slotId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var expertUserId = Guid.NewGuid();

            var booking = new Booking { Id = bookingId, ExpertSlotId = slotId, Status = BookingStatus.IN_PROGRESS };
            var bookingRepo = new Mock<IGenericRepository<Booking>>();
            bookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(bookingRepo.Object);

            var slot = new ExpertSlot { Id = slotId, ExpertProfileId = profileId };
            var slotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            slotRepo.Setup(r => r.GetByIdAsync(slotId)).ReturnsAsync(slot);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(slotRepo.Object);

            var profile = new ExpertProfile { Id = profileId, UserId = expertUserId };
            var profileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            profileRepo.Setup(r => r.GetByIdAsync(profileId)).ReturnsAsync(profile);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(profileRepo.Object);

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.CompleteSessionAsync(expertUserId, "expert", bookingId, new CompleteConsultationRequest { SharedNotes = "Ngan qua" }));
            ex.ErrorCode.Should().Be("NOTES_REQUIRED");
        }

        [Fact]
        public async Task UTCID15_CompleteSessionAsync_Valid_CompletesSessionAndTransitionsEscrow()
        {
            var bookingId = Guid.NewGuid();
            var slotId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var clientUserId = Guid.NewGuid();
            var expertUserId = Guid.NewGuid();

            var booking = new Booking
            {
                Id = bookingId,
                UserId = clientUserId,
                ExpertSlotId = slotId,
                Status = BookingStatus.IN_PROGRESS,
                BookingCode = "BK-20250101-CMP01"
            };
            var bookingRepo = new Mock<IGenericRepository<Booking>>();
            bookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(bookingRepo.Object);

            var slot = new ExpertSlot { Id = slotId, ExpertProfileId = profileId, SlotDate = new DateOnly(2025, 5, 1) };
            var slotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            slotRepo.Setup(r => r.GetByIdAsync(slotId)).ReturnsAsync(slot);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(slotRepo.Object);

            var profile = new ExpertProfile { Id = profileId, UserId = expertUserId, CompletedConsultationsCount = 5 };
            var profileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            profileRepo.Setup(r => r.GetByIdAsync(profileId)).ReturnsAsync(profile);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(profileRepo.Object);

            var session = new ConsultationSession
            {
                Id = Guid.NewGuid(),
                BookingId = bookingId,
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-45)
            };
            var sessionRepo = new Mock<IGenericRepository<ConsultationSession>>();
            sessionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationSession, bool>>>()))
                       .ReturnsAsync(new List<ConsultationSession> { session });
            _mockUnitOfWork.Setup(u => u.Repository<ConsultationSession>()).Returns(sessionRepo.Object);

            var escrow = new EscrowTransaction { Id = Guid.NewGuid(), BookingId = bookingId, EscrowStatus = EscrowStatus.HeldInEscrow };
            var escrowRepo = new Mock<IGenericRepository<EscrowTransaction>>();
            escrowRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<EscrowTransaction, bool>>>()))
                      .ReturnsAsync(new List<EscrowTransaction> { escrow });
            _mockUnitOfWork.Setup(u => u.Repository<EscrowTransaction>()).Returns(escrowRepo.Object);

            var request = new CompleteConsultationRequest
            {
                SharedNotes = "Đã tư vấn hoàn tất hồ sơ giảm trừ gia cảnh người phụ thuộc và hướng dẫn nộp online."
            };

            var result = await _service.CompleteSessionAsync(expertUserId, "expert", bookingId, request);

            result.Should().NotBeNull();
            booking.Status.Should().Be(BookingStatus.COMPLETED);
            booking.CompletedAt.Should().NotBeNull();
            escrow.EscrowStatus.Should().Be(EscrowStatus.CoolingDown);
            profile.CompletedConsultationsCount.Should().Be(6);
            _mockNotifier.Verify(n => n.NotifySessionCompletedAsync(bookingId, It.IsAny<int>(), It.IsAny<string>()), Times.Once);
        }

        #endregion

        #region ReportNoShowAsync

        [Fact]
        public async Task UTCID16_ReportNoShowAsync_TooEarly_ThrowsBadRequestException()
        {
            var bookingId = Guid.NewGuid();
            var slotId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var clientUserId = Guid.NewGuid();
            var expertUserId = Guid.NewGuid();

            var booking = new Booking { Id = bookingId, UserId = clientUserId, ExpertSlotId = slotId };
            var bookingRepo = new Mock<IGenericRepository<Booking>>();
            bookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(bookingRepo.Object);

            // Slot is starting right now (less than 15 mins passed)
            var nowVn = DateTime.UtcNow.AddHours(7);
            var slot = new ExpertSlot
            {
                Id = slotId,
                ExpertProfileId = profileId,
                SlotDate = DateOnly.FromDateTime(nowVn),
                StartTime = TimeOnly.FromDateTime(nowVn),
                EndTime = TimeOnly.FromDateTime(nowVn.AddHours(1))
            };
            var slotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            slotRepo.Setup(r => r.GetByIdAsync(slotId)).ReturnsAsync(slot);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(slotRepo.Object);

            var profile = new ExpertProfile { Id = profileId, UserId = expertUserId };
            var profileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            profileRepo.Setup(r => r.GetByIdAsync(profileId)).ReturnsAsync(profile);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(profileRepo.Object);

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.ReportNoShowAsync(clientUserId, "taxpayer", bookingId, new ReportNoShowRequest()));
            ex.ErrorCode.Should().Be("NO_SHOW_EARLY");
        }

        [Fact]
        public async Task UTCID17_ReportNoShowAsync_ExpertNoShow_Refunds100Percent()
        {
            var bookingId = Guid.NewGuid();
            var slotId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var clientUserId = Guid.NewGuid();
            var expertUserId = Guid.NewGuid();

            var booking = new Booking
            {
                Id = bookingId,
                UserId = clientUserId,
                ExpertSlotId = slotId,
                BookingCode = "BK-20250101-NOSHOW-EXPERT",
                Fee = 500000m,
                Status = BookingStatus.CONFIRMED
            };
            var bookingRepo = new Mock<IGenericRepository<Booking>>();
            bookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(bookingRepo.Object);

            // Slot was 30 minutes ago (> 15 min threshold)
            var nowVn = DateTime.UtcNow.AddHours(7);
            var slot = new ExpertSlot
            {
                Id = slotId,
                ExpertProfileId = profileId,
                SlotDate = DateOnly.FromDateTime(nowVn),
                StartTime = TimeOnly.FromDateTime(nowVn.AddMinutes(-30)),
                EndTime = TimeOnly.FromDateTime(nowVn.AddMinutes(30)),
                IsBooked = true
            };
            var slotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            slotRepo.Setup(r => r.GetByIdAsync(slotId)).ReturnsAsync(slot);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(slotRepo.Object);

            var profile = new ExpertProfile { Id = profileId, UserId = expertUserId };
            var profileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            profileRepo.Setup(r => r.GetByIdAsync(profileId)).ReturnsAsync(profile);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(profileRepo.Object);

            // User is present, Expert is not
            var session = new ConsultationSession
            {
                Id = Guid.NewGuid(),
                BookingId = bookingId,
                UserCheckInAt = DateTimeOffset.UtcNow.AddMinutes(-25),
                ExpertCheckInAt = null
            };
            var sessionRepo = new Mock<IGenericRepository<ConsultationSession>>();
            sessionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationSession, bool>>>()))
                       .ReturnsAsync(new List<ConsultationSession> { session });
            _mockUnitOfWork.Setup(u => u.Repository<ConsultationSession>()).Returns(sessionRepo.Object);

            var escrow = new EscrowTransaction { Id = Guid.NewGuid(), BookingId = bookingId, Amount = 500000m, EscrowStatus = EscrowStatus.HeldInEscrow };
            var escrowRepo = new Mock<IGenericRepository<EscrowTransaction>>();
            escrowRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<EscrowTransaction, bool>>>()))
                      .ReturnsAsync(new List<EscrowTransaction> { escrow });
            _mockUnitOfWork.Setup(u => u.Repository<EscrowTransaction>()).Returns(escrowRepo.Object);

            var userWallet = new UserWallet { Id = Guid.NewGuid(), UserId = clientUserId, WalletType = "USER", AvailableBalance = 0m };
            var walletRepo = new Mock<IGenericRepository<UserWallet>>();
            walletRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserWallet, bool>>>()))
                      .ReturnsAsync(new List<UserWallet> { userWallet });
            _mockUnitOfWork.Setup(u => u.Repository<UserWallet>()).Returns(walletRepo.Object);

            var result = await _service.ReportNoShowAsync(clientUserId, "taxpayer", bookingId, new ReportNoShowRequest { Reason = "Chuyên gia không vào phòng" });

            result.Should().NotBeNull();
            booking.Status.Should().Be(BookingStatus.CANCELLED_BY_EXPERT);
            booking.CancelledBy.Should().Be("EXPERT_NO_SHOW");
            session.NoShowType.Should().Be("EXPERT_NO_SHOW");
            escrow.EscrowStatus.Should().Be(EscrowStatus.Refunded);
            userWallet.AvailableBalance.Should().Be(500000m);
        }

        [Fact]
        public async Task UTCID18_ReportNoShowAsync_UserNoShow_ProtectsExpertFee()
        {
            var bookingId = Guid.NewGuid();
            var slotId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var clientUserId = Guid.NewGuid();
            var expertUserId = Guid.NewGuid();

            var booking = new Booking
            {
                Id = bookingId,
                UserId = clientUserId,
                ExpertSlotId = slotId,
                BookingCode = "BK-20250101-NOSHOW-USER",
                Fee = 500000m,
                Status = BookingStatus.CONFIRMED
            };
            var bookingRepo = new Mock<IGenericRepository<Booking>>();
            bookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(bookingRepo.Object);

            var nowVn = DateTime.UtcNow.AddHours(7);
            var slot = new ExpertSlot
            {
                Id = slotId,
                ExpertProfileId = profileId,
                SlotDate = DateOnly.FromDateTime(nowVn),
                StartTime = TimeOnly.FromDateTime(nowVn.AddMinutes(-30)),
                EndTime = TimeOnly.FromDateTime(nowVn.AddMinutes(30)),
                IsBooked = true
            };
            var slotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            slotRepo.Setup(r => r.GetByIdAsync(slotId)).ReturnsAsync(slot);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(slotRepo.Object);

            var profile = new ExpertProfile { Id = profileId, UserId = expertUserId };
            var profileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            profileRepo.Setup(r => r.GetByIdAsync(profileId)).ReturnsAsync(profile);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(profileRepo.Object);

            // Expert is present, User is not
            var session = new ConsultationSession
            {
                Id = Guid.NewGuid(),
                BookingId = bookingId,
                UserCheckInAt = null,
                ExpertCheckInAt = DateTimeOffset.UtcNow.AddMinutes(-25)
            };
            var sessionRepo = new Mock<IGenericRepository<ConsultationSession>>();
            sessionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationSession, bool>>>()))
                       .ReturnsAsync(new List<ConsultationSession> { session });
            _mockUnitOfWork.Setup(u => u.Repository<ConsultationSession>()).Returns(sessionRepo.Object);

            var escrow = new EscrowTransaction { Id = Guid.NewGuid(), BookingId = bookingId, Amount = 500000m, EscrowStatus = EscrowStatus.HeldInEscrow };
            var escrowRepo = new Mock<IGenericRepository<EscrowTransaction>>();
            escrowRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<EscrowTransaction, bool>>>()))
                      .ReturnsAsync(new List<EscrowTransaction> { escrow });
            _mockUnitOfWork.Setup(u => u.Repository<EscrowTransaction>()).Returns(escrowRepo.Object);

            var result = await _service.ReportNoShowAsync(expertUserId, "expert", bookingId, new ReportNoShowRequest());

            result.Should().NotBeNull();
            booking.Status.Should().Be(BookingStatus.COMPLETED);
            session.NoShowType.Should().Be("USER_NO_SHOW");
            escrow.EscrowStatus.Should().Be(EscrowStatus.CoolingDown);
        }

        #endregion

        #region GetUpcomingMeetingReminderAsync

        [Fact]
        public async Task UTCID19_GetUpcomingMeetingReminderAsync_NoBookings_ReturnsNoMeeting()
        {
            var userId = Guid.NewGuid();
            var bookingRepo = new Mock<IGenericRepository<Booking>>();
            bookingRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Booking, bool>>>()))
                       .ReturnsAsync(new List<Booking>());
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(bookingRepo.Object);

            var profileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            profileRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertProfile, bool>>>()))
                       .ReturnsAsync(new List<ExpertProfile>());
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(profileRepo.Object);

            var result = await _service.GetUpcomingMeetingReminderAsync(userId);

            result.Should().NotBeNull();
            result.HasUpcomingMeeting.Should().BeFalse();
        }

        [Fact]
        public async Task UTCID20_GetUpcomingMeetingReminderAsync_UpcomingMeeting_ReturnsReminderDto()
        {
            var userId = Guid.NewGuid();
            var expertUserId = Guid.NewGuid();
            var slotId = Guid.NewGuid();
            var profileId = Guid.NewGuid();

            var booking = new Booking
            {
                Id = Guid.NewGuid(),
                BookingCode = "BK-20250101-REMIND",
                UserId = userId,
                ExpertSlotId = slotId,
                Status = BookingStatus.CONFIRMED,
                TopicTitle = "Hỏi đáp thuế"
            };
            var bookingRepo = new Mock<IGenericRepository<Booking>>();
            bookingRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Booking, bool>>>()))
                       .ReturnsAsync(new List<Booking> { booking });
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(bookingRepo.Object);

            var profile = new ExpertProfile { Id = profileId, UserId = expertUserId, JobTitle = "Chuyên gia thuế" };
            var profileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            profileRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertProfile, bool>>>()))
                       .ReturnsAsync(new List<ExpertProfile>());
            profileRepo.Setup(r => r.GetByIdAsync(profileId)).ReturnsAsync(profile);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(profileRepo.Object);

            var nowVn = DateTime.UtcNow.AddHours(7);
            var slot = new ExpertSlot
            {
                Id = slotId,
                ExpertProfileId = profileId,
                SlotDate = DateOnly.FromDateTime(nowVn),
                StartTime = TimeOnly.FromDateTime(nowVn.AddMinutes(5)), // starting in 5 minutes (open)
                EndTime = TimeOnly.FromDateTime(nowVn.AddMinutes(65))
            };
            var slotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            slotRepo.Setup(r => r.GetByIdAsync(slotId)).ReturnsAsync(slot);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(slotRepo.Object);

            var userRepo = new Mock<IGenericRepository<User>>();
            userRepo.Setup(r => r.GetByIdAsync(expertUserId)).ReturnsAsync(new User { UserId = expertUserId, FullName = "Nguyen Van ChuyenGia" });
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(userRepo.Object);

            var result = await _service.GetUpcomingMeetingReminderAsync(userId);

            result.Should().NotBeNull();
            result.HasUpcomingMeeting.Should().BeTrue();
            result.PartnerName.Should().Be("Nguyen Van ChuyenGia");
            result.IsRoomOpen.Should().BeTrue();
            result.CanJoinRoom.Should().BeTrue();
            result.UrgencyLevel.Should().Be("OPENED");
        }

        #endregion
    }
}
