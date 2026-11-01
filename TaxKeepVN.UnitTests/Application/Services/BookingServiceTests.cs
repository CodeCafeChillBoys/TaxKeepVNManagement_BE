using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TaxKeepVN.Application.DTOs.Bookings;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Implementations;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Domain.IRepositories;
using Xunit;

namespace TaxKeepVN.UnitTests.Application.Services
{
    public class BookingServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IFileStorageService> _mockFileStorage;
        private readonly Mock<ILogger<BookingService>> _mockLogger;
        private readonly ConsultationPlatformPolicy _defaultPolicy;
        private readonly BookingService _service;

        public BookingServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockFileStorage = new Mock<IFileStorageService>();
            _mockLogger = new Mock<ILogger<BookingService>>();

            _defaultPolicy = new ConsultationPlatformPolicy
            {
                Id = 1,
                IsActive = true,
                BookingMinLeadTimeHours = 4,
                BookingSlotHoldMinutes = 10,
                CancelFullRefundHours = 24,
                CancelPartialRefundHours = 4,
                CancelPartialRefundPercent = 50m,
                ExpertLateCancelHours = 12,
                ExpertLateCancelPenaltyVnd = 100000m
            };

            var policyRepo = new Mock<IGenericRepository<ConsultationPlatformPolicy>>();
            policyRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationPlatformPolicy, bool>>>()))
                      .ReturnsAsync(new List<ConsultationPlatformPolicy> { _defaultPolicy });
            _mockUnitOfWork.Setup(u => u.Repository<ConsultationPlatformPolicy>()).Returns(policyRepo.Object);

            var notifRepo = new Mock<IGenericRepository<SystemNotification>>();
            _mockUnitOfWork.Setup(u => u.Repository<SystemNotification>()).Returns(notifRepo.Object);

            var feeProposalRepo = new Mock<IGenericRepository<ExpertApplicationFeeProposal>>();
            feeProposalRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationFeeProposal, bool>>>()))
                           .ReturnsAsync(new List<ExpertApplicationFeeProposal>());
            _mockUnitOfWork.Setup(u => u.Repository<ExpertApplicationFeeProposal>()).Returns(feeProposalRepo.Object);

            var feeConfigRepo = new Mock<IGenericRepository<ConsultationFeeConfiguration>>();
            feeConfigRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationFeeConfiguration, bool>>>()))
                         .ReturnsAsync(new List<ConsultationFeeConfiguration>());
            _mockUnitOfWork.Setup(u => u.Repository<ConsultationFeeConfiguration>()).Returns(feeConfigRepo.Object);

            var sessionRepo = new Mock<IGenericRepository<ConsultationSession>>();
            sessionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationSession, bool>>>()))
                       .ReturnsAsync(new List<ConsultationSession>());
            _mockUnitOfWork.Setup(u => u.Repository<ConsultationSession>()).Returns(sessionRepo.Object);

            var appRepo = new Mock<IGenericRepository<ExpertApplication>>();
            appRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplication, bool>>>()))
                   .ReturnsAsync(new List<ExpertApplication>());
            _mockUnitOfWork.Setup(u => u.Repository<ExpertApplication>()).Returns(appRepo.Object);

            var walletRepo = new Mock<IGenericRepository<UserWallet>>();
            walletRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserWallet, bool>>>()))
                      .ReturnsAsync(new List<UserWallet>());
            _mockUnitOfWork.Setup(u => u.Repository<UserWallet>()).Returns(walletRepo.Object);

            var txRepo = new Mock<IGenericRepository<WalletTransaction>>();
            _mockUnitOfWork.Setup(u => u.Repository<WalletTransaction>()).Returns(txRepo.Object);

            var escrowRepo = new Mock<IGenericRepository<EscrowTransaction>>();
            escrowRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<EscrowTransaction, bool>>>()))
                      .ReturnsAsync(new List<EscrowTransaction>());
            _mockUnitOfWork.Setup(u => u.Repository<EscrowTransaction>()).Returns(escrowRepo.Object);

            _service = new BookingService(
                _mockUnitOfWork.Object,
                _mockFileStorage.Object,
                _mockLogger.Object
            );
        }

        #region GetAvailableSlotsForBookingAsync

        [Fact]
        public async Task UTCID01_GetAvailableSlots_ExpertProfileNotFound_ThrowsNotFoundException()
        {
            var profileId = Guid.NewGuid();
            var mockRepo = new Mock<IGenericRepository<ExpertProfile>>();
            mockRepo.Setup(r => r.GetByIdAsync(profileId)).ReturnsAsync((ExpertProfile?)null);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(mockRepo.Object);

            var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.GetAvailableSlotsForBookingAsync(profileId));
            ex.Message.Should().Contain(profileId.ToString());
        }

        [Fact]
        public async Task UTCID06_GetAvailableSlots_Success_ReturnsAvailableSlots()
        {
            var profileId = Guid.NewGuid();
            var profile = new ExpertProfile { Id = profileId, UserId = Guid.NewGuid() };
            var mockProfileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            mockProfileRepo.Setup(r => r.GetByIdAsync(profileId)).ReturnsAsync(profile);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(mockProfileRepo.Object);

            var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));
            var slot1 = new ExpertSlot
            {
                Id = Guid.NewGuid(),
                ExpertProfileId = profileId,
                SlotDate = futureDate,
                StartTime = new TimeOnly(14, 0),
                EndTime = new TimeOnly(15, 0),
                SessionType = SessionType.ONLINE_MEETING,
                IsActive = true,
                IsBooked = false,
                HoldExpiresAt = null
            };
            var slot2Held = new ExpertSlot
            {
                Id = Guid.NewGuid(),
                ExpertProfileId = profileId,
                SlotDate = futureDate,
                StartTime = new TimeOnly(16, 0),
                EndTime = new TimeOnly(17, 0),
                SessionType = SessionType.ONLINE_MEETING,
                IsActive = true,
                IsBooked = false,
                HoldExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5) // currently held
            };

            var mockSlotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            mockSlotRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertSlot, bool>>>()))
                        .ReturnsAsync(new List<ExpertSlot> { slot1, slot2Held });
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(mockSlotRepo.Object);

            var result = await _service.GetAvailableSlotsForBookingAsync(profileId, futureDate, 7);

            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            result[0].Id.Should().Be(slot1.Id);
        }

        #endregion

        #region PreviewBookingAsync

        [Fact]
        public async Task UTCID07_PreviewBookingAsync_SlotNotFound_ThrowsNotFoundException()
        {
            var slotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            slotRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((ExpertSlot?)null);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(slotRepo.Object);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.PreviewBookingAsync(Guid.NewGuid(), new BookingPreviewRequest { ExpertSlotId = Guid.NewGuid(), SpecializationId = 1 }));
        }

        [Fact]
        public async Task UTCID08_PreviewBookingAsync_ProfileNotFound_ThrowsNotFoundException()
        {
            var slot = new ExpertSlot { Id = Guid.NewGuid(), ExpertProfileId = Guid.NewGuid(), IsActive = true };
            var slotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            slotRepo.Setup(r => r.GetByIdAsync(slot.Id)).ReturnsAsync(slot);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(slotRepo.Object);

            var profileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            profileRepo.Setup(r => r.GetByIdAsync(slot.ExpertProfileId)).ReturnsAsync((ExpertProfile?)null);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(profileRepo.Object);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.PreviewBookingAsync(Guid.NewGuid(), new BookingPreviewRequest { ExpertSlotId = slot.Id, SpecializationId = 1 }));
        }

        [Fact]
        public async Task UTCID09_PreviewBookingAsync_SelfBooking_ThrowsBadRequestException()
        {
            var userId = Guid.NewGuid();
            var slot = new ExpertSlot { Id = Guid.NewGuid(), ExpertProfileId = Guid.NewGuid(), IsActive = true };
            var slotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            slotRepo.Setup(r => r.GetByIdAsync(slot.Id)).ReturnsAsync(slot);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(slotRepo.Object);

            var profile = new ExpertProfile { Id = slot.ExpertProfileId, UserId = userId };
            var profileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            profileRepo.Setup(r => r.GetByIdAsync(slot.ExpertProfileId)).ReturnsAsync(profile);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(profileRepo.Object);

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.PreviewBookingAsync(userId, new BookingPreviewRequest { ExpertSlotId = slot.Id, SpecializationId = 1 }));
            ex.ErrorCode.Should().Be("SELF_BOOKING_NOT_ALLOWED");
        }

        [Fact]
        public async Task UTCID10_PreviewBookingAsync_SpecNotFound_ThrowsNotFoundException()
        {
            var userId = Guid.NewGuid();
            var slot = new ExpertSlot { Id = Guid.NewGuid(), ExpertProfileId = Guid.NewGuid(), IsActive = true };
            var slotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            slotRepo.Setup(r => r.GetByIdAsync(slot.Id)).ReturnsAsync(slot);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(slotRepo.Object);

            var profile = new ExpertProfile { Id = slot.ExpertProfileId, UserId = Guid.NewGuid() };
            var profileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            profileRepo.Setup(r => r.GetByIdAsync(slot.ExpertProfileId)).ReturnsAsync(profile);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(profileRepo.Object);

            var specRepo = new Mock<IGenericRepository<Specialization>>();
            specRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Specialization, bool>>>()))
                    .ReturnsAsync(new List<Specialization>());
            _mockUnitOfWork.Setup(u => u.Repository<Specialization>()).Returns(specRepo.Object);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.PreviewBookingAsync(userId, new BookingPreviewRequest { ExpertSlotId = slot.Id, SpecializationId = 99 }));
        }

        [Fact]
        public async Task UTCID11_PreviewBookingAsync_Valid_ReturnsPreviewResponse()
        {
            var userId = Guid.NewGuid();
            var expertUserId = Guid.NewGuid();
            var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));
            var slot = new ExpertSlot
            {
                Id = Guid.NewGuid(),
                ExpertProfileId = Guid.NewGuid(),
                SlotDate = futureDate,
                StartTime = new TimeOnly(14, 0),
                EndTime = new TimeOnly(15, 0),
                SessionType = SessionType.ONLINE_MEETING,
                IsActive = true,
                IsBooked = false
            };
            var slotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            slotRepo.Setup(r => r.GetByIdAsync(slot.Id)).ReturnsAsync(slot);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(slotRepo.Object);

            var profile = new ExpertProfile { Id = slot.ExpertProfileId, UserId = expertUserId, JobTitle = "Chuyên gia thuế" };
            var profileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            profileRepo.Setup(r => r.GetByIdAsync(slot.ExpertProfileId)).ReturnsAsync(profile);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(profileRepo.Object);

            var spec = new Specialization { Id = 1, Name = "Thuế TNCN", IsActive = true };
            var specRepo = new Mock<IGenericRepository<Specialization>>();
            specRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Specialization, bool>>>()))
                    .ReturnsAsync(new List<Specialization> { spec });
            _mockUnitOfWork.Setup(u => u.Repository<Specialization>()).Returns(specRepo.Object);

            var userRepo = new Mock<IGenericRepository<User>>();
            userRepo.Setup(r => r.GetByIdAsync(expertUserId)).ReturnsAsync(new User { UserId = expertUserId, FullName = "Nguyen Van A" });
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(userRepo.Object);

            var bookingRepo = new Mock<IGenericRepository<Booking>>();
            bookingRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Booking, bool>>>())).ReturnsAsync(new List<Booking>());
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(bookingRepo.Object);

            var result = await _service.PreviewBookingAsync(userId, new BookingPreviewRequest { ExpertSlotId = slot.Id, SpecializationId = 1 });

            result.Should().NotBeNull();
            result.IsValid.Should().BeTrue();
            result.SpecializationName.Should().Be("Thuế TNCN");
            result.DurationMinutes.Should().Be(60);
        }

        #endregion

        #region CreateBookingAsync

        [Fact]
        public async Task UTCID12_CreateBookingAsync_UserNotFound_ThrowsUnauthorizedException()
        {
            var userRepo = new Mock<IGenericRepository<User>>();
            userRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((User?)null);
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(userRepo.Object);

            await Assert.ThrowsAsync<UnauthorizedException>(() =>
                _service.CreateBookingAsync(Guid.NewGuid(), new CreateBookingRequest()));
        }

        [Fact]
        public async Task UTCID13_CreateBookingAsync_SpecNotFound_ThrowsNotFoundException()
        {
            var userId = Guid.NewGuid();
            var userRepo = new Mock<IGenericRepository<User>>();
            userRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(new User { UserId = userId });
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(userRepo.Object);

            var specRepo = new Mock<IGenericRepository<Specialization>>();
            specRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Specialization, bool>>>()))
                    .ReturnsAsync(new List<Specialization>());
            _mockUnitOfWork.Setup(u => u.Repository<Specialization>()).Returns(specRepo.Object);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.CreateBookingAsync(userId, new CreateBookingRequest { SpecializationId = 99 }));
        }

        [Fact]
        public async Task UTCID14_CreateBookingAsync_SlotNotFound_ThrowsNotFoundException()
        {
            var userId = Guid.NewGuid();
            var userRepo = new Mock<IGenericRepository<User>>();
            userRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(new User { UserId = userId });
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(userRepo.Object);

            var spec = new Specialization { Id = 1, Name = "Thuế TNCN", IsActive = true };
            var specRepo = new Mock<IGenericRepository<Specialization>>();
            specRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Specialization, bool>>>()))
                    .ReturnsAsync(new List<Specialization> { spec });
            _mockUnitOfWork.Setup(u => u.Repository<Specialization>()).Returns(specRepo.Object);

            var slotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            slotRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((ExpertSlot?)null);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(slotRepo.Object);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.CreateBookingAsync(userId, new CreateBookingRequest { SpecializationId = 1, ExpertSlotId = Guid.NewGuid() }));
        }

        [Fact]
        public async Task UTCID15_CreateBookingAsync_SelfBooking_ThrowsBadRequestException()
        {
            var userId = Guid.NewGuid();
            var slotId = Guid.NewGuid();
            var profileId = Guid.NewGuid();

            var userRepo = new Mock<IGenericRepository<User>>();
            userRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(new User { UserId = userId });
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(userRepo.Object);

            var spec = new Specialization { Id = 1, Name = "Thuế TNCN", IsActive = true };
            var specRepo = new Mock<IGenericRepository<Specialization>>();
            specRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Specialization, bool>>>()))
                    .ReturnsAsync(new List<Specialization> { spec });
            _mockUnitOfWork.Setup(u => u.Repository<Specialization>()).Returns(specRepo.Object);

            var slot = new ExpertSlot { Id = slotId, ExpertProfileId = profileId, IsActive = true };
            var slotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            slotRepo.Setup(r => r.GetByIdAsync(slotId)).ReturnsAsync(slot);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(slotRepo.Object);

            var profile = new ExpertProfile { Id = profileId, UserId = userId };
            var profileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            profileRepo.Setup(r => r.GetByIdAsync(profileId)).ReturnsAsync(profile);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(profileRepo.Object);

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.CreateBookingAsync(userId, new CreateBookingRequest { SpecializationId = 1, ExpertSlotId = slotId }));
            ex.ErrorCode.Should().Be("SELF_BOOKING_NOT_ALLOWED");
        }

        [Fact]
        public async Task UTCID16_CreateBookingAsync_LeadTimeViolation_ThrowsBadRequestException()
        {
            var userId = Guid.NewGuid();
            var expertUserId = Guid.NewGuid();
            var slotId = Guid.NewGuid();
            var profileId = Guid.NewGuid();

            var userRepo = new Mock<IGenericRepository<User>>();
            userRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(new User { UserId = userId });
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(userRepo.Object);

            var spec = new Specialization { Id = 1, Name = "Thuế TNCN", IsActive = true };
            var specRepo = new Mock<IGenericRepository<Specialization>>();
            specRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Specialization, bool>>>()))
                    .ReturnsAsync(new List<Specialization> { spec });
            _mockUnitOfWork.Setup(u => u.Repository<Specialization>()).Returns(specRepo.Object);

            // Slot is only 1 hour from now -> violates 4 hour lead time
            var nowVn = DateTime.UtcNow.AddHours(7);
            var slot = new ExpertSlot
            {
                Id = slotId,
                ExpertProfileId = profileId,
                IsActive = true,
                SlotDate = DateOnly.FromDateTime(nowVn),
                StartTime = TimeOnly.FromDateTime(nowVn.AddHours(1)),
                EndTime = TimeOnly.FromDateTime(nowVn.AddHours(2))
            };
            var slotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            slotRepo.Setup(r => r.GetByIdAsync(slotId)).ReturnsAsync(slot);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(slotRepo.Object);

            var profile = new ExpertProfile { Id = profileId, UserId = expertUserId };
            var profileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            profileRepo.Setup(r => r.GetByIdAsync(profileId)).ReturnsAsync(profile);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(profileRepo.Object);

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.CreateBookingAsync(userId, new CreateBookingRequest { SpecializationId = 1, ExpertSlotId = slotId }));
            ex.ErrorCode.Should().Be("LEAD_TIME_VIOLATION");
        }

        [Fact]
        public async Task UTCID17_CreateBookingAsync_SlotAlreadyTaken_ThrowsConflictException()
        {
            var userId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();
            var expertUserId = Guid.NewGuid();
            var slotId = Guid.NewGuid();
            var profileId = Guid.NewGuid();

            var userRepo = new Mock<IGenericRepository<User>>();
            userRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(new User { UserId = userId });
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(userRepo.Object);

            var spec = new Specialization { Id = 1, Name = "Thuế TNCN", IsActive = true };
            var specRepo = new Mock<IGenericRepository<Specialization>>();
            specRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Specialization, bool>>>()))
                    .ReturnsAsync(new List<Specialization> { spec });
            _mockUnitOfWork.Setup(u => u.Repository<Specialization>()).Returns(specRepo.Object);

            var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));
            var slot = new ExpertSlot
            {
                Id = slotId,
                ExpertProfileId = profileId,
                IsActive = true,
                SlotDate = futureDate,
                StartTime = new TimeOnly(14, 0),
                EndTime = new TimeOnly(15, 0),
                HoldExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
                HoldUserId = otherUserId
            };
            var slotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            slotRepo.Setup(r => r.GetByIdAsync(slotId)).ReturnsAsync(slot);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(slotRepo.Object);

            var profile = new ExpertProfile { Id = profileId, UserId = expertUserId };
            var profileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            profileRepo.Setup(r => r.GetByIdAsync(profileId)).ReturnsAsync(profile);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(profileRepo.Object);

            var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                _service.CreateBookingAsync(userId, new CreateBookingRequest { SpecializationId = 1, ExpertSlotId = slotId }));
            ex.ErrorCode.Should().Be("SLOT_ALREADY_TAKEN");
        }

        [Fact]
        public async Task UTCID18_CreateBookingAsync_Valid_CreatesBookingAndHoldsSlot()
        {
            var userId = Guid.NewGuid();
            var expertUserId = Guid.NewGuid();
            var slotId = Guid.NewGuid();
            var profileId = Guid.NewGuid();

            var clientUser = new User { UserId = userId, FullName = "Client User", Email = "client@taxkeep.vn" };
            var expertUser = new User { UserId = expertUserId, FullName = "Expert User", Email = "expert@taxkeep.vn" };
            var userRepo = new Mock<IGenericRepository<User>>();
            userRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(clientUser);
            userRepo.Setup(r => r.GetByIdAsync(expertUserId)).ReturnsAsync(expertUser);
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(userRepo.Object);

            var spec = new Specialization { Id = 1, Name = "Thuế TNCN", IsActive = true };
            var specRepo = new Mock<IGenericRepository<Specialization>>();
            specRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Specialization, bool>>>()))
                    .ReturnsAsync(new List<Specialization> { spec });
            _mockUnitOfWork.Setup(u => u.Repository<Specialization>()).Returns(specRepo.Object);

            var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));
            var slot = new ExpertSlot
            {
                Id = slotId,
                ExpertProfileId = profileId,
                IsActive = true,
                SlotDate = futureDate,
                StartTime = new TimeOnly(14, 0),
                EndTime = new TimeOnly(15, 0),
                SessionType = SessionType.ONLINE_MEETING
            };
            var slotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            slotRepo.Setup(r => r.GetByIdAsync(slotId)).ReturnsAsync(slot);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(slotRepo.Object);

            var profile = new ExpertProfile { Id = profileId, UserId = expertUserId, JobTitle = "Chuyên gia tư vấn" };
            var profileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            profileRepo.Setup(r => r.GetByIdAsync(profileId)).ReturnsAsync(profile);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(profileRepo.Object);

            var bookingRepo = new Mock<IGenericRepository<Booking>>();
            bookingRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Booking, bool>>>())).ReturnsAsync(new List<Booking>());
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(bookingRepo.Object);

            var request = new CreateBookingRequest
            {
                ExpertSlotId = slotId,
                SpecializationId = 1,
                TopicTitle = "Tư vấn thuế hoàn thuế",
                ProblemDescription = "Cần tư vấn hồ sơ hoàn thuế TNCN năm 2024"
            };

            var result = await _service.CreateBookingAsync(userId, request);

            result.Should().NotBeNull();
            result.TopicTitle.Should().Be("Tư vấn thuế hoàn thuế");
            result.Status.Should().Be(BookingStatus.PENDING_PAYMENT);
            slot.HoldUserId.Should().Be(userId);
            slot.HoldExpiresAt.Should().NotBeNull();
            bookingRepo.Verify(r => r.AddAsync(It.IsAny<Booking>()), Times.Once);
            _mockUnitOfWork.Verify(u => u.CommitTransactionAsync(), Times.Once);
        }

        #endregion

        #region GetBookingByIdAsync

        [Fact]
        public async Task UTCID19_GetBookingByIdAsync_BookingNotFound_ThrowsNotFoundException()
        {
            var bookingRepo = new Mock<IGenericRepository<Booking>>();
            bookingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Booking?)null);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(bookingRepo.Object);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.GetBookingByIdAsync(Guid.NewGuid(), Guid.NewGuid(), "client"));
        }

        [Fact]
        public async Task UTCID20_GetBookingByIdAsync_SlotNotFound_ThrowsNotFoundException()
        {
            var bookingId = Guid.NewGuid();
            var booking = new Booking { Id = bookingId, ExpertSlotId = Guid.NewGuid() };
            var bookingRepo = new Mock<IGenericRepository<Booking>>();
            bookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(bookingRepo.Object);

            var slotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            slotRepo.Setup(r => r.GetByIdAsync(booking.ExpertSlotId)).ReturnsAsync((ExpertSlot?)null);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(slotRepo.Object);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.GetBookingByIdAsync(bookingId, Guid.NewGuid(), "client"));
        }

        [Fact]
        public async Task UTCID21_GetBookingByIdAsync_ProfileNotFound_ThrowsNotFoundException()
        {
            var bookingId = Guid.NewGuid();
            var slotId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var booking = new Booking { Id = bookingId, ExpertSlotId = slotId };
            var bookingRepo = new Mock<IGenericRepository<Booking>>();
            bookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(bookingRepo.Object);

            var slot = new ExpertSlot { Id = slotId, ExpertProfileId = profileId };
            var slotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            slotRepo.Setup(r => r.GetByIdAsync(slotId)).ReturnsAsync(slot);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(slotRepo.Object);

            var profileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            profileRepo.Setup(r => r.GetByIdAsync(profileId)).ReturnsAsync((ExpertProfile?)null);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(profileRepo.Object);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.GetBookingByIdAsync(bookingId, Guid.NewGuid(), "client"));
        }

        [Fact]
        public async Task UTCID22_GetBookingByIdAsync_ForbiddenUser_ThrowsForbiddenException()
        {
            var bookingId = Guid.NewGuid();
            var slotId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var clientUserId = Guid.NewGuid();
            var expertUserId = Guid.NewGuid();
            var strangerUserId = Guid.NewGuid();

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

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                _service.GetBookingByIdAsync(bookingId, strangerUserId, "client"));
        }

        [Fact]
        public async Task UTCID23_GetBookingByIdAsync_ValidClient_ReturnsDetailResponse()
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
                SpecializationId = 1,
                BookingCode = "BK-20250101-ABCDEF",
                Status = BookingStatus.CONFIRMED,
                TopicTitle = "Hỏi đáp thuế"
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
            userRepo.Setup(r => r.GetByIdAsync(clientUserId)).ReturnsAsync(new User { UserId = clientUserId, FullName = "Client" });
            userRepo.Setup(r => r.GetByIdAsync(expertUserId)).ReturnsAsync(new User { UserId = expertUserId, FullName = "Expert" });
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(userRepo.Object);

            var specRepo = new Mock<IGenericRepository<Specialization>>();
            specRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Specialization, bool>>>()))
                    .ReturnsAsync(new List<Specialization> { new Specialization { Id = 1, Name = "Thuế" } });
            _mockUnitOfWork.Setup(u => u.Repository<Specialization>()).Returns(specRepo.Object);

            var result = await _service.GetBookingByIdAsync(bookingId, clientUserId, "taxpayer");

            result.Should().NotBeNull();
            result.BookingCode.Should().Be("BK-20250101-ABCDEF");
            result.Status.Should().Be(BookingStatus.CONFIRMED);
        }

        #endregion

        #region GetMyBookingsAsync & GetExpertBookingsAsync

        [Fact]
        public async Task UTCID25_GetMyBookingsAsync_ReturnsPagedBookings()
        {
            var userId = Guid.NewGuid();
            var booking = new Booking
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ExpertSlotId = Guid.NewGuid(),
                Status = BookingStatus.CONFIRMED,
                CreatedAt = DateTimeOffset.UtcNow
            };

            var bookingRepo = new Mock<IGenericRepository<Booking>>();
            bookingRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Booking, bool>>>()))
                       .ReturnsAsync(new List<Booking> { booking });
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(bookingRepo.Object);

            var userRepo = new Mock<IGenericRepository<User>>();
            userRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                    .ReturnsAsync(new List<User> { new User { UserId = userId, FullName = "Client User" } });
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(userRepo.Object);

            var slotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            slotRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertSlot, bool>>>()))
                    .ReturnsAsync(new List<ExpertSlot>());
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(slotRepo.Object);

            var profileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            profileRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertProfile, bool>>>()))
                       .ReturnsAsync(new List<ExpertProfile>());
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(profileRepo.Object);

            var specRepo = new Mock<IGenericRepository<Specialization>>();
            specRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Specialization, bool>>>()))
                    .ReturnsAsync(new List<Specialization>());
            _mockUnitOfWork.Setup(u => u.Repository<Specialization>()).Returns(specRepo.Object);

            var result = await _service.GetMyBookingsAsync(userId, null, 1, 10);

            result.Should().NotBeNull();
            result.Items.Should().HaveCount(1);
            result.Pagination.TotalItems.Should().Be(1);
        }

        [Fact]
        public async Task UTCID26_GetExpertPendingBookingsAsync_ProfileNotFound_ThrowsNotFoundException()
        {
            var expertUserId = Guid.NewGuid();
            var profileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            profileRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertProfile, bool>>>()))
                       .ReturnsAsync(new List<ExpertProfile>());
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(profileRepo.Object);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.GetExpertPendingBookingsAsync(expertUserId, 1, 10));
        }

        [Fact]
        public async Task UTCID27_GetExpertPendingBookingsAsync_Valid_ReturnsPagedBookings()
        {
            var expertUserId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var slotId = Guid.NewGuid();

            var profile = new ExpertProfile { Id = profileId, UserId = expertUserId };
            var profileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            profileRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertProfile, bool>>>()))
                       .ReturnsAsync(new List<ExpertProfile> { profile });
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(profileRepo.Object);

            var slot = new ExpertSlot { Id = slotId, ExpertProfileId = profileId };
            var slotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            slotRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertSlot, bool>>>()))
                    .ReturnsAsync(new List<ExpertSlot> { slot });
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(slotRepo.Object);

            var booking = new Booking
            {
                Id = Guid.NewGuid(),
                ExpertSlotId = slotId,
                Status = BookingStatus.AWAITING_EXPERT_APPROVAL,
                ApprovalDeadline = DateTimeOffset.UtcNow.AddHours(1)
            };
            var bookingRepo = new Mock<IGenericRepository<Booking>>();
            bookingRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Booking, bool>>>()))
                       .ReturnsAsync(new List<Booking> { booking });
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(bookingRepo.Object);

            var userRepo = new Mock<IGenericRepository<User>>();
            userRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                    .ReturnsAsync(new List<User>());
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(userRepo.Object);

            var specRepo = new Mock<IGenericRepository<Specialization>>();
            specRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Specialization, bool>>>()))
                    .ReturnsAsync(new List<Specialization>());
            _mockUnitOfWork.Setup(u => u.Repository<Specialization>()).Returns(specRepo.Object);

            var result = await _service.GetExpertPendingBookingsAsync(expertUserId, 1, 10);

            result.Should().NotBeNull();
            result.Items.Should().HaveCount(1);
        }

        [Fact]
        public async Task UTCID28_GetExpertBookingsAsync_Valid_ReturnsPagedBookings()
        {
            var expertUserId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var slotId = Guid.NewGuid();

            var profile = new ExpertProfile { Id = profileId, UserId = expertUserId };
            var profileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            profileRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertProfile, bool>>>()))
                       .ReturnsAsync(new List<ExpertProfile> { profile });
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(profileRepo.Object);

            var slot = new ExpertSlot { Id = slotId, ExpertProfileId = profileId };
            var slotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            slotRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertSlot, bool>>>()))
                    .ReturnsAsync(new List<ExpertSlot> { slot });
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(slotRepo.Object);

            var booking = new Booking { Id = Guid.NewGuid(), ExpertSlotId = slotId, Status = BookingStatus.CONFIRMED };
            var bookingRepo = new Mock<IGenericRepository<Booking>>();
            bookingRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Booking, bool>>>()))
                       .ReturnsAsync(new List<Booking> { booking });
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(bookingRepo.Object);

            var userRepo = new Mock<IGenericRepository<User>>();
            userRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                    .ReturnsAsync(new List<User>());
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(userRepo.Object);

            var specRepo = new Mock<IGenericRepository<Specialization>>();
            specRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Specialization, bool>>>()))
                    .ReturnsAsync(new List<Specialization>());
            _mockUnitOfWork.Setup(u => u.Repository<Specialization>()).Returns(specRepo.Object);

            var result = await _service.GetExpertBookingsAsync(expertUserId, null, 1, 10);

            result.Should().NotBeNull();
            result.Items.Should().HaveCount(1);
        }

        #endregion

        #region AcceptBookingAsync & RejectBookingAsync

        [Fact]
        public async Task UTCID30_AcceptBookingAsync_InvalidStatus_ThrowsBadRequestException()
        {
            var bookingId = Guid.NewGuid();
            var slotId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var expertUserId = Guid.NewGuid();

            var booking = new Booking { Id = bookingId, ExpertSlotId = slotId, Status = BookingStatus.CONFIRMED };
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
                _service.AcceptBookingAsync(expertUserId, bookingId));
            ex.ErrorCode.Should().Be("INVALID_STATUS");
        }

        [Fact]
        public async Task UTCID31_AcceptBookingAsync_Timeout_ThrowsBadRequestException()
        {
            var bookingId = Guid.NewGuid();
            var slotId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var expertUserId = Guid.NewGuid();

            var booking = new Booking
            {
                Id = bookingId,
                ExpertSlotId = slotId,
                Status = BookingStatus.AWAITING_EXPERT_APPROVAL,
                PaidAt = DateTimeOffset.UtcNow.AddHours(-3),
                ApprovalDeadline = DateTimeOffset.UtcNow.AddMinutes(-5) // expired
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

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.AcceptBookingAsync(expertUserId, bookingId));
            ex.ErrorCode.Should().Be("APPROVAL_TIMEOUT");
        }

        [Fact]
        public async Task UTCID32_AcceptBookingAsync_Valid_ConfirmsAndSendsNotification()
        {
            var bookingId = Guid.NewGuid();
            var slotId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var expertUserId = Guid.NewGuid();
            var clientUserId = Guid.NewGuid();

            var booking = new Booking
            {
                Id = bookingId,
                UserId = clientUserId,
                ExpertSlotId = slotId,
                SpecializationId = 1,
                Status = BookingStatus.AWAITING_EXPERT_APPROVAL,
                PaidAt = DateTimeOffset.UtcNow.AddHours(-1),
                ApprovalDeadline = DateTimeOffset.UtcNow.AddHours(1),
                BookingCode = "BK-20250101-ACCEPT"
            };
            var bookingRepo = new Mock<IGenericRepository<Booking>>();
            bookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(bookingRepo.Object);

            var slot = new ExpertSlot
            {
                Id = slotId,
                ExpertProfileId = profileId,
                SlotDate = new DateOnly(2025, 5, 1),
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

            var userRepo = new Mock<IGenericRepository<User>>();
            userRepo.Setup(r => r.GetByIdAsync(clientUserId)).ReturnsAsync(new User { UserId = clientUserId, FullName = "Client" });
            userRepo.Setup(r => r.GetByIdAsync(expertUserId)).ReturnsAsync(new User { UserId = expertUserId, FullName = "Expert" });
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(userRepo.Object);

            var specRepo = new Mock<IGenericRepository<Specialization>>();
            specRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Specialization, bool>>>()))
                    .ReturnsAsync(new List<Specialization> { new Specialization { Id = 1, Name = "Thuế" } });
            _mockUnitOfWork.Setup(u => u.Repository<Specialization>()).Returns(specRepo.Object);

            var result = await _service.AcceptBookingAsync(expertUserId, bookingId);

            result.Should().NotBeNull();
            booking.Status.Should().Be(BookingStatus.CONFIRMED);
            booking.ApprovedAt.Should().NotBeNull();
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task UTCID36_RejectBookingAsync_Valid_RejectsAndRefunds100Percent()
        {
            var bookingId = Guid.NewGuid();
            var slotId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var expertUserId = Guid.NewGuid();
            var clientUserId = Guid.NewGuid();

            var booking = new Booking
            {
                Id = bookingId,
                UserId = clientUserId,
                ExpertSlotId = slotId,
                SpecializationId = 1,
                Fee = 500000m,
                Status = BookingStatus.AWAITING_EXPERT_APPROVAL,
                BookingCode = "BK-20250101-REJECT"
            };
            var bookingRepo = new Mock<IGenericRepository<Booking>>();
            bookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(bookingRepo.Object);

            var slot = new ExpertSlot
            {
                Id = slotId,
                ExpertProfileId = profileId,
                SlotDate = new DateOnly(2025, 5, 1),
                StartTime = new TimeOnly(14, 0),
                EndTime = new TimeOnly(15, 0),
                IsBooked = true
            };
            var slotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            slotRepo.Setup(r => r.GetByIdAsync(slotId)).ReturnsAsync(slot);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(slotRepo.Object);

            var profile = new ExpertProfile { Id = profileId, UserId = expertUserId };
            var profileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            profileRepo.Setup(r => r.GetByIdAsync(profileId)).ReturnsAsync(profile);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(profileRepo.Object);

            var escrow = new EscrowTransaction { Id = Guid.NewGuid(), BookingId = bookingId, Amount = 500000m, EscrowStatus = EscrowStatus.HeldInEscrow };
            var escrowRepo = new Mock<IGenericRepository<EscrowTransaction>>();
            escrowRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<EscrowTransaction, bool>>>()))
                      .ReturnsAsync(new List<EscrowTransaction> { escrow });
            _mockUnitOfWork.Setup(u => u.Repository<EscrowTransaction>()).Returns(escrowRepo.Object);

            var userRepo = new Mock<IGenericRepository<User>>();
            userRepo.Setup(r => r.GetByIdAsync(clientUserId)).ReturnsAsync(new User { UserId = clientUserId, FullName = "Client" });
            userRepo.Setup(r => r.GetByIdAsync(expertUserId)).ReturnsAsync(new User { UserId = expertUserId, FullName = "Expert" });
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(userRepo.Object);

            var specRepo = new Mock<IGenericRepository<Specialization>>();
            specRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Specialization, bool>>>()))
                    .ReturnsAsync(new List<Specialization> { new Specialization { Id = 1, Name = "Thuế" } });
            _mockUnitOfWork.Setup(u => u.Repository<Specialization>()).Returns(specRepo.Object);

            var request = new RejectBookingRequest { Reason = "Bận việc đột xuất không thể nhận ca." };
            var result = await _service.RejectBookingAsync(expertUserId, bookingId, request);

            result.Should().NotBeNull();
            booking.Status.Should().Be(BookingStatus.REJECTED_BY_EXPERT);
            booking.RefundStatus.Should().Be("PENDING_REFUND");
            slot.IsBooked.Should().BeFalse();
            escrow.EscrowStatus.Should().Be(EscrowStatus.Refunded);
            escrow.RefundAmount.Should().Be(500000m);
        }

        #endregion

        #region CancelBookingAsync

        [Fact]
        public async Task UTCID38_CancelBookingAsync_ForbiddenUser_ThrowsForbiddenException()
        {
            var bookingId = Guid.NewGuid();
            var clientUserId = Guid.NewGuid();
            var strangerUserId = Guid.NewGuid();

            var booking = new Booking { Id = bookingId, UserId = clientUserId };
            var bookingRepo = new Mock<IGenericRepository<Booking>>();
            bookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(bookingRepo.Object);

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                _service.CancelBookingAsync(strangerUserId, bookingId, "Cancel"));
        }

        [Fact]
        public async Task UTCID39_CancelBookingAsync_InvalidStatus_ThrowsBadRequestException()
        {
            var bookingId = Guid.NewGuid();
            var clientUserId = Guid.NewGuid();

            var booking = new Booking { Id = bookingId, UserId = clientUserId, Status = BookingStatus.COMPLETED };
            var bookingRepo = new Mock<IGenericRepository<Booking>>();
            bookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(bookingRepo.Object);

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.CancelBookingAsync(clientUserId, bookingId, "Cancel"));
            ex.ErrorCode.Should().Be("CANNOT_CANCEL");
        }

        [Fact]
        public async Task UTCID40_CancelBookingAsync_Valid_FullRefund()
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
                SpecializationId = 1,
                Status = BookingStatus.CONFIRMED,
                PaidAt = DateTimeOffset.UtcNow.AddDays(-2),
                BookingCode = "BK-20250101-CANCEL-FULL"
            };
            var bookingRepo = new Mock<IGenericRepository<Booking>>();
            bookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(bookingRepo.Object);

            // Slot is 3 days in the future (>= 24h -> full refund)
            var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3));
            var slot = new ExpertSlot
            {
                Id = slotId,
                ExpertProfileId = profileId,
                SlotDate = futureDate,
                StartTime = new TimeOnly(14, 0),
                EndTime = new TimeOnly(15, 0),
                IsBooked = true
            };
            var slotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            slotRepo.Setup(r => r.GetByIdAsync(slotId)).ReturnsAsync(slot);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(slotRepo.Object);

            var profile = new ExpertProfile { Id = profileId, UserId = expertUserId };
            var profileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            profileRepo.Setup(r => r.GetByIdAsync(profileId)).ReturnsAsync(profile);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(profileRepo.Object);

            var escrow = new EscrowTransaction { Id = Guid.NewGuid(), BookingId = bookingId, Amount = 1000000m, EscrowStatus = EscrowStatus.HeldInEscrow };
            var escrowRepo = new Mock<IGenericRepository<EscrowTransaction>>();
            escrowRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<EscrowTransaction, bool>>>()))
                      .ReturnsAsync(new List<EscrowTransaction> { escrow });
            _mockUnitOfWork.Setup(u => u.Repository<EscrowTransaction>()).Returns(escrowRepo.Object);

            var userRepo = new Mock<IGenericRepository<User>>();
            userRepo.Setup(r => r.GetByIdAsync(clientUserId)).ReturnsAsync(new User { UserId = clientUserId, FullName = "Client" });
            userRepo.Setup(r => r.GetByIdAsync(expertUserId)).ReturnsAsync(new User { UserId = expertUserId, FullName = "Expert" });
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(userRepo.Object);

            var specRepo = new Mock<IGenericRepository<Specialization>>();
            specRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Specialization, bool>>>()))
                    .ReturnsAsync(new List<Specialization> { new Specialization { Id = 1, Name = "Thuế" } });
            _mockUnitOfWork.Setup(u => u.Repository<Specialization>()).Returns(specRepo.Object);

            var result = await _service.CancelBookingAsync(clientUserId, bookingId, "Có việc gia đình");

            result.Should().NotBeNull();
            booking.Status.Should().Be(BookingStatus.CANCELLED_BY_USER);
            booking.RefundStatus.Should().Be("REFUNDED");
            escrow.EscrowStatus.Should().Be(EscrowStatus.Refunded);
            escrow.RefundAmount.Should().Be(1000000m);
        }

        #endregion

        #region ExpertCancelBookingAsync

        [Fact]
        public async Task UTCID44_ExpertCancelBookingAsync_ForbiddenExpert_ThrowsForbiddenException()
        {
            var bookingId = Guid.NewGuid();
            var slotId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var expertUserId = Guid.NewGuid();
            var strangerUserId = Guid.NewGuid();

            var booking = new Booking { Id = bookingId, ExpertSlotId = slotId };
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

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                _service.ExpertCancelBookingAsync(strangerUserId, bookingId, "Cancel"));
        }

        [Fact]
        public async Task UTCID45_ExpertCancelBookingAsync_InvalidStatus_ThrowsBadRequestException()
        {
            var bookingId = Guid.NewGuid();
            var slotId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var expertUserId = Guid.NewGuid();

            var booking = new Booking { Id = bookingId, ExpertSlotId = slotId, Status = BookingStatus.PENDING_PAYMENT };
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
                _service.ExpertCancelBookingAsync(expertUserId, bookingId, "Cancel"));
            ex.ErrorCode.Should().Be("CANNOT_CANCEL");
        }

        [Fact]
        public async Task UTCID46_ExpertCancelBookingAsync_LateCancel_AppliesPenalty()
        {
            var bookingId = Guid.NewGuid();
            var slotId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var expertUserId = Guid.NewGuid();
            var clientUserId = Guid.NewGuid();

            var booking = new Booking
            {
                Id = bookingId,
                UserId = clientUserId,
                ExpertSlotId = slotId,
                SpecializationId = 1,
                Status = BookingStatus.CONFIRMED,
                BookingCode = "BK-20250101-EXPERT-LATE"
            };
            var bookingRepo = new Mock<IGenericRepository<Booking>>();
            bookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(bookingRepo.Object);

            // Slot is only 6 hours from now (< 12h -> penalty applies)
            var nowVn = DateTime.UtcNow.AddHours(7);
            var slot = new ExpertSlot
            {
                Id = slotId,
                ExpertProfileId = profileId,
                SlotDate = DateOnly.FromDateTime(nowVn),
                StartTime = TimeOnly.FromDateTime(nowVn.AddHours(6)),
                EndTime = TimeOnly.FromDateTime(nowVn.AddHours(7)),
                IsBooked = true
            };
            var slotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            slotRepo.Setup(r => r.GetByIdAsync(slotId)).ReturnsAsync(slot);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(slotRepo.Object);

            var profile = new ExpertProfile { Id = profileId, UserId = expertUserId };
            var profileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            profileRepo.Setup(r => r.GetByIdAsync(profileId)).ReturnsAsync(profile);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(profileRepo.Object);

            var escrow = new EscrowTransaction { Id = Guid.NewGuid(), BookingId = bookingId, Amount = 500000m, EscrowStatus = EscrowStatus.HeldInEscrow };
            var escrowRepo = new Mock<IGenericRepository<EscrowTransaction>>();
            escrowRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<EscrowTransaction, bool>>>()))
                      .ReturnsAsync(new List<EscrowTransaction> { escrow });
            _mockUnitOfWork.Setup(u => u.Repository<EscrowTransaction>()).Returns(escrowRepo.Object);

            var expertWallet = new UserWallet { Id = Guid.NewGuid(), UserId = expertUserId, WalletType = "EXPERT", AvailableBalance = 500000m };
            var walletRepo = new Mock<IGenericRepository<UserWallet>>();
            walletRepo.Setup(r => r.FindAsync(It.Is<Expression<Func<UserWallet, bool>>>(e => e.ToString().Contains("EXPERT"))))
                      .ReturnsAsync(new List<UserWallet> { expertWallet });
            _mockUnitOfWork.Setup(u => u.Repository<UserWallet>()).Returns(walletRepo.Object);

            var userRepo = new Mock<IGenericRepository<User>>();
            userRepo.Setup(r => r.GetByIdAsync(clientUserId)).ReturnsAsync(new User { UserId = clientUserId, FullName = "Client" });
            userRepo.Setup(r => r.GetByIdAsync(expertUserId)).ReturnsAsync(new User { UserId = expertUserId, FullName = "Expert" });
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(userRepo.Object);

            var specRepo = new Mock<IGenericRepository<Specialization>>();
            specRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Specialization, bool>>>()))
                    .ReturnsAsync(new List<Specialization> { new Specialization { Id = 1, Name = "Thuế" } });
            _mockUnitOfWork.Setup(u => u.Repository<Specialization>()).Returns(specRepo.Object);

            var result = await _service.ExpertCancelBookingAsync(expertUserId, bookingId, "Sự cố đột xuất");

            result.Should().NotBeNull();
            booking.Status.Should().Be(BookingStatus.CANCELLED_BY_EXPERT);
            booking.RefundStatus.Should().Be("REFUNDED");
            expertWallet.AvailableBalance.Should().Be(400000m); // 500k - 100k penalty
        }

        #endregion
    }
}
