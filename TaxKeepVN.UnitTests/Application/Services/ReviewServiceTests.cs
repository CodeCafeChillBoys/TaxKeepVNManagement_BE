using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using TaxKeepVN.Application.DTOs.Reviews;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Implementations;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Domain.IRepositories;
using Xunit;

namespace TaxKeepVN.UnitTests.Application.Services
{
    public class ReviewServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IConfiguration> _mockConfig;
        private readonly Mock<ILogger<ReviewService>> _mockLogger;
        private readonly ReviewService _service;

        public ReviewServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockConfig = new Mock<IConfiguration>();
            _mockLogger = new Mock<ILogger<ReviewService>>();

            _service = new ReviewService(
                _mockUnitOfWork.Object,
                _mockConfig.Object,
                _mockLogger.Object
            );
        }

        #region CompleteSessionAsync

        [Fact]
        public async Task UTCID01_CompleteSessionAsync_BookingNotFound_ThrowsNotFoundException()
        {
            var bookingId = Guid.NewGuid();
            var expertUserId = Guid.NewGuid();

            var mockRepo = new Mock<IGenericRepository<Booking>>();
            mockRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync((Booking?)null);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(mockRepo.Object);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.CompleteSessionAsync(expertUserId, bookingId));
        }

        [Fact]
        public async Task UTCID02_CompleteSessionAsync_SlotNotFound_ThrowsNotFoundException()
        {
            var bookingId = Guid.NewGuid();
            var slotId = Guid.NewGuid();
            var expertUserId = Guid.NewGuid();

            var booking = new Booking { Id = bookingId, ExpertSlotId = slotId };
            var mockBookingRepo = new Mock<IGenericRepository<Booking>>();
            mockBookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(mockBookingRepo.Object);

            var mockSlotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            mockSlotRepo.Setup(r => r.GetByIdAsync(slotId)).ReturnsAsync((ExpertSlot?)null);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(mockSlotRepo.Object);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.CompleteSessionAsync(expertUserId, bookingId));
        }

        [Fact]
        public async Task UTCID03_CompleteSessionAsync_ForbiddenExpert_ThrowsForbiddenException()
        {
            var bookingId = Guid.NewGuid();
            var slotId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var expertUserId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();

            var booking = new Booking { Id = bookingId, ExpertSlotId = slotId };
            var mockBookingRepo = new Mock<IGenericRepository<Booking>>();
            mockBookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(mockBookingRepo.Object);

            var slot = new ExpertSlot { Id = slotId, ExpertProfileId = profileId };
            var mockSlotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            mockSlotRepo.Setup(r => r.GetByIdAsync(slotId)).ReturnsAsync(slot);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(mockSlotRepo.Object);

            var profile = new ExpertProfile { Id = profileId, UserId = otherUserId };
            var mockProfileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            mockProfileRepo.Setup(r => r.GetByIdAsync(profileId)).ReturnsAsync(profile);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(mockProfileRepo.Object);

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                _service.CompleteSessionAsync(expertUserId, bookingId));
        }

        [Fact]
        public async Task UTCID04_CompleteSessionAsync_InvalidStatus_ThrowsBadRequestException()
        {
            var bookingId = Guid.NewGuid();
            var slotId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var expertUserId = Guid.NewGuid();

            var booking = new Booking { Id = bookingId, ExpertSlotId = slotId, Status = BookingStatus.PENDING_PAYMENT };
            var mockBookingRepo = new Mock<IGenericRepository<Booking>>();
            mockBookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(mockBookingRepo.Object);

            var slot = new ExpertSlot { Id = slotId, ExpertProfileId = profileId };
            var mockSlotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            mockSlotRepo.Setup(r => r.GetByIdAsync(slotId)).ReturnsAsync(slot);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(mockSlotRepo.Object);

            var profile = new ExpertProfile { Id = profileId, UserId = expertUserId };
            var mockProfileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            mockProfileRepo.Setup(r => r.GetByIdAsync(profileId)).ReturnsAsync(profile);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(mockProfileRepo.Object);

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.CompleteSessionAsync(expertUserId, bookingId));
            ex.ErrorCode.Should().Be("INVALID_STATUS");
        }

        #endregion

        #region CheckReviewEligibilityAsync

        [Fact]
        public async Task UTCID05_CheckReviewEligibilityAsync_NotCustomer_ThrowsForbiddenException()
        {
            var bookingId = Guid.NewGuid();
            var booking = new Booking { Id = bookingId, UserId = Guid.NewGuid() };
            var mockBookingRepo = new Mock<IGenericRepository<Booking>>();
            mockBookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(mockBookingRepo.Object);

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                _service.CheckReviewEligibilityAsync(Guid.NewGuid(), bookingId));
        }

        [Fact]
        public async Task UTCID06_CheckReviewEligibilityAsync_AlreadyReviewed_ReturnsFalse()
        {
            var userId = Guid.NewGuid();
            var bookingId = Guid.NewGuid();
            var booking = new Booking { Id = bookingId, UserId = userId, Status = BookingStatus.COMPLETED };
            var mockBookingRepo = new Mock<IGenericRepository<Booking>>();
            mockBookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(mockBookingRepo.Object);

            var existingReview = new ExpertReview { Id = Guid.NewGuid(), BookingId = bookingId, UserId = userId };
            var mockReviewRepo = new Mock<IGenericRepository<ExpertReview>>();
            mockReviewRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertReview, bool>>>()))
                          .ReturnsAsync(new List<ExpertReview> { existingReview });
            _mockUnitOfWork.Setup(u => u.Repository<ExpertReview>()).Returns(mockReviewRepo.Object);

            var result = await _service.CheckReviewEligibilityAsync(userId, bookingId);

            result.Should().NotBeNull();
            result.CanReview.Should().BeFalse();
            result.AlreadyReviewed.Should().BeTrue();
        }

        [Fact]
        public async Task UTCID07_CheckReviewEligibilityAsync_Eligible_ReturnsTrue()
        {
            var userId = Guid.NewGuid();
            var bookingId = Guid.NewGuid();
            var booking = new Booking
            {
                Id = bookingId,
                UserId = userId,
                Status = BookingStatus.COMPLETED,
                ReviewDeadline = DateTimeOffset.UtcNow.AddDays(3)
            };
            var mockBookingRepo = new Mock<IGenericRepository<Booking>>();
            mockBookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(mockBookingRepo.Object);

            var mockReviewRepo = new Mock<IGenericRepository<ExpertReview>>();
            mockReviewRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertReview, bool>>>()))
                          .ReturnsAsync(new List<ExpertReview>());
            _mockUnitOfWork.Setup(u => u.Repository<ExpertReview>()).Returns(mockReviewRepo.Object);

            var result = await _service.CheckReviewEligibilityAsync(userId, bookingId);

            result.Should().NotBeNull();
            result.CanReview.Should().BeTrue();
            result.AlreadyReviewed.Should().BeFalse();
        }

        #endregion

        #region SubmitReviewAsync

        [Fact]
        public async Task UTCID08_SubmitReviewAsync_InvalidRating_ThrowsBadRequestException()
        {
            var request = new CreateReviewRequest { Rating = 6, Comment = "Rất tốt và nhiệt tình hỗ trợ" };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.SubmitReviewAsync(Guid.NewGuid(), Guid.NewGuid(), request));
            ex.ErrorCode.Should().Be("INVALID_RATING");
        }

        [Fact]
        public async Task UTCID09_SubmitReviewAsync_CommentTooShort_ThrowsBadRequestException()
        {
            var request = new CreateReviewRequest { Rating = 5, Comment = "Ok" };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.SubmitReviewAsync(Guid.NewGuid(), Guid.NewGuid(), request));
            ex.ErrorCode.Should().Be("COMMENT_TOO_SHORT");
        }

        [Fact]
        public async Task UTCID10_SubmitReviewAsync_Valid_CreatesReviewSuccessfully()
        {
            var userId = Guid.NewGuid();
            var bookingId = Guid.NewGuid();
            var slotId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var expertUserId = Guid.NewGuid();

            var booking = new Booking
            {
                Id = bookingId,
                UserId = userId,
                ExpertSlotId = slotId,
                Status = BookingStatus.COMPLETED,
                ReviewDeadline = DateTimeOffset.UtcNow.AddDays(3)
            };
            var mockBookingRepo = new Mock<IGenericRepository<Booking>>();
            mockBookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(mockBookingRepo.Object);

            var slot = new ExpertSlot { Id = slotId, ExpertProfileId = profileId };
            var mockSlotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            mockSlotRepo.Setup(r => r.GetByIdAsync(slotId)).ReturnsAsync(slot);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(mockSlotRepo.Object);

            var profile = new ExpertProfile { Id = profileId, UserId = expertUserId, Rating = 4.5m, TotalReviews = 2 };
            var mockProfileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            mockProfileRepo.Setup(r => r.GetByIdAsync(profileId)).ReturnsAsync(profile);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(mockProfileRepo.Object);

            var mockReviewRepo = new Mock<IGenericRepository<ExpertReview>>();
            mockReviewRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertReview, bool>>>()))
                          .ReturnsAsync(new List<ExpertReview>());
            _mockUnitOfWork.Setup(u => u.Repository<ExpertReview>()).Returns(mockReviewRepo.Object);

            var user = new User { UserId = userId, FullName = "Khách hàng Nguyễn" };
            var mockUserRepo = new Mock<IGenericRepository<User>>();
            mockUserRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(mockUserRepo.Object);

            var mockNotifRepo = new Mock<IGenericRepository<SystemNotification>>();
            _mockUnitOfWork.Setup(u => u.Repository<SystemNotification>()).Returns(mockNotifRepo.Object);

            var request = new CreateReviewRequest
            {
                Rating = 5,
                Comment = "Chuyên gia tư vấn rất nhiệt tình và chi tiết."
            };

            var result = await _service.SubmitReviewAsync(userId, bookingId, request);

            result.Should().NotBeNull();
            result.Rating.Should().Be(5);
            mockReviewRepo.Verify(r => r.AddAsync(It.IsAny<ExpertReview>()), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.AtLeastOnce());
        }

        #endregion

        #region TogglePublishReviewAsync

        [Fact]
        public async Task UTCID11_TogglePublishReviewAsync_NotFound_ThrowsNotFoundException()
        {
            var mockReviewRepo = new Mock<IGenericRepository<ExpertReview>>();
            mockReviewRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((ExpertReview?)null);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertReview>()).Returns(mockReviewRepo.Object);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.TogglePublishReviewAsync(Guid.NewGuid(), false));
        }

        [Fact]
        public async Task UTCID12_TogglePublishReviewAsync_Found_TogglesSuccessfully()
        {
            var review = new ExpertReview { Id = Guid.NewGuid(), IsPublished = true, Rating = 5, Comment = "Tốt" };
            var mockReviewRepo = new Mock<IGenericRepository<ExpertReview>>();
            mockReviewRepo.Setup(r => r.GetByIdAsync(review.Id)).ReturnsAsync(review);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertReview>()).Returns(mockReviewRepo.Object);

            var mockProfileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            mockProfileRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((ExpertProfile?)null);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(mockProfileRepo.Object);

            var mockUserRepo = new Mock<IGenericRepository<User>>();
            mockUserRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(new User { FullName = "Khách hàng" });
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(mockUserRepo.Object);

            var result = await _service.TogglePublishReviewAsync(review.Id, false);

            result.Should().NotBeNull();
            result.IsPublished.Should().BeFalse();
            mockReviewRepo.Verify(r => r.Update(review), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion

        #region CompleteSessionAsync Success

        [Fact]
        public async Task UTCID13_CompleteSessionAsync_Success_CompletesSessionAndNotifies()
        {
            var bookingId = Guid.NewGuid();
            var slotId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var expertUserId = Guid.NewGuid();
            var clientId = Guid.NewGuid();

            var booking = new Booking
            {
                Id = bookingId,
                UserId = clientId,
                ExpertSlotId = slotId,
                Status = BookingStatus.CONFIRMED
            };
            var mockBookingRepo = new Mock<IGenericRepository<Booking>>();
            mockBookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(mockBookingRepo.Object);

            var slot = new ExpertSlot { Id = slotId, ExpertProfileId = profileId };
            var mockSlotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            mockSlotRepo.Setup(r => r.GetByIdAsync(slotId)).ReturnsAsync(slot);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(mockSlotRepo.Object);

            var profile = new ExpertProfile { Id = profileId, UserId = expertUserId, CompletedConsultationsCount = 0 };
            var mockProfileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            mockProfileRepo.Setup(r => r.GetByIdAsync(profileId)).ReturnsAsync(profile);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(mockProfileRepo.Object);

            var escrow = new EscrowTransaction { Id = Guid.NewGuid(), BookingId = bookingId, EscrowStatus = EscrowStatus.HeldInEscrow };
            var mockEscrowRepo = new Mock<IGenericRepository<EscrowTransaction>>();
            mockEscrowRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<EscrowTransaction, bool>>>()))
                .ReturnsAsync(new List<EscrowTransaction> { escrow });
            _mockUnitOfWork.Setup(u => u.Repository<EscrowTransaction>()).Returns(mockEscrowRepo.Object);

            var session = new ConsultationSession { Id = Guid.NewGuid(), BookingId = bookingId, StartedAt = DateTimeOffset.UtcNow.AddMinutes(-45) };
            var mockSessionRepo = new Mock<IGenericRepository<ConsultationSession>>();
            mockSessionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationSession, bool>>>()))
                .ReturnsAsync(new List<ConsultationSession> { session });
            _mockUnitOfWork.Setup(u => u.Repository<ConsultationSession>()).Returns(mockSessionRepo.Object);

            var mockNotifRepo = new Mock<IGenericRepository<SystemNotification>>();
            _mockUnitOfWork.Setup(u => u.Repository<SystemNotification>()).Returns(mockNotifRepo.Object);

            await _service.CompleteSessionAsync(expertUserId, bookingId);

            booking.Status.Should().Be(BookingStatus.COMPLETED);
            escrow.EscrowStatus.Should().Be(EscrowStatus.CoolingDown);
            profile.CompletedConsultationsCount.Should().Be(1);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion

        #region GetExpertPublicReviewsAsync & GetBookingReviewAsync

        [Fact]
        public async Task UTCID14_GetExpertPublicReviewsAsync_ProfileNotFound_ThrowsNotFoundException()
        {
            var profileId = Guid.NewGuid();
            var mockProfileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            mockProfileRepo.Setup(r => r.GetByIdAsync(profileId)).ReturnsAsync((ExpertProfile?)null);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(mockProfileRepo.Object);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.GetExpertPublicReviewsAsync(profileId));
        }

        [Fact]
        public async Task UTCID15_GetExpertPublicReviewsAsync_Success_ReturnsPublishedReviews()
        {
            var profileId = Guid.NewGuid();
            var reviewerUserId = Guid.NewGuid();
            var profile = new ExpertProfile { Id = profileId, UserId = Guid.NewGuid() };
            var mockProfileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            mockProfileRepo.Setup(r => r.GetByIdAsync(profileId)).ReturnsAsync(profile);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(mockProfileRepo.Object);

            var reviews = new List<ExpertReview>
            {
                new ExpertReview
                {
                    Id = Guid.NewGuid(),
                    ExpertProfileId = profileId,
                    UserId = reviewerUserId,
                    Rating = 5,
                    Comment = "Chuyen nghiep",
                    IsPublished = true,
                    IsAnonymous = false,
                    CreatedAt = DateTimeOffset.UtcNow
                }
            };
            var mockReviewRepo = new Mock<IGenericRepository<ExpertReview>>();
            mockReviewRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertReview, bool>>>()))
                .ReturnsAsync(reviews);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertReview>()).Returns(mockReviewRepo.Object);

            var reviewer = new User { UserId = reviewerUserId, FullName = "Nguyen Van A" };
            var mockUserRepo = new Mock<IGenericRepository<User>>();
            mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User> { reviewer });
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(mockUserRepo.Object);

            var result = await _service.GetExpertPublicReviewsAsync(profileId);

            result.Should().NotBeNull();
            result.Items.Should().HaveCount(1);
            result.Items.First().ReviewerName.Should().Be("Nguyen Van A");
        }

        [Fact]
        public async Task UTCID16_GetBookingReviewAsync_ReviewFound_ReturnsReviewDetail()
        {
            var bookingId = Guid.NewGuid();
            var clientId = Guid.NewGuid();
            var slotId = Guid.NewGuid();
            var profileId = Guid.NewGuid();

            var booking = new Booking { Id = bookingId, UserId = clientId, ExpertSlotId = slotId };
            var mockBookingRepo = new Mock<IGenericRepository<Booking>>();
            mockBookingRepo.Setup(r => r.GetByIdAsync(bookingId)).ReturnsAsync(booking);
            _mockUnitOfWork.Setup(u => u.Repository<Booking>()).Returns(mockBookingRepo.Object);

            var slot = new ExpertSlot { Id = slotId, ExpertProfileId = profileId };
            var mockSlotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            mockSlotRepo.Setup(r => r.GetByIdAsync(slotId)).ReturnsAsync(slot);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(mockSlotRepo.Object);

            var profile = new ExpertProfile { Id = profileId, UserId = Guid.NewGuid() };
            var mockProfileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            mockProfileRepo.Setup(r => r.GetByIdAsync(profileId)).ReturnsAsync(profile);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(mockProfileRepo.Object);

            var review = new ExpertReview
            {
                Id = Guid.NewGuid(),
                BookingId = bookingId,
                UserId = clientId,
                Rating = 5,
                Comment = "Tot",
                IsPublished = true
            };
            var mockReviewRepo = new Mock<IGenericRepository<ExpertReview>>();
            mockReviewRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertReview, bool>>>()))
                .ReturnsAsync(new List<ExpertReview> { review });
            _mockUnitOfWork.Setup(u => u.Repository<ExpertReview>()).Returns(mockReviewRepo.Object);

            var mockUserRepo = new Mock<IGenericRepository<User>>();
            mockUserRepo.Setup(r => r.GetByIdAsync(clientId)).ReturnsAsync(new User { FullName = "Khach Hang" });
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(mockUserRepo.Object);

            var result = await _service.GetBookingReviewAsync(clientId, bookingId);

            result.Should().NotBeNull();
            result!.Rating.Should().Be(5);
        }

        #endregion

        #region ReplyToReviewAsync & Paged Queries

        [Fact]
        public async Task UTCID17_ReplyToReviewAsync_EmptyReply_ThrowsBadRequestException()
        {
            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.ReplyToReviewAsync(Guid.NewGuid(), Guid.NewGuid(), new ExpertReplyRequest { Reply = "" }));
            ex.ErrorCode.Should().Be("EMPTY_REPLY");
        }

        [Fact]
        public async Task UTCID18_ReplyToReviewAsync_Valid_SavesReplyAndNotifies()
        {
            var reviewId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var expertUserId = Guid.NewGuid();
            var clientId = Guid.NewGuid();

            var review = new ExpertReview
            {
                Id = reviewId,
                ExpertProfileId = profileId,
                UserId = clientId,
                ExpertReply = null
            };
            var mockReviewRepo = new Mock<IGenericRepository<ExpertReview>>();
            mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId)).ReturnsAsync(review);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertReview>()).Returns(mockReviewRepo.Object);

            var profile = new ExpertProfile { Id = profileId, UserId = expertUserId };
            var mockProfileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            mockProfileRepo.Setup(r => r.GetByIdAsync(profileId)).ReturnsAsync(profile);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(mockProfileRepo.Object);

            var mockUserRepo = new Mock<IGenericRepository<User>>();
            mockUserRepo.Setup(r => r.GetByIdAsync(clientId)).ReturnsAsync(new User { FullName = "Khach Hang" });
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(mockUserRepo.Object);

            var mockNotifRepo = new Mock<IGenericRepository<SystemNotification>>();
            _mockUnitOfWork.Setup(u => u.Repository<SystemNotification>()).Returns(mockNotifRepo.Object);

            var result = await _service.ReplyToReviewAsync(expertUserId, reviewId, new ExpertReplyRequest { Reply = "Cam on ban da danh gia tot!" });

            result.Should().NotBeNull();
            review.ExpertReply.Should().Be("Cam on ban da danh gia tot!");
            mockReviewRepo.Verify(r => r.Update(review), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task UTCID19_GetMyReceivedReviewsAsync_ReturnsPagedList()
        {
            var expertUserId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var profile = new ExpertProfile { Id = profileId, UserId = expertUserId };

            var mockProfileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            mockProfileRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertProfile, bool>>>()))
                .ReturnsAsync(new List<ExpertProfile> { profile });
            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(mockProfileRepo.Object);

            var reviews = new List<ExpertReview>
            {
                new ExpertReview { Id = Guid.NewGuid(), ExpertProfileId = profileId, UserId = Guid.NewGuid(), Rating = 5, CreatedAt = DateTimeOffset.UtcNow }
            };
            var mockReviewRepo = new Mock<IGenericRepository<ExpertReview>>();
            mockReviewRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertReview, bool>>>()))
                .ReturnsAsync(reviews);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertReview>()).Returns(mockReviewRepo.Object);

            var mockUserRepo = new Mock<IGenericRepository<User>>();
            mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User>());
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(mockUserRepo.Object);

            var result = await _service.GetMyReceivedReviewsAsync(expertUserId, 1, 10);

            result.Should().NotBeNull();
            result.Pagination.TotalItems.Should().Be(1);
        }

        [Fact]
        public async Task UTCID20_GetFlaggedReviewsAsync_ReturnsPagedList()
        {
            var reviews = new List<ExpertReview>
            {
                new ExpertReview { Id = Guid.NewGuid(), IsFlaggedForReview = true, Rating = 1, CreatedAt = DateTimeOffset.UtcNow }
            };
            var mockReviewRepo = new Mock<IGenericRepository<ExpertReview>>();
            mockReviewRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertReview, bool>>>()))
                .ReturnsAsync(reviews);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertReview>()).Returns(mockReviewRepo.Object);

            var mockUserRepo = new Mock<IGenericRepository<User>>();
            mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User>());
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(mockUserRepo.Object);

            var result = await _service.GetFlaggedReviewsAsync(1, 20);

            result.Should().NotBeNull();
            result.Pagination.TotalItems.Should().Be(1);
        }

        #endregion
    }
}

