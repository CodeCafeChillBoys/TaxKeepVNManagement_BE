using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Experts;
using TaxKeepVN.Application.DTOs.SearchConfigs;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Implementations;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Domain.IRepositories;
using Xunit;

namespace TaxKeepVN.UnitTests.Application.Services
{
    public class ExpertSearchServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IExpertSearchConfigService> _mockSearchConfigService;
        private readonly Mock<ILogger<ExpertSearchService>> _mockLogger;
        private readonly Mock<IGenericRepository<ExpertProfile>> _mockProfileRepo;
        private readonly Mock<IGenericRepository<ExpertApplication>> _mockAppRepo;
        private readonly Mock<IGenericRepository<User>> _mockUserRepo;
        private readonly Mock<IGenericRepository<ExpertApplicationSpecialization>> _mockAppSpecRepo;
        private readonly Mock<IGenericRepository<Specialization>> _mockSpecRepo;
        private readonly Mock<IGenericRepository<ExpertApplicationFeeProposal>> _mockProposalRepo;
        private readonly Mock<IGenericRepository<ExpertApplicationCertificate>> _mockCertRepo;
        private readonly Mock<IGenericRepository<ExpertSlot>> _mockSlotRepo;
        private readonly Mock<IGenericRepository<ExpertReview>> _mockReviewRepo;
        private readonly ExpertSearchService _service;

        public ExpertSearchServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockSearchConfigService = new Mock<IExpertSearchConfigService>();
            _mockLogger = new Mock<ILogger<ExpertSearchService>>();

            _mockProfileRepo = new Mock<IGenericRepository<ExpertProfile>>();
            _mockAppRepo = new Mock<IGenericRepository<ExpertApplication>>();
            _mockUserRepo = new Mock<IGenericRepository<User>>();
            _mockAppSpecRepo = new Mock<IGenericRepository<ExpertApplicationSpecialization>>();
            _mockSpecRepo = new Mock<IGenericRepository<Specialization>>();
            _mockProposalRepo = new Mock<IGenericRepository<ExpertApplicationFeeProposal>>();
            _mockCertRepo = new Mock<IGenericRepository<ExpertApplicationCertificate>>();
            _mockSlotRepo = new Mock<IGenericRepository<ExpertSlot>>();
            _mockReviewRepo = new Mock<IGenericRepository<ExpertReview>>();

            _mockUnitOfWork.Setup(u => u.Repository<ExpertProfile>()).Returns(_mockProfileRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertApplication>()).Returns(_mockAppRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(_mockUserRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertApplicationSpecialization>()).Returns(_mockAppSpecRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<Specialization>()).Returns(_mockSpecRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertApplicationFeeProposal>()).Returns(_mockProposalRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertApplicationCertificate>()).Returns(_mockCertRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertSlot>()).Returns(_mockSlotRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<ExpertReview>()).Returns(_mockReviewRepo.Object);

            // Default config setup
            _mockSearchConfigService.Setup(s => s.GetConfigAsync())
                                    .ReturnsAsync(new ExpertSearchCriteriaConfigDto
                                    {
                                        RequireVerifiedCertificate = false,
                                        DefaultSortBy = ExpertSortBy.Recommended,
                                        SearchableFields = new SearchableFieldsConfig
                                        {
                                            SearchInFullName = true,
                                            SearchInJobTitle = true,
                                            SearchInCompanyName = true,
                                            SearchInBio = true,
                                            SearchInSpecializations = true,
                                            SearchInCertificates = true
                                        },
                                        RankingWeights = new RankingWeightsConfig()
                                    });

            _service = new ExpertSearchService(
                _mockUnitOfWork.Object,
                _mockSearchConfigService.Object,
                _mockLogger.Object);
        }

        #region SearchExpertsAsync

        [Fact]
        public async Task UTCID01_SearchExpertsAsync_NoActiveProfiles_ReturnsEmptyWithSuggestion()
        {
            _mockProfileRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ExpertProfile>());

            var query = new ExpertSearchQueryParameters { Page = 1, Size = 10 };
            var result = await _service.SearchExpertsAsync(query);

            result.Should().NotBeNull();
            result.Items.Should().BeEmpty();
            result.Pagination.TotalItems.Should().Be(0);
        }

        [Fact]
        public async Task UTCID03_SearchExpertsAsync_NoApprovedApps_ReturnsEmptyWithSuggestion()
        {
            var profile = new ExpertProfile { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), LatestApplicationId = Guid.NewGuid(), IsActive = true };
            _mockProfileRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ExpertProfile> { profile });

            // Application is Draft, not Approved
            var app = new ExpertApplication { Id = profile.LatestApplicationId, Status = ExpertApplicationStatus.Draft };
            _mockAppRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplication, bool>>>()))
                        .ReturnsAsync(new List<ExpertApplication> { app });

            var user = new User { UserId = profile.UserId, Status = "active", FullName = "Nguyen Van A" };
            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User> { user });

            var query = new ExpertSearchQueryParameters { Page = 1, Size = 10 };
            var result = await _service.SearchExpertsAsync(query);

            result.Should().NotBeNull();
            result.Items.Should().BeEmpty();
        }

        [Fact]
        public async Task UTCID04_SearchExpertsAsync_FullFilterMatch_ReturnsMatchedExperts()
        {
            var profileId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var appId = Guid.NewGuid();

            var profile = new ExpertProfile
            {
                Id = profileId,
                UserId = userId,
                LatestApplicationId = appId,
                IsActive = true,
                JobTitle = "Chuyên gia tư vấn thuế doanh nghiệp",
                CompanyName = "TaxKeep Vietnam",
                Bio = "Kinh nghiệm 10 năm",
                Rating = 4.9m,
                TotalReviews = 25,
                CompletedConsultationsCount = 50,
                YearsOfExperience = 10
            };
            _mockProfileRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ExpertProfile> { profile });

            var app = new ExpertApplication
            {
                Id = appId,
                UserId = userId,
                FullName = "Tran Van ChuyenGia",
                Status = ExpertApplicationStatus.Approved
            };
            _mockAppRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplication, bool>>>()))
                        .ReturnsAsync(new List<ExpertApplication> { app });

            var user = new User { UserId = userId, Status = "active", FullName = "Tran Van ChuyenGia" };
            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User> { user });

            var appSpec = new ExpertApplicationSpecialization { ApplicationId = appId, SpecializationId = 1 };
            _mockAppSpecRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationSpecialization, bool>>>()))
                            .ReturnsAsync(new List<ExpertApplicationSpecialization> { appSpec });

            var spec = new Specialization { Id = 1, Name = "Thuế TNCN", IsActive = true };
            _mockSpecRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Specialization, bool>>>()))
                         .ReturnsAsync(new List<Specialization> { spec });

            var fee = new ExpertApplicationFeeProposal { ApplicationId = appId, ProposedFee = 500000m };
            _mockProposalRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationFeeProposal, bool>>>()))
                             .ReturnsAsync(new List<ExpertApplicationFeeProposal> { fee });

            var cert = new ExpertApplicationCertificate { ApplicationId = appId, VerificationStatus = CertificateVerificationStatus.Verified, CertificateName = "Chứng chỉ CPA" };
            _mockCertRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationCertificate, bool>>>()))
                         .ReturnsAsync(new List<ExpertApplicationCertificate> { cert });

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var slot = new ExpertSlot { Id = Guid.NewGuid(), ExpertProfileId = profileId, SlotDate = today, StartTime = new TimeOnly(14, 0), EndTime = new TimeOnly(15, 0), IsActive = true, IsBooked = false };
            _mockSlotRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertSlot, bool>>>()))
                         .ReturnsAsync(new List<ExpertSlot> { slot });

            var query = new ExpertSearchQueryParameters
            {
                Keyword = "ChuyenGia",
                SpecializationIds = new List<int> { 1 },
                MinRating = 4.0m,
                MinFee = 100000m,
                MaxFee = 1000000m,
                TimeFilter = ExpertTimeFilter.Today,
                SortBy = ExpertSortBy.RatingDesc,
                Page = 1,
                Size = 10
            };

            var result = await _service.SearchExpertsAsync(query);

            result.Should().NotBeNull();
            result.Items.Should().HaveCount(1);
            result.Items[0].FullName.Should().Be("Tran Van ChuyenGia");
            result.Items[0].StartingFee.Should().Be(500000m);
            result.Items[0].Rating.Should().Be(4.9m);
        }

        [Fact]
        public async Task UTCID05_SearchExpertsAsync_VariousSortOrders_SortsCorrectly()
        {
            var profile1 = new ExpertProfile { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), LatestApplicationId = Guid.NewGuid(), IsActive = true, Rating = 4.5m, CompletedConsultationsCount = 10, YearsOfExperience = 3 };
            var profile2 = new ExpertProfile { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), LatestApplicationId = Guid.NewGuid(), IsActive = true, Rating = 5.0m, CompletedConsultationsCount = 50, YearsOfExperience = 8 };
            _mockProfileRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ExpertProfile> { profile1, profile2 });

            var app1 = new ExpertApplication { Id = profile1.LatestApplicationId, Status = ExpertApplicationStatus.Approved, FullName = "Expert One" };
            var app2 = new ExpertApplication { Id = profile2.LatestApplicationId, Status = ExpertApplicationStatus.Approved, FullName = "Expert Two" };
            _mockAppRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplication, bool>>>()))
                        .ReturnsAsync(new List<ExpertApplication> { app1, app2 });

            var user1 = new User { UserId = profile1.UserId, Status = "active", FullName = "Expert One" };
            var user2 = new User { UserId = profile2.UserId, Status = "active", FullName = "Expert Two" };
            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User> { user1, user2 });

            var fee1 = new ExpertApplicationFeeProposal { ApplicationId = profile1.LatestApplicationId, ProposedFee = 300000m };
            var fee2 = new ExpertApplicationFeeProposal { ApplicationId = profile2.LatestApplicationId, ProposedFee = 800000m };
            _mockProposalRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationFeeProposal, bool>>>()))
                             .ReturnsAsync(new List<ExpertApplicationFeeProposal> { fee1, fee2 });

            _mockSlotRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertSlot, bool>>>()))
                         .ReturnsAsync(new List<ExpertSlot>());
            _mockCertRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationCertificate, bool>>>()))
                         .ReturnsAsync(new List<ExpertApplicationCertificate>());
            _mockAppSpecRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationSpecialization, bool>>>()))
                            .ReturnsAsync(new List<ExpertApplicationSpecialization>());
            _mockSpecRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Specialization, bool>>>()))
                         .ReturnsAsync(new List<Specialization>());

            // Test FeeAsc
            var resFeeAsc = await _service.SearchExpertsAsync(new ExpertSearchQueryParameters { SortBy = ExpertSortBy.FeeAsc });
            resFeeAsc.Items[0].StartingFee.Should().Be(300000m);

            // Test FeeDesc
            var resFeeDesc = await _service.SearchExpertsAsync(new ExpertSearchQueryParameters { SortBy = ExpertSortBy.FeeDesc });
            resFeeDesc.Items[0].StartingFee.Should().Be(800000m);

            // Test ExperienceDesc
            var resExp = await _service.SearchExpertsAsync(new ExpertSearchQueryParameters { SortBy = ExpertSortBy.ExperienceDesc });
            resExp.Items[0].YearsOfExperience.Should().Be(8);

            // Test CompletedSessionsDesc
            var resCompleted = await _service.SearchExpertsAsync(new ExpertSearchQueryParameters { SortBy = ExpertSortBy.CompletedSessionsDesc });
            resCompleted.Items[0].CompletedConsultationsCount.Should().Be(50);
        }

        #endregion

        #region GetFeaturedExpertsAsync

        [Fact]
        public async Task UTCID06_GetFeaturedExpertsAsync_ReturnsFeaturedList()
        {
            var profile = new ExpertProfile { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), LatestApplicationId = Guid.NewGuid(), IsActive = true, Rating = 5.0m };
            _mockProfileRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ExpertProfile> { profile });

            var app = new ExpertApplication { Id = profile.LatestApplicationId, Status = ExpertApplicationStatus.Approved, FullName = "Featured Expert" };
            _mockAppRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplication, bool>>>()))
                        .ReturnsAsync(new List<ExpertApplication> { app });

            var user = new User { UserId = profile.UserId, Status = "active", FullName = "Featured Expert" };
            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User> { user });

            _mockSlotRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertSlot, bool>>>()))
                         .ReturnsAsync(new List<ExpertSlot>());
            _mockCertRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationCertificate, bool>>>()))
                         .ReturnsAsync(new List<ExpertApplicationCertificate>());
            _mockProposalRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationFeeProposal, bool>>>()))
                             .ReturnsAsync(new List<ExpertApplicationFeeProposal>());
            _mockAppSpecRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationSpecialization, bool>>>()))
                            .ReturnsAsync(new List<ExpertApplicationSpecialization>());
            _mockSpecRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Specialization, bool>>>()))
                         .ReturnsAsync(new List<Specialization>());

            var result = await _service.GetFeaturedExpertsAsync(6);

            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            result[0].FullName.Should().Be("Featured Expert");
        }

        #endregion

        #region GetExpertDetailByIdAsync

        [Fact]
        public async Task UTCID02_GetExpertDetailByIdAsync_NotFound_ThrowsNotFoundException()
        {
            _mockProfileRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                            .ReturnsAsync((ExpertProfile?)null);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.GetExpertDetailByIdAsync(Guid.NewGuid()));
        }

        [Fact]
        public async Task UTCID07_GetExpertDetailByIdAsync_Valid_ReturnsDetailWithMasking()
        {
            var profileId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var appId = Guid.NewGuid();

            var profile = new ExpertProfile
            {
                Id = profileId,
                UserId = userId,
                LatestApplicationId = appId,
                IsActive = true,
                JobTitle = "Chuyên viên tư vấn",
                Rating = 4.8m,
                TotalReviews = 10
            };
            _mockProfileRepo.Setup(r => r.GetByIdAsync(profileId)).ReturnsAsync(profile);

            var app = new ExpertApplication { Id = appId, FullName = "Nguyen Van B", AvatarUrl = "https://taxkeep.vn/avatar.png" };
            _mockAppRepo.Setup(r => r.GetByIdAsync(appId)).ReturnsAsync(app);

            var user = new User { UserId = userId, FullName = "Nguyen Van B" };
            _mockUserRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

            var spec = new Specialization { Id = 1, Name = "Thuế TNCN" };
            _mockAppSpecRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationSpecialization, bool>>>()))
                            .ReturnsAsync(new List<ExpertApplicationSpecialization> { new ExpertApplicationSpecialization { SpecializationId = 1 } });
            _mockSpecRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Specialization, bool>>>()))
                         .ReturnsAsync(new List<Specialization> { spec });

            var cert = new ExpertApplicationCertificate
            {
                Id = Guid.NewGuid(),
                CertificateType = "CPA",
                CertificateName = "Chung chi CPA",
                CertificateNumber = "1234567890",
                VerificationStatus = CertificateVerificationStatus.Verified
            };
            _mockCertRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationCertificate, bool>>>()))
                         .ReturnsAsync(new List<ExpertApplicationCertificate> { cert });

            var fee = new ExpertApplicationFeeProposal { ProposedFee = 500000m, SessionType = SessionType.ONLINE_MEETING, DurationMinutes = 60 };
            _mockProposalRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationFeeProposal, bool>>>()))
                             .ReturnsAsync(new List<ExpertApplicationFeeProposal> { fee });

            var review = new ExpertReview
            {
                Id = Guid.NewGuid(),
                ExpertProfileId = profileId,
                UserId = Guid.NewGuid(),
                Rating = 5,
                Comment = "Tư vấn rất nhiệt tình",
                IsPublished = true,
                IsAnonymous = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            _mockReviewRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertReview, bool>>>()))
                           .ReturnsAsync(new List<ExpertReview> { review });

            _mockSlotRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertSlot, bool>>>()))
                         .ReturnsAsync(new List<ExpertSlot>());

            var result = await _service.GetExpertDetailByIdAsync(profileId);

            result.Should().NotBeNull();
            result.FullName.Should().Be("Nguyen Van B");
            result.VerifiedCertificates.Should().HaveCount(1);
            result.VerifiedCertificates[0].MaskedCertificateNumber.Should().Contain("*");
            result.RecentReviews.Should().HaveCount(1);
            result.FeePackages.Should().HaveCount(1);
        }

        #endregion

        #region GetExpertReviewsAsync & GetExpertUpcomingSlotsAsync

        [Fact]
        public async Task UTCID08_GetExpertReviewsAsync_NotFound_ThrowsNotFoundException()
        {
            _mockProfileRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((ExpertProfile?)null);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.GetExpertReviewsAsync(Guid.NewGuid()));
        }

        [Fact]
        public async Task UTCID09_GetExpertReviewsAsync_Valid_ReturnsPagedReviews()
        {
            var profileId = Guid.NewGuid();
            var reviewerId = Guid.NewGuid();
            var profile = new ExpertProfile { Id = profileId };
            _mockProfileRepo.Setup(r => r.GetByIdAsync(profileId)).ReturnsAsync(profile);

            var review = new ExpertReview
            {
                Id = Guid.NewGuid(),
                ExpertProfileId = profileId,
                UserId = reviewerId,
                Rating = 5,
                Comment = "Dịch vụ xuất sắc",
                IsPublished = true,
                IsAnonymous = false,
                CreatedAt = DateTimeOffset.UtcNow
            };
            _mockReviewRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertReview, bool>>>()))
                           .ReturnsAsync(new List<ExpertReview> { review });

            var reviewer = new User { UserId = reviewerId, FullName = "Khach Hang Tran" };
            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User> { reviewer });

            var result = await _service.GetExpertReviewsAsync(profileId, 1, 10, 5);

            result.Should().NotBeNull();
            result.Items.Should().HaveCount(1);
            result.Items[0].ReviewerDisplayName.Should().Be("Khach T***");
            result.Items[0].Rating.Should().Be(5);
        }

        [Fact]
        public async Task UTCID10_GetExpertUpcomingSlotsAsync_NotFound_ThrowsNotFoundException()
        {
            _mockProfileRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((ExpertProfile?)null);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.GetExpertUpcomingSlotsAsync(Guid.NewGuid()));
        }

        [Fact]
        public async Task UTCID11_GetExpertUpcomingSlotsAsync_Valid_ReturnsAvailableSlots()
        {
            var profileId = Guid.NewGuid();
            var profile = new ExpertProfile { Id = profileId };
            _mockProfileRepo.Setup(r => r.GetByIdAsync(profileId)).ReturnsAsync(profile);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var slot = new ExpertSlot
            {
                Id = Guid.NewGuid(),
                ExpertProfileId = profileId,
                SlotDate = today.AddDays(2),
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(10, 0),
                SessionType = SessionType.ONLINE_MEETING,
                IsActive = true,
                IsBooked = false,
                HoldExpiresAt = null
            };
            _mockSlotRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertSlot, bool>>>()))
                         .ReturnsAsync(new List<ExpertSlot> { slot });

            var result = await _service.GetExpertUpcomingSlotsAsync(profileId);

            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            result[0].Id.Should().Be(slot.Id);
            result[0].SlotDate.Should().Be(today.AddDays(2));
        }

        #endregion

        #region Dedicated Matrix Tests for SearchExpertsAsync & GetExpertDetailByIdAsync

        [Fact]
        public async Task SearchExpertsAsync_RatingAndFeeFilter_MatchesCorrectProfiles()
        {
            var profileId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var appId = Guid.NewGuid();

            var profile = new ExpertProfile
            {
                Id = profileId,
                UserId = userId,
                LatestApplicationId = appId,
                IsActive = true,
                Rating = 4.8m,
                YearsOfExperience = 5
            };
            _mockProfileRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ExpertProfile> { profile });

            var app = new ExpertApplication { Id = appId, Status = ExpertApplicationStatus.Approved, FullName = "Le Thi C" };
            _mockAppRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplication, bool>>>()))
                        .ReturnsAsync(new List<ExpertApplication> { app });

            var user = new User { UserId = userId, Status = "active", FullName = "Le Thi C" };
            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User> { user });

            var fee = new ExpertApplicationFeeProposal { ApplicationId = appId, ProposedFee = 400000m };
            _mockProposalRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationFeeProposal, bool>>>()))
                             .ReturnsAsync(new List<ExpertApplicationFeeProposal> { fee });

            _mockSlotRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertSlot, bool>>>()))
                         .ReturnsAsync(new List<ExpertSlot>());
            _mockCertRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationCertificate, bool>>>()))
                         .ReturnsAsync(new List<ExpertApplicationCertificate>());
            _mockAppSpecRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationSpecialization, bool>>>()))
                            .ReturnsAsync(new List<ExpertApplicationSpecialization>());
            _mockSpecRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Specialization, bool>>>()))
                         .ReturnsAsync(new List<Specialization>());

            var query = new ExpertSearchQueryParameters
            {
                MinRating = 4.5m,
                MinFee = 300000m,
                MaxFee = 500000m,
                TimeFilter = ExpertTimeFilter.All
            };

            var result = await _service.SearchExpertsAsync(query);

            result.Should().NotBeNull();
            result.Items.Should().HaveCount(1);
            result.Items[0].FullName.Should().Be("Le Thi C");
        }

        [Fact]
        public async Task SearchExpertsAsync_NoCriteriaMatch_ReturnsEmptyWithSuggestions()
        {
            var profileId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var appId = Guid.NewGuid();

            var profile = new ExpertProfile { Id = profileId, UserId = userId, LatestApplicationId = appId, IsActive = true, Rating = 3.0m };
            _mockProfileRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ExpertProfile> { profile });

            var app = new ExpertApplication { Id = appId, Status = ExpertApplicationStatus.Approved, FullName = "Expert Match" };
            _mockAppRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplication, bool>>>()))
                        .ReturnsAsync(new List<ExpertApplication> { app });

            var user = new User { UserId = userId, Status = "active", FullName = "Expert Match" };
            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User> { user });

            _mockProposalRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationFeeProposal, bool>>>()))
                             .ReturnsAsync(new List<ExpertApplicationFeeProposal>());
            _mockSlotRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertSlot, bool>>>()))
                         .ReturnsAsync(new List<ExpertSlot>());
            _mockCertRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationCertificate, bool>>>()))
                         .ReturnsAsync(new List<ExpertApplicationCertificate>());
            _mockAppSpecRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationSpecialization, bool>>>()))
                            .ReturnsAsync(new List<ExpertApplicationSpecialization>());
            _mockSpecRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Specialization, bool>>>()))
                         .ReturnsAsync(new List<Specialization>());

            // Filter for rating >= 4.9 which doesn't match rating 3.0
            var query = new ExpertSearchQueryParameters { MinRating = 4.9m };
            var result = await _service.SearchExpertsAsync(query);

            result.Should().NotBeNull();
            result.Items.Should().BeEmpty();
            result.Pagination.TotalItems.Should().Be(0);
            result.Suggestion.Should().NotBeNull();
        }

        [Fact]
        public async Task GetExpertDetailByIdAsync_EmptyGuid_ThrowsNotFoundException()
        {
            _mockProfileRepo.Setup(r => r.GetByIdAsync(Guid.Empty)).ReturnsAsync((ExpertProfile?)null);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.GetExpertDetailByIdAsync(Guid.Empty));
        }

        [Fact]
        public async Task GetExpertDetailByIdAsync_NonAnonymousReview_DisplaysFullName()
        {
            var profileId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var appId = Guid.NewGuid();
            var reviewerId = Guid.NewGuid();

            var profile = new ExpertProfile { Id = profileId, UserId = userId, LatestApplicationId = appId, IsActive = true };
            _mockProfileRepo.Setup(r => r.GetByIdAsync(profileId)).ReturnsAsync(profile);

            var app = new ExpertApplication { Id = appId, FullName = "Expert NonAnon" };
            _mockAppRepo.Setup(r => r.GetByIdAsync(appId)).ReturnsAsync(app);

            var user = new User { UserId = userId, FullName = "Expert NonAnon" };
            _mockUserRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

            var reviewerUser = new User { UserId = reviewerId, FullName = "Dang Van Lam" };
            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User> { reviewerUser });

            var review = new ExpertReview
            {
                Id = Guid.NewGuid(),
                ExpertProfileId = profileId,
                UserId = reviewerId,
                Rating = 5,
                Comment = "Rất tốt",
                IsPublished = true,
                IsAnonymous = false,
                CreatedAt = DateTimeOffset.UtcNow
            };
            _mockReviewRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertReview, bool>>>()))
                           .ReturnsAsync(new List<ExpertReview> { review });

            _mockSlotRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertSlot, bool>>>()))
                         .ReturnsAsync(new List<ExpertSlot>());
            _mockCertRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationCertificate, bool>>>()))
                         .ReturnsAsync(new List<ExpertApplicationCertificate>());
            _mockProposalRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationFeeProposal, bool>>>()))
                             .ReturnsAsync(new List<ExpertApplicationFeeProposal>());
            _mockAppSpecRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationSpecialization, bool>>>()))
                            .ReturnsAsync(new List<ExpertApplicationSpecialization>());
            _mockSpecRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Specialization, bool>>>()))
                         .ReturnsAsync(new List<Specialization>());

            var result = await _service.GetExpertDetailByIdAsync(profileId);

            result.Should().NotBeNull();
            result.RecentReviews.Should().HaveCount(1);
            result.RecentReviews[0].ReviewerDisplayName.Should().Be("Dang L***");
        }

        [Fact]
        public async Task GetExpertDetailByIdAsync_SlotsBeyond14Days_Excluded()
        {
            var profileId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var appId = Guid.NewGuid();

            var profile = new ExpertProfile { Id = profileId, UserId = userId, LatestApplicationId = appId, IsActive = true };
            _mockProfileRepo.Setup(r => r.GetByIdAsync(profileId)).ReturnsAsync(profile);

            var app = new ExpertApplication { Id = appId, FullName = "Expert Slot Horizon" };
            _mockAppRepo.Setup(r => r.GetByIdAsync(appId)).ReturnsAsync(app);

            var user = new User { UserId = userId, FullName = "Expert Slot Horizon" };
            _mockUserRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            // In range: slot in 3 days
            var slot1 = new ExpertSlot { Id = Guid.NewGuid(), ExpertProfileId = profileId, SlotDate = today.AddDays(3), StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(11, 0), IsActive = true, IsBooked = false };
            // Out of range: slot in 20 days (> 14 days)
            var slot2 = new ExpertSlot { Id = Guid.NewGuid(), ExpertProfileId = profileId, SlotDate = today.AddDays(20), StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(11, 0), IsActive = true, IsBooked = false };

            var slotsList = new List<ExpertSlot> { slot1, slot2 };
            _mockSlotRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertSlot, bool>>>()))
                         .ReturnsAsync((Expression<Func<ExpertSlot, bool>> expr) => slotsList.Where(expr.Compile()).ToList());

            _mockReviewRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertReview, bool>>>()))
                           .ReturnsAsync(new List<ExpertReview>());
            _mockCertRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationCertificate, bool>>>()))
                         .ReturnsAsync(new List<ExpertApplicationCertificate>());
            _mockProposalRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationFeeProposal, bool>>>()))
                             .ReturnsAsync(new List<ExpertApplicationFeeProposal>());
            _mockAppSpecRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationSpecialization, bool>>>()))
                            .ReturnsAsync(new List<ExpertApplicationSpecialization>());
            _mockSpecRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Specialization, bool>>>()))
                         .ReturnsAsync(new List<Specialization>());

            var result = await _service.GetExpertDetailByIdAsync(profileId);

            result.Should().NotBeNull();
            result.UpcomingAvailableSlots.Should().HaveCount(1);
            result.UpcomingAvailableSlots[0].Id.Should().Be(slot1.Id);
        }

        [Fact]
        public async Task GetExpertDetailByIdAsync_ZeroReviewsAndZeroSlots_ReturnsEmptyLists()
        {
            var profileId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var appId = Guid.NewGuid();

            var profile = new ExpertProfile { Id = profileId, UserId = userId, LatestApplicationId = appId, IsActive = true };
            _mockProfileRepo.Setup(r => r.GetByIdAsync(profileId)).ReturnsAsync(profile);

            var app = new ExpertApplication { Id = appId, FullName = "Expert Empty Subcollections" };
            _mockAppRepo.Setup(r => r.GetByIdAsync(appId)).ReturnsAsync(app);

            var user = new User { UserId = userId, FullName = "Expert Empty Subcollections" };
            _mockUserRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

            _mockReviewRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertReview, bool>>>()))
                           .ReturnsAsync(new List<ExpertReview>());
            _mockSlotRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertSlot, bool>>>()))
                         .ReturnsAsync(new List<ExpertSlot>());
            _mockCertRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationCertificate, bool>>>()))
                         .ReturnsAsync(new List<ExpertApplicationCertificate>());
            _mockProposalRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationFeeProposal, bool>>>()))
                             .ReturnsAsync(new List<ExpertApplicationFeeProposal>());
            _mockAppSpecRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationSpecialization, bool>>>()))
                            .ReturnsAsync(new List<ExpertApplicationSpecialization>());
            _mockSpecRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Specialization, bool>>>()))
                         .ReturnsAsync(new List<Specialization>());

            var result = await _service.GetExpertDetailByIdAsync(profileId);

            result.Should().NotBeNull();
            result.RecentReviews.Should().BeEmpty();
            result.UpcomingAvailableSlots.Should().BeEmpty();
            result.VerifiedCertificates.Should().BeEmpty();
            result.FeePackages.Should().BeEmpty();
        }

        #endregion
    }
}
