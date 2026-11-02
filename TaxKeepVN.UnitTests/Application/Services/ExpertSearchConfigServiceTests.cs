using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using TaxKeepVN.Application.DTOs.SearchConfigs;
using TaxKeepVN.Application.Service.Implementations;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Domain.IRepositories;
using Xunit;

namespace TaxKeepVN.UnitTests.Application.Services
{
    public class ExpertSearchConfigServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IGenericRepository<ExpertSearchConfiguration>> _mockRepo;
        private readonly ExpertSearchConfigService _service;

        public ExpertSearchConfigServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockRepo = new Mock<IGenericRepository<ExpertSearchConfiguration>>();

            _mockUnitOfWork.Setup(u => u.Repository<ExpertSearchConfiguration>()).Returns(_mockRepo.Object);

            _service = new ExpertSearchConfigService(_mockUnitOfWork.Object);
        }

        #region GetConfigAsync

        [Fact]
        public async Task UTCID01_GetConfigAsync_NoEntityInDb_ReturnsDefaultDto()
        {
            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ExpertSearchConfiguration>());

            var result = await _service.GetConfigAsync();

            result.Should().NotBeNull();
            result.DefaultSortBy.Should().Be(ExpertSortBy.Recommended);
        }

        [Fact]
        public async Task UTCID02_GetConfigAsync_EntityExists_ReturnsMappedDto()
        {
            var entity = new ExpertSearchConfiguration
            {
                Id = 1,
                DefaultSortBy = ExpertSortBy.RatingDesc,
                RequireVerifiedCertificate = true,
                FeaturedSpecializationIds = "[1, 2, 3]",
                AllowedSortOptions = "[0, 1]",
                RatingWeight = 0.5,
                SearchInFullName = true
            };

            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ExpertSearchConfiguration> { entity });

            var result = await _service.GetConfigAsync();

            result.Should().NotBeNull();
            result.DefaultSortBy.Should().Be(ExpertSortBy.RatingDesc);
            result.RequireVerifiedCertificate.Should().BeTrue();
            result.FeaturedSpecializationIds.Should().BeEquivalentTo(new List<int> { 1, 2, 3 });
        }

        #endregion

        #region UpdateConfigAsync

        [Fact]
        public async Task UTCID03_UpdateConfigAsync_NoExistingConfig_AddsNewEntity()
        {
            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ExpertSearchConfiguration>());

            var dto = new ExpertSearchCriteriaConfigDto
            {
                DefaultSortBy = ExpertSortBy.FeeAsc,
                RequireVerifiedCertificate = false
            };

            var result = await _service.UpdateConfigAsync(dto);

            result.Should().NotBeNull();
            _mockRepo.Verify(r => r.AddAsync(It.IsAny<ExpertSearchConfiguration>()), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task UTCID04_UpdateConfigAsync_ConfigExists_UpdatesEntity()
        {
            var existing = new ExpertSearchConfiguration { Id = 1, DefaultSortBy = ExpertSortBy.Recommended };
            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ExpertSearchConfiguration> { existing });

            var dto = new ExpertSearchCriteriaConfigDto
            {
                DefaultSortBy = ExpertSortBy.ExperienceDesc,
                RequireVerifiedCertificate = true
            };

            var result = await _service.UpdateConfigAsync(dto);

            result.Should().NotBeNull();
            existing.DefaultSortBy.Should().Be(ExpertSortBy.ExperienceDesc);
            _mockRepo.Verify(r => r.Update(existing), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion

        #region ResetToDefaultsAsync

        [Fact]
        public async Task UTCID05_ResetToDefaultsAsync_ResetsToDefaults()
        {
            var existing = new ExpertSearchConfiguration { Id = 1 };
            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ExpertSearchConfiguration> { existing });

            var result = await _service.ResetToDefaultsAsync();

            result.Should().NotBeNull();
            _mockRepo.Verify(r => r.Update(existing), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion
    }
}
