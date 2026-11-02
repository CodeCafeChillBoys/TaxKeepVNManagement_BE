using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TaxKeepVN.Application.DTOs.ConsultationFeeConfigurations;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Implementations;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Domain.IRepositories;
using Xunit;

namespace TaxKeepVN.UnitTests.Application.Services
{
    public class ConsultationFeeConfigServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IGenericRepository<ConsultationFeeConfiguration>> _mockRepo;
        private readonly Mock<ILogger<ConsultationFeeConfigService>> _mockLogger;
        private readonly ConsultationFeeConfigService _service;

        public ConsultationFeeConfigServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockRepo = new Mock<IGenericRepository<ConsultationFeeConfiguration>>();
            _mockLogger = new Mock<ILogger<ConsultationFeeConfigService>>();

            _mockUnitOfWork.Setup(u => u.Repository<ConsultationFeeConfiguration>()).Returns(_mockRepo.Object);

            _service = new ConsultationFeeConfigService(_mockUnitOfWork.Object, _mockLogger.Object);
        }

        #region GetByIdAsync

        [Fact]
        public async Task UTCID01_GetByIdAsync_NotFound_ThrowsNotFoundException()
        {
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationFeeConfiguration, bool>>>()))
                     .ReturnsAsync(new List<ConsultationFeeConfiguration>());

            await Assert.ThrowsAsync<NotFoundException>(() => _service.GetByIdAsync(999));
        }

        [Fact]
        public async Task UTCID02_GetByIdAsync_Found_ReturnsDto()
        {
            var config = new ConsultationFeeConfiguration
            {
                Id = 1,
                SessionType = SessionType.ONLINE_MEETING,
                DurationMinutes = 30,
                MinFee = 100000m,
                MaxFee = 500000m,
                IsActive = true
            };

            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationFeeConfiguration, bool>>>()))
                     .ReturnsAsync(new List<ConsultationFeeConfiguration> { config });

            var result = await _service.GetByIdAsync(1);

            result.Should().NotBeNull();
            result.DurationMinutes.Should().Be(30);
            result.MinFee.Should().Be(100000m);
        }

        #endregion

        #region CreateAsync

        [Fact]
        public async Task UTCID03_CreateAsync_MaxFeeLessThanMinFee_ThrowsBadRequestException()
        {
            var dto = new CreateConsultationFeeConfigDto
            {
                SessionType = SessionType.ONLINE_MEETING,
                DurationMinutes = 30,
                MinFee = 500000m,
                MaxFee = 200000m // Max < Min
            };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() => _service.CreateAsync(dto));
            ex.ErrorCode.Should().Be("INVALID_FEE_RANGE");
        }

        [Fact]
        public async Task UTCID04_CreateAsync_DuplicateConfig_ThrowsConflictException()
        {
            var dto = new CreateConsultationFeeConfigDto
            {
                SessionType = SessionType.ONLINE_MEETING,
                DurationMinutes = 30,
                MinFee = 100000m,
                MaxFee = 500000m
            };

            var existing = new ConsultationFeeConfiguration { Id = 1, SessionType = dto.SessionType, DurationMinutes = 30 };
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationFeeConfiguration, bool>>>()))
                     .ReturnsAsync(new List<ConsultationFeeConfiguration> { existing });

            var ex = await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(dto));
            ex.ErrorCode.Should().Be("FEE_CONFIG_EXISTS");
        }

        [Fact]
        public async Task UTCID05_CreateAsync_Valid_CreatesSuccessfully()
        {
            var dto = new CreateConsultationFeeConfigDto
            {
                SessionType = SessionType.ONLINE_MEETING,
                DurationMinutes = 45,
                MinFee = 150000m,
                MaxFee = 600000m,
                IsActive = true
            };

            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationFeeConfiguration, bool>>>()))
                     .ReturnsAsync(new List<ConsultationFeeConfiguration>());

            var result = await _service.CreateAsync(dto);

            result.Should().NotBeNull();
            result.DurationMinutes.Should().Be(45);
            _mockRepo.Verify(r => r.AddAsync(It.IsAny<ConsultationFeeConfiguration>()), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion

        #region GetAllAsync

        [Fact]
        public async Task UTCID06_GetAllAsync_WithoutFilter_ReturnsAllOrdered()
        {
            var data = new List<ConsultationFeeConfiguration>
            {
                new ConsultationFeeConfiguration { Id = 2, SessionType = SessionType.ONLINE_MEETING, DurationMinutes = 60, MinFee = 200000m, MaxFee = 600000m, IsActive = true },
                new ConsultationFeeConfiguration { Id = 1, SessionType = SessionType.ONLINE_MEETING, DurationMinutes = 30, MinFee = 100000m, MaxFee = 500000m, IsActive = true }
            };

            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(data);

            var result = await _service.GetAllAsync(null);

            result.Should().HaveCount(2);
            result.First().DurationMinutes.Should().Be(30);
            result.Last().DurationMinutes.Should().Be(60);
        }

        [Fact]
        public async Task UTCID07_GetAllAsync_WithFilters_FiltersCorrectly()
        {
            var data = new List<ConsultationFeeConfiguration>
            {
                new ConsultationFeeConfiguration { Id = 1, SessionType = SessionType.ONLINE_MEETING, DurationMinutes = 30, MinFee = 100000m, MaxFee = 500000m, IsActive = true },
                new ConsultationFeeConfiguration { Id = 2, SessionType = SessionType.ONLINE_MEETING, DurationMinutes = 60, MinFee = 200000m, MaxFee = 600000m, IsActive = false }
            };

            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(data);

            var query = new ConsultationFeeConfigQueryParameters
            {
                SessionType = SessionType.ONLINE_MEETING,
                IsActive = true
            };

            var result = await _service.GetAllAsync(query);

            result.Should().HaveCount(1);
            result.First().Id.Should().Be(1);
        }

        #endregion

        #region UpdateAsync

        [Fact]
        public async Task UTCID08_UpdateAsync_MaxFeeLessThanMinFee_ThrowsBadRequestException()
        {
            var dto = new UpdateConsultationFeeConfigDto
            {
                SessionType = SessionType.ONLINE_MEETING,
                DurationMinutes = 30,
                MinFee = 500000m,
                MaxFee = 200000m
            };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() => _service.UpdateAsync(1, dto));
            ex.ErrorCode.Should().Be("INVALID_FEE_RANGE");
        }

        [Fact]
        public async Task UTCID09_UpdateAsync_NotFound_ThrowsNotFoundException()
        {
            var dto = new UpdateConsultationFeeConfigDto
            {
                SessionType = SessionType.ONLINE_MEETING,
                DurationMinutes = 30,
                MinFee = 100000m,
                MaxFee = 500000m
            };

            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationFeeConfiguration, bool>>>()))
                     .ReturnsAsync(new List<ConsultationFeeConfiguration>());

            await Assert.ThrowsAsync<NotFoundException>(() => _service.UpdateAsync(999, dto));
        }

        [Fact]
        public async Task UTCID10_UpdateAsync_DuplicateWithOtherId_ThrowsConflictException()
        {
            var existing = new ConsultationFeeConfiguration { Id = 1, SessionType = SessionType.ONLINE_MEETING, DurationMinutes = 30 };
            var duplicate = new ConsultationFeeConfiguration { Id = 2, SessionType = SessionType.ONLINE_MEETING, DurationMinutes = 45 };

            var dto = new UpdateConsultationFeeConfigDto
            {
                SessionType = SessionType.ONLINE_MEETING,
                DurationMinutes = 45,
                MinFee = 100000m,
                MaxFee = 500000m
            };

            // First find by id returns existing
            // Second find returns duplicate
            var callCount = 0;
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationFeeConfiguration, bool>>>()))
                     .ReturnsAsync(() =>
                     {
                         callCount++;
                         return callCount == 1 ? new List<ConsultationFeeConfiguration> { existing } : new List<ConsultationFeeConfiguration> { duplicate };
                     });

            var ex = await Assert.ThrowsAsync<ConflictException>(() => _service.UpdateAsync(1, dto));
            ex.ErrorCode.Should().Be("FEE_CONFIG_EXISTS");
        }

        [Fact]
        public async Task UTCID11_UpdateAsync_Valid_UpdatesSuccessfully()
        {
            var entity = new ConsultationFeeConfiguration
            {
                Id = 1,
                SessionType = SessionType.ONLINE_MEETING,
                DurationMinutes = 30,
                MinFee = 100000m,
                MaxFee = 500000m,
                IsActive = true
            };

            var dto = new UpdateConsultationFeeConfigDto
            {
                SessionType = SessionType.ONLINE_MEETING,
                DurationMinutes = 30,
                MinFee = 200000m,
                MaxFee = 800000m,
                IsActive = true
            };

            var callCount = 0;
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationFeeConfiguration, bool>>>()))
                     .ReturnsAsync(() =>
                     {
                         callCount++;
                         return callCount == 1 ? new List<ConsultationFeeConfiguration> { entity } : new List<ConsultationFeeConfiguration>();
                     });

            var result = await _service.UpdateAsync(1, dto);

            result.Should().NotBeNull();
            result.MinFee.Should().Be(200000m);
            result.MaxFee.Should().Be(800000m);
            _mockRepo.Verify(r => r.Update(entity), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion

        #region DeleteAsync

        [Fact]
        public async Task UTCID12_DeleteAsync_NotFound_ThrowsNotFoundException()
        {
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationFeeConfiguration, bool>>>()))
                     .ReturnsAsync(new List<ConsultationFeeConfiguration>());

            await Assert.ThrowsAsync<NotFoundException>(() => _service.DeleteAsync(999));
        }

        [Fact]
        public async Task UTCID13_DeleteAsync_InUse_PerformsSoftDelete()
        {
            var entity = new ConsultationFeeConfiguration
            {
                Id = 1,
                SessionType = SessionType.ONLINE_MEETING,
                DurationMinutes = 30,
                IsActive = true
            };

            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationFeeConfiguration, bool>>>()))
                     .ReturnsAsync(new List<ConsultationFeeConfiguration> { entity });

            var mockProposalRepo = new Mock<IGenericRepository<ExpertApplicationFeeProposal>>();
            mockProposalRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationFeeProposal, bool>>>()))
                            .ReturnsAsync(new List<ExpertApplicationFeeProposal> { new ExpertApplicationFeeProposal() });
            _mockUnitOfWork.Setup(u => u.Repository<ExpertApplicationFeeProposal>()).Returns(mockProposalRepo.Object);

            var result = await _service.DeleteAsync(1);

            result.Should().BeTrue();
            entity.IsActive.Should().BeFalse();
            _mockRepo.Verify(r => r.Update(entity), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task UTCID14_DeleteAsync_NotInUse_PerformsHardDelete()
        {
            var entity = new ConsultationFeeConfiguration
            {
                Id = 1,
                SessionType = SessionType.ONLINE_MEETING,
                DurationMinutes = 30,
                IsActive = true
            };

            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationFeeConfiguration, bool>>>()))
                     .ReturnsAsync(new List<ConsultationFeeConfiguration> { entity });

            var mockProposalRepo = new Mock<IGenericRepository<ExpertApplicationFeeProposal>>();
            mockProposalRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationFeeProposal, bool>>>()))
                            .ReturnsAsync(new List<ExpertApplicationFeeProposal>());
            _mockUnitOfWork.Setup(u => u.Repository<ExpertApplicationFeeProposal>()).Returns(mockProposalRepo.Object);

            var result = await _service.DeleteAsync(1);

            result.Should().BeTrue();
            _mockRepo.Verify(r => r.Remove(entity), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion
    }
}
