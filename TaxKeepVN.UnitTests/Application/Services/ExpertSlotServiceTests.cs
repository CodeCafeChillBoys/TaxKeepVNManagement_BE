using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TaxKeepVN.Application.DTOs.Experts;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Implementations;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Domain.IRepositories;
using Xunit;

namespace TaxKeepVN.UnitTests.Application.Services
{
    public class ExpertSlotServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<ILogger<ExpertSlotService>> _mockLogger;
        private readonly Mock<IGenericRepository<ExpertProfile>> _mockProfileRepo;
        private readonly Mock<IGenericRepository<ExpertSlot>> _mockSlotRepo;
        private readonly Mock<IGenericRepository<ConsultationPlatformPolicy>> _mockPolicyRepo;
        private readonly ExpertSlotService _service;

        public ExpertSlotServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockLogger = new Mock<ILogger<ExpertSlotService>>();
            _mockProfileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            _mockSlotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            _mockPolicyRepo = new Mock<IGenericRepository<ConsultationPlatformPolicy>>();

            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(_mockProfileRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(_mockSlotRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<ConsultationPlatformPolicy>()).Returns(_mockPolicyRepo.Object);

            _service = new ExpertSlotService(_mockUnitOfWork.Object, _mockLogger.Object);
        }

        #region GetMySlotsAsync

        [Fact]
        public async Task UTCID01_GetMySlotsAsync_ProfileNotFound_ThrowsForbiddenException()
        {
            _mockProfileRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertProfile, bool>>>()))
                            .ReturnsAsync(new List<ExpertProfile>());

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                _service.GetMySlotsAsync(Guid.NewGuid(), new ExpertSlotFilterParameters()));
        }

        [Fact]
        public async Task UTCID02_GetMySlotsAsync_Success_ReturnsSlots()
        {
            var expertUserId = Guid.NewGuid();
            var profile = new ExpertProfile { Id = Guid.NewGuid(), UserId = expertUserId };

            _mockProfileRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertProfile, bool>>>()))
                            .ReturnsAsync(new List<ExpertProfile> { profile });

            var slots = new List<ExpertSlot>
            {
                new ExpertSlot
                {
                    Id = Guid.NewGuid(),
                    ExpertProfileId = profile.Id,
                    SlotDate = DateOnly.FromDateTime(DateTime.Today),
                    StartTime = new TimeOnly(9, 0),
                    EndTime = new TimeOnly(10, 0),
                    IsActive = true
                }
            };

            _mockSlotRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertSlot, bool>>>()))
                         .ReturnsAsync(slots);

            var result = await _service.GetMySlotsAsync(expertUserId, new ExpertSlotFilterParameters());

            result.Should().HaveCount(1);
            result[0].StartTime.Should().Be(new TimeOnly(9, 0));
        }

        #endregion

        #region CreateSlotAsync

        [Fact]
        public async Task UTCID03_CreateSlotAsync_SlotOverlap_ThrowsConflictException()
        {
            var expertUserId = Guid.NewGuid();
            var profile = new ExpertProfile { Id = Guid.NewGuid(), UserId = expertUserId };

            _mockProfileRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertProfile, bool>>>()))
                            .ReturnsAsync(new List<ExpertProfile> { profile });

            _mockPolicyRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationPlatformPolicy, bool>>>()))
                           .ReturnsAsync(new List<ConsultationPlatformPolicy>
                           {
                               new ConsultationPlatformPolicy { Id = 1, IsActive = true, ExpertMaxDailySlots = 6 }
                           });

            var existingSlot = new ExpertSlot
            {
                ExpertProfileId = profile.Id,
                SlotDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(10, 0)
            };

            _mockSlotRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertSlot, bool>>>()))
                         .ReturnsAsync(new List<ExpertSlot> { existingSlot });

            var request = new CreateExpertSlotRequest
            {
                SlotDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
                StartTime = new TimeOnly(9, 30), // Overlaps with 9:00 - 10:00
                EndTime = new TimeOnly(10, 30),
                SessionType = SessionType.ONLINE_MEETING
            };

            var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                _service.CreateSlotAsync(expertUserId, request));
            ex.ErrorCode.Should().Be("SLOT_OVERLAP");
        }

        [Fact]
        public async Task UTCID04_CreateSlotAsync_Valid_CreatesSuccessfully()
        {
            var expertUserId = Guid.NewGuid();
            var profile = new ExpertProfile { Id = Guid.NewGuid(), UserId = expertUserId };

            _mockProfileRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertProfile, bool>>>()))
                            .ReturnsAsync(new List<ExpertProfile> { profile });

            _mockPolicyRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationPlatformPolicy, bool>>>()))
                           .ReturnsAsync(new List<ConsultationPlatformPolicy>
                           {
                               new ConsultationPlatformPolicy { Id = 1, IsActive = true, ExpertMaxDailySlots = 6 }
                           });

            _mockSlotRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertSlot, bool>>>()))
                         .ReturnsAsync(new List<ExpertSlot>());

            var request = new CreateExpertSlotRequest
            {
                SlotDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
                StartTime = new TimeOnly(14, 0),
                EndTime = new TimeOnly(15, 0),
                SessionType = SessionType.ONLINE_MEETING
            };

            var result = await _service.CreateSlotAsync(expertUserId, request);

            result.Should().NotBeNull();
            result.StartTime.Should().Be(new TimeOnly(14, 0));
            _mockSlotRepo.Verify(r => r.AddAsync(It.IsAny<ExpertSlot>()), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion

        #region DeleteSlotAsync

        [Fact]
        public async Task UTCID05_DeleteSlotAsync_AlreadyBooked_ThrowsBadRequestException()
        {
            var expertUserId = Guid.NewGuid();
            var profile = new ExpertProfile { Id = Guid.NewGuid(), UserId = expertUserId };
            var slot = new ExpertSlot { Id = Guid.NewGuid(), ExpertProfileId = profile.Id, IsBooked = true };

            _mockProfileRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertProfile, bool>>>()))
                            .ReturnsAsync(new List<ExpertProfile> { profile });
            _mockSlotRepo.Setup(r => r.GetByIdAsync(slot.Id)).ReturnsAsync(slot);

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.DeleteSlotAsync(expertUserId, slot.Id));
            ex.ErrorCode.Should().Be("SLOT_ALREADY_BOOKED");
        }

        #endregion

        #region GetSlotByIdAsync

        [Fact]
        public async Task UTCID06_GetSlotByIdAsync_NotFoundAndForbiddenAndSuccess()
        {
            var expertUserId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var profile = new ExpertProfile { Id = profileId, UserId = expertUserId };

            _mockProfileRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertProfile, bool>>>()))
                            .ReturnsAsync(new List<ExpertProfile> { profile });

            // Not found
            _mockSlotRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((ExpertSlot?)null);
            await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.GetSlotByIdAsync(expertUserId, Guid.NewGuid()));

            // Forbidden (different expert)
            var otherSlot = new ExpertSlot { Id = Guid.NewGuid(), ExpertProfileId = Guid.NewGuid() };
            _mockSlotRepo.Setup(r => r.GetByIdAsync(otherSlot.Id)).ReturnsAsync(otherSlot);
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                _service.GetSlotByIdAsync(expertUserId, otherSlot.Id));

            // Success
            var mySlot = new ExpertSlot { Id = Guid.NewGuid(), ExpertProfileId = profileId, SlotDate = DateOnly.FromDateTime(DateTime.Today), StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(11, 0) };
            _mockSlotRepo.Setup(r => r.GetByIdAsync(mySlot.Id)).ReturnsAsync(mySlot);
            var res = await _service.GetSlotByIdAsync(expertUserId, mySlot.Id);
            res.Should().NotBeNull();
            res.StartTime.Should().Be(new TimeOnly(10, 0));
        }

        #endregion

        #region UpdateSlotAsync

        [Fact]
        public async Task UTCID07_UpdateSlotAsync_ValidationsAndSuccess()
        {
            var expertUserId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var profile = new ExpertProfile { Id = profileId, UserId = expertUserId };

            _mockProfileRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertProfile, bool>>>()))
                            .ReturnsAsync(new List<ExpertProfile> { profile });

            var slotId = Guid.NewGuid();
            var slot = new ExpertSlot
            {
                Id = slotId,
                ExpertProfileId = profileId,
                SlotDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(10, 0),
                IsBooked = false
            };
            _mockSlotRepo.Setup(r => r.GetByIdAsync(slotId)).ReturnsAsync(slot);

            _mockPolicyRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationPlatformPolicy, bool>>>()))
                           .ReturnsAsync(new List<ConsultationPlatformPolicy> { new ConsultationPlatformPolicy { ExpertMaxDailySlots = 6 } });

            // 1. IsBooked validation
            slot.IsBooked = true;
            var exBooked = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.UpdateSlotAsync(expertUserId, slotId, new UpdateExpertSlotRequest { SlotDate = slot.SlotDate, StartTime = slot.StartTime, EndTime = slot.EndTime }));
            exBooked.ErrorCode.Should().Be("SLOT_ALREADY_BOOKED");
            slot.IsBooked = false;

            // 2. Holding validation
            slot.HoldExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10);
            var exHold = await Assert.ThrowsAsync<ConflictException>(() =>
                _service.UpdateSlotAsync(expertUserId, slotId, new UpdateExpertSlotRequest { SlotDate = slot.SlotDate, StartTime = slot.StartTime, EndTime = slot.EndTime }));
            exHold.ErrorCode.Should().Be("SLOT_HOLDING");
            slot.HoldExpiresAt = null;

            // 3. Max daily slots when changing date
            var newDate = DateOnly.FromDateTime(DateTime.Today.AddDays(2));
            var existingSlotsNewDate = new List<ExpertSlot>
            {
                new ExpertSlot { Id = Guid.NewGuid(), SlotDate = newDate },
                new ExpertSlot { Id = Guid.NewGuid(), SlotDate = newDate },
                new ExpertSlot { Id = Guid.NewGuid(), SlotDate = newDate },
                new ExpertSlot { Id = Guid.NewGuid(), SlotDate = newDate },
                new ExpertSlot { Id = Guid.NewGuid(), SlotDate = newDate },
                new ExpertSlot { Id = Guid.NewGuid(), SlotDate = newDate }
            };
            _mockSlotRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertSlot, bool>>>()))
                         .ReturnsAsync(existingSlotsNewDate);

            var exMax = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.UpdateSlotAsync(expertUserId, slotId, new UpdateExpertSlotRequest { SlotDate = newDate, StartTime = new TimeOnly(14, 0), EndTime = new TimeOnly(15, 0) }));
            exMax.ErrorCode.Should().Be("MAX_DAILY_SLOTS_EXCEEDED");

            // 4. Overlap validation
            var existingConflict = new List<ExpertSlot>
            {
                new ExpertSlot { Id = Guid.NewGuid(), SlotDate = slot.SlotDate, StartTime = new TimeOnly(14, 0), EndTime = new TimeOnly(15, 0) }
            };
            _mockSlotRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertSlot, bool>>>()))
                         .ReturnsAsync(existingConflict);

            var exOverlap = await Assert.ThrowsAsync<ConflictException>(() =>
                _service.UpdateSlotAsync(expertUserId, slotId, new UpdateExpertSlotRequest { SlotDate = slot.SlotDate, StartTime = new TimeOnly(14, 30), EndTime = new TimeOnly(15, 30) }));
            exOverlap.ErrorCode.Should().Be("SLOT_OVERLAP");

            // 5. Success
            _mockSlotRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertSlot, bool>>>()))
                         .ReturnsAsync(new List<ExpertSlot>());

            var result = await _service.UpdateSlotAsync(expertUserId, slotId, new UpdateExpertSlotRequest
            {
                SlotDate = slot.SlotDate,
                StartTime = new TimeOnly(15, 0),
                EndTime = new TimeOnly(16, 0),
                SessionType = SessionType.ONLINE_MEETING,
                IsActive = true
            });

            result.Should().NotBeNull();
            result.StartTime.Should().Be(new TimeOnly(15, 0));
            slot.StartTime.Should().Be(new TimeOnly(15, 0));
        }

        #endregion

        #region ToggleSlotActiveAsync

        [Fact]
        public async Task UTCID08_ToggleSlotActiveAsync_ValidationsAndSuccess()
        {
            var expertUserId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var profile = new ExpertProfile { Id = profileId, UserId = expertUserId };

            _mockProfileRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertProfile, bool>>>()))
                            .ReturnsAsync(new List<ExpertProfile> { profile });

            var slotId = Guid.NewGuid();
            var slot = new ExpertSlot
            {
                Id = slotId,
                ExpertProfileId = profileId,
                SlotDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
                IsActive = true
            };
            _mockSlotRepo.Setup(r => r.GetByIdAsync(slotId)).ReturnsAsync(slot);

            // IsBooked
            slot.IsBooked = true;
            var exBooked = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.ToggleSlotActiveAsync(expertUserId, slotId));
            exBooked.ErrorCode.Should().Be("SLOT_ALREADY_BOOKED");
            slot.IsBooked = false;

            // Holding
            slot.HoldExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5);
            var exHold = await Assert.ThrowsAsync<ConflictException>(() =>
                _service.ToggleSlotActiveAsync(expertUserId, slotId));
            exHold.ErrorCode.Should().Be("SLOT_HOLDING");
            slot.HoldExpiresAt = null;

            // Success toggle to false
            var resFalse = await _service.ToggleSlotActiveAsync(expertUserId, slotId);
            resFalse.IsActive.Should().BeFalse();
            slot.IsActive.Should().BeFalse();

            // Toggle back to true
            var resTrue = await _service.ToggleSlotActiveAsync(expertUserId, slotId);
            resTrue.IsActive.Should().BeTrue();
            slot.IsActive.Should().BeTrue();
        }

        #endregion

        #region DeleteSlotAsync Holding

        [Fact]
        public async Task UTCID09_DeleteSlotAsync_HoldingAndSuccess()
        {
            var expertUserId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var profile = new ExpertProfile { Id = profileId, UserId = expertUserId };

            _mockProfileRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertProfile, bool>>>()))
                            .ReturnsAsync(new List<ExpertProfile> { profile });

            var slotId = Guid.NewGuid();
            var slot = new ExpertSlot
            {
                Id = slotId,
                ExpertProfileId = profileId,
                SlotDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
                HoldExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5)
            };
            _mockSlotRepo.Setup(r => r.GetByIdAsync(slotId)).ReturnsAsync(slot);

            var exHold = await Assert.ThrowsAsync<ConflictException>(() =>
                _service.DeleteSlotAsync(expertUserId, slotId));
            exHold.ErrorCode.Should().Be("SLOT_HOLDING");

            // Success delete
            slot.HoldExpiresAt = null;
            await _service.DeleteSlotAsync(expertUserId, slotId);
            _mockSlotRepo.Verify(r => r.Remove(slot), Times.Once);
        }

        #endregion

        #region CreateRecurringSlotsAsync

        [Fact]
        public async Task UTCID10_CreateRecurringSlotsAsync_SuccessAndSkipping()
        {
            var expertUserId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var profile = new ExpertProfile { Id = profileId, UserId = expertUserId };

            _mockProfileRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertProfile, bool>>>()))
                            .ReturnsAsync(new List<ExpertProfile> { profile });

            _mockPolicyRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationPlatformPolicy, bool>>>()))
                           .ReturnsAsync(new List<ConsultationPlatformPolicy> { new ConsultationPlatformPolicy { ExpertMaxDailySlots = 6 } });

            var existingSlot = new ExpertSlot
            {
                Id = Guid.NewGuid(),
                ExpertProfileId = profileId,
                SlotDate = DateOnly.FromDateTime(DateTime.Today.AddDays(7)),
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(10, 0)
            };
            _mockSlotRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertSlot, bool>>>()))
                         .ReturnsAsync(new List<ExpertSlot> { existingSlot });

            var startDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
            var request = new CreateRecurringSlotRequest
            {
                StartDate = startDate,
                NumberOfWeeks = 2,
                WeeklySlots = new List<WeeklySlotItemDto>
                {
                    new WeeklySlotItemDto
                    {
                        DayOfWeek = startDate.DayOfWeek,
                        StartTime = new TimeOnly(9, 0),
                        EndTime = new TimeOnly(10, 0),
                        SessionType = SessionType.ONLINE_MEETING
                    },
                    new WeeklySlotItemDto
                    {
                        DayOfWeek = startDate.DayOfWeek,
                        StartTime = new TimeOnly(14, 0),
                        EndTime = new TimeOnly(15, 0),
                        SessionType = SessionType.ONLINE_MEETING
                    }
                }
            };

            var result = await _service.CreateRecurringSlotsAsync(expertUserId, request);

            result.Should().NotBeNull();
            result.TotalCreated.Should().BeGreaterThan(0);
        }

        #endregion

        #region EmergencyCancelSlotAsync

        [Fact]
        public async Task UTCID11_EmergencyCancelSlotAsync_Validations()
        {
            var expertUserId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var profile = new ExpertProfile { Id = profileId, UserId = expertUserId };

            _mockProfileRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertProfile, bool>>>()))
                            .ReturnsAsync(new List<ExpertProfile> { profile });

            var slotId = Guid.NewGuid();
            var slot = new ExpertSlot
            {
                Id = slotId,
                ExpertProfileId = profileId,
                SlotDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-1)), // in past
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(10, 0)
            };
            _mockSlotRepo.Setup(r => r.GetByIdAsync(slotId)).ReturnsAsync(slot);

            // Reason too short (<10 chars)
            var exReason = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.EmergencyCancelSlotAsync(expertUserId, slotId, new EmergencyCancelSlotRequest { Reason = "Bận" }));
            exReason.ErrorCode.Should().Be("INVALID_REASON");

            // Slot expired in past
            var exExpired = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.EmergencyCancelSlotAsync(expertUserId, slotId, new EmergencyCancelSlotRequest { Reason = "Lý do khẩn cấp gia đình có việc đột xuất" }));
            exExpired.ErrorCode.Should().Be("SLOT_EXPIRED");
        }

        [Fact]
        public async Task UTCID12_EmergencyCancelSlotAsync_NotBooked_DeactivatesSlot()
        {
            var expertUserId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var profile = new ExpertProfile { Id = profileId, UserId = expertUserId };

            _mockProfileRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertProfile, bool>>>()))
                            .ReturnsAsync(new List<ExpertProfile> { profile });

            var slotId = Guid.NewGuid();
            var slot = new ExpertSlot
            {
                Id = slotId,
                ExpertProfileId = profileId,
                SlotDate = DateOnly.FromDateTime(DateTime.Today.AddDays(5)),
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(10, 0),
                IsBooked = false,
                IsActive = true
            };
            _mockSlotRepo.Setup(r => r.GetByIdAsync(slotId)).ReturnsAsync(slot);

            _mockPolicyRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationPlatformPolicy, bool>>>()))
                           .ReturnsAsync(new List<ConsultationPlatformPolicy> { new ConsultationPlatformPolicy() });

            var result = await _service.EmergencyCancelSlotAsync(expertUserId, slotId, new EmergencyCancelSlotRequest { Reason = "Gia đình có việc đột xuất cần nghỉ" });

            result.Should().NotBeNull();
            result.HadBooking.Should().BeFalse();
            slot.IsActive.Should().BeFalse();
        }

        [Fact]
        public async Task UTCID13_EmergencyCancelSlotAsync_Booked_LateCancel_AppliesPenaltyAndRefunds()
        {
            var expertUserId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var taxpayerId = Guid.NewGuid();
            var profile = new ExpertProfile { Id = profileId, UserId = expertUserId };

            _mockProfileRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertProfile, bool>>>()))
                            .ReturnsAsync(new List<ExpertProfile> { profile });

            var slotId = Guid.NewGuid();
            var tomorrow = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
            var slot = new ExpertSlot
            {
                Id = slotId,
                ExpertProfileId = profileId,
                SlotDate = tomorrow,
                StartTime = new TimeOnly(10, 0),
                EndTime = new TimeOnly(11, 0),
                IsBooked = true,
                IsActive = true
            };
            _mockSlotRepo.Setup(r => r.GetByIdAsync(slotId)).ReturnsAsync(slot);

            _mockPolicyRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationPlatformPolicy, bool>>>()))
                           .ReturnsAsync(new List<ConsultationPlatformPolicy>
                           {
                               new ConsultationPlatformPolicy
                               {
                                   ExpertLateCancelHours = 48, // 48h -> 24h from now is late!
                                   ExpertLateCancelPenaltyVnd = 50000m
                               }
                           });

            var booking = new Booking
            {
                Id = Guid.NewGuid(),
                ExpertSlotId = slotId,
                UserId = taxpayerId,
                BookingCode = "BK_EMERGENCY",
                Fee = 500000m,
                Status = BookingStatus.CONFIRMED
            };
            var mockBookingRepo = new Mock<IGenericRepository<Booking>>();
            mockBookingRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Booking, bool>>>()))
                            .ReturnsAsync(new List<Booking> { booking });
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(mockBookingRepo.Object);

            var escrow = new EscrowTransaction
            {
                Id = Guid.NewGuid(),
                BookingId = booking.Id,
                Amount = 500000m,
                EscrowStatus = EscrowStatus.HeldInEscrow
            };
            var mockEscrowRepo = new Mock<IGenericRepository<EscrowTransaction>>();
            mockEscrowRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<EscrowTransaction, bool>>>()))
                          .ReturnsAsync(new List<EscrowTransaction> { escrow });
            _mockUnitOfWork.Setup(u => u.Repository<EscrowTransaction>()).Returns(mockEscrowRepo.Object);

            var mockNotifyRepo = new Mock<IGenericRepository<SystemNotification>>();
            _mockUnitOfWork.Setup(u => u.Repository<SystemNotification>()).Returns(mockNotifyRepo.Object);

            var result = await _service.EmergencyCancelSlotAsync(expertUserId, slotId, new EmergencyCancelSlotRequest { Reason = "Chuyên gia bị sốt cao không thể tư vấn" });

            result.Should().NotBeNull();
            result.HadBooking.Should().BeTrue();
            result.IsLateCancel.Should().BeTrue();
            result.PenaltyAmount.Should().Be(50000m);
            booking.Status.Should().Be(BookingStatus.CANCELLED_BY_EXPERT);
            escrow.EscrowStatus.Should().Be(EscrowStatus.Refunded);
        }

        #endregion
    }
}
