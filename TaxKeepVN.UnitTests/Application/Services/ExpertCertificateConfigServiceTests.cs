using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TaxKeepVN.Application.DTOs.ExpertCertificateConfigurations;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Implementations;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;
using Xunit;

namespace TaxKeepVN.UnitTests.Application.Services
{
    public class ExpertCertificateConfigServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IGenericRepository<ExpertCertificateConfiguration>> _mockRepo;
        private readonly Mock<ILogger<ExpertCertificateConfigService>> _mockLogger;
        private readonly ExpertCertificateConfigService _service;

        public ExpertCertificateConfigServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockRepo = new Mock<IGenericRepository<ExpertCertificateConfiguration>>();
            _mockLogger = new Mock<ILogger<ExpertCertificateConfigService>>();

            _mockUnitOfWork.Setup(u => u.Repository<ExpertCertificateConfiguration>()).Returns(_mockRepo.Object);

            _service = new ExpertCertificateConfigService(_mockUnitOfWork.Object, _mockLogger.Object);
        }

        #region GetAllAsync

        [Fact]
        public async Task UTCID01_GetAllAsync_WithoutFilter_ReturnsAllOrdered()
        {
            var data = new List<ExpertCertificateConfiguration>
            {
                new ExpertCertificateConfiguration { Id = 2, Code = "CPA", Name = "Kế toán viên", DisplayOrder = 2 },
                new ExpertCertificateConfiguration { Id = 1, Code = "TAX_AGENT", Name = "Đại lý thuế", DisplayOrder = 1 }
            };

            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(data);

            var result = await _service.GetAllAsync(null);

            result.Should().HaveCount(2);
            result.First().Code.Should().Be("TAX_AGENT");
        }

        [Fact]
        public async Task UTCID02_GetAllAsync_WithFilters_FiltersCorrectly()
        {
            var data = new List<ExpertCertificateConfiguration>
            {
                new ExpertCertificateConfiguration { Id = 1, Code = "TAX_AGENT", Name = "Đại lý thuế", IsActive = true },
                new ExpertCertificateConfiguration { Id = 2, Code = "CPA", Name = "Kế toán", IsActive = false }
            };

            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(data);

            var query = new ExpertCertificateConfigQueryParameters
            {
                IsActive = true,
                Search = "thuế"
            };

            var result = await _service.GetAllAsync(query);

            result.Should().HaveCount(1);
            result.First().Code.Should().Be("TAX_AGENT");
        }

        #endregion

        #region GetByIdAsync

        [Fact]
        public async Task UTCID03_GetByIdAsync_NotFound_ThrowsNotFoundException()
        {
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertCertificateConfiguration, bool>>>()))
                     .ReturnsAsync(new List<ExpertCertificateConfiguration>());

            await Assert.ThrowsAsync<NotFoundException>(() => _service.GetByIdAsync(999));
        }

        [Fact]
        public async Task UTCID04_GetByIdAsync_Found_ReturnsDto()
        {
            var config = new ExpertCertificateConfiguration { Id = 1, Code = "CPA", Name = "Chứng chỉ CPA" };
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertCertificateConfiguration, bool>>>()))
                     .ReturnsAsync(new List<ExpertCertificateConfiguration> { config });

            var result = await _service.GetByIdAsync(1);

            result.Should().NotBeNull();
            result.Code.Should().Be("CPA");
        }

        #endregion

        #region CreateAsync

        [Fact]
        public async Task UTCID05_CreateAsync_DuplicateCode_ThrowsConflictException()
        {
            var request = new CreateExpertCertificateConfigRequest { Code = "CPA", Name = "Chứng chỉ CPA" };
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertCertificateConfiguration, bool>>>()))
                     .ReturnsAsync(new List<ExpertCertificateConfiguration> { new ExpertCertificateConfiguration { Code = "CPA" } });

            var ex = await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(request));
            ex.ErrorCode.Should().Be("CERTIFICATE_CONFIG_EXISTS");
        }

        [Fact]
        public async Task UTCID06_CreateAsync_Valid_CreatesSuccessfully()
        {
            var request = new CreateExpertCertificateConfigRequest
            {
                Code = "ACCA",
                Name = "Chứng chỉ ACCA",
                RequiresExpiryDate = true,
                IsActive = true
            };

            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertCertificateConfiguration, bool>>>()))
                     .ReturnsAsync(new List<ExpertCertificateConfiguration>());

            var result = await _service.CreateAsync(request);

            result.Should().NotBeNull();
            result.Code.Should().Be("ACCA");
            _mockRepo.Verify(r => r.AddAsync(It.IsAny<ExpertCertificateConfiguration>()), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion

        #region UpdateAsync

        [Fact]
        public async Task UTCID07_UpdateAsync_NotFound_ThrowsNotFoundException()
        {
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertCertificateConfiguration, bool>>>()))
                     .ReturnsAsync(new List<ExpertCertificateConfiguration>());

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.UpdateAsync(999, new UpdateExpertCertificateConfigRequest { Name = "Test" }));
        }

        [Fact]
        public async Task UTCID08_UpdateAsync_Valid_UpdatesSuccessfully()
        {
            var entity = new ExpertCertificateConfiguration { Id = 1, Code = "CPA", Name = "Old Name" };
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertCertificateConfiguration, bool>>>()))
                     .ReturnsAsync(new List<ExpertCertificateConfiguration> { entity });

            var request = new UpdateExpertCertificateConfigRequest { Name = "New CPA Name", IsActive = true };

            var result = await _service.UpdateAsync(1, request);

            result.Name.Should().Be("New CPA Name");
            _mockRepo.Verify(r => r.Update(entity), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion

        #region ToggleActiveAsync

        [Fact]
        public async Task UTCID09_ToggleActiveAsync_Valid_InvertsIsActive()
        {
            var entity = new ExpertCertificateConfiguration { Id = 1, IsActive = true };
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertCertificateConfiguration, bool>>>()))
                     .ReturnsAsync(new List<ExpertCertificateConfiguration> { entity });

            var result = await _service.ToggleActiveAsync(1);

            result.IsActive.Should().BeFalse();
            entity.IsActive.Should().BeFalse();
            _mockRepo.Verify(r => r.Update(entity), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion

        #region DeleteAsync

        [Fact]
        public async Task UTCID10_DeleteAsync_NotFound_ThrowsNotFoundException()
        {
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertCertificateConfiguration, bool>>>()))
                     .ReturnsAsync(new List<ExpertCertificateConfiguration>());

            await Assert.ThrowsAsync<NotFoundException>(() => _service.DeleteAsync(999));
        }

        [Fact]
        public async Task UTCID11_DeleteAsync_InUse_ThrowsBadRequestException()
        {
            var entity = new ExpertCertificateConfiguration { Id = 1, Code = "CPA" };
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertCertificateConfiguration, bool>>>()))
                     .ReturnsAsync(new List<ExpertCertificateConfiguration> { entity });

            var mockCertRepo = new Mock<IGenericRepository<ExpertApplicationCertificate>>();
            mockCertRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationCertificate, bool>>>()))
                        .ReturnsAsync(new List<ExpertApplicationCertificate> { new ExpertApplicationCertificate() });
            _mockUnitOfWork.Setup(u => u.Repository<ExpertApplicationCertificate>()).Returns(mockCertRepo.Object);

            var ex = await Assert.ThrowsAsync<BadRequestException>(() => _service.DeleteAsync(1));
            ex.ErrorCode.Should().Be("CANNOT_DELETE_IN_USE");
        }

        [Fact]
        public async Task UTCID12_DeleteAsync_NotInUse_RemovesSuccessfully()
        {
            var entity = new ExpertCertificateConfiguration { Id = 1, Code = "CPA" };
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertCertificateConfiguration, bool>>>()))
                     .ReturnsAsync(new List<ExpertCertificateConfiguration> { entity });

            var mockCertRepo = new Mock<IGenericRepository<ExpertApplicationCertificate>>();
            mockCertRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationCertificate, bool>>>()))
                        .ReturnsAsync(new List<ExpertApplicationCertificate>());
            _mockUnitOfWork.Setup(u => u.Repository<ExpertApplicationCertificate>()).Returns(mockCertRepo.Object);

            await _service.DeleteAsync(1);

            _mockRepo.Verify(r => r.Remove(entity), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion
    }
}
