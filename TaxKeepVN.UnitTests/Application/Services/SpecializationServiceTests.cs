using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TaxKeepVN.Application.DTOs.Specializations;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Implementations;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;
using Xunit;

namespace TaxKeepVN.UnitTests.Application.Services
{
    public class SpecializationServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IGenericRepository<Specialization>> _mockRepo;
        private readonly Mock<ILogger<SpecializationService>> _mockLogger;
        private readonly SpecializationService _service;

        public SpecializationServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockRepo = new Mock<IGenericRepository<Specialization>>();
            _mockLogger = new Mock<ILogger<SpecializationService>>();

            _mockUnitOfWork.Setup(u => u.Repository<Specialization>()).Returns(_mockRepo.Object);

            _service = new SpecializationService(_mockUnitOfWork.Object, _mockLogger.Object);
        }

        #region GetAllAsync

        [Fact]
        public async Task UTCID01_GetAllAsync_ReturnsOrderedList()
        {
            var data = new List<Specialization>
            {
                new Specialization { Id = 1, Code = "PIT", Name = "Thuế TNCN", IsActive = true },
                new Specialization { Id = 2, Code = "CIT", Name = "Thuế TNDN", IsActive = true }
            };
            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(data);

            var result = await _service.GetAllAsync(new SpecializationQueryParameters());

            result.Should().HaveCount(2);
            result.First().Code.Should().Be("PIT");
        }

        [Fact]
        public async Task UTCID01_B_GetAllAsync_WithFilters_FiltersCorrectly()
        {
            var data = new List<Specialization>
            {
                new Specialization { Id = 1, Code = "PIT", Name = "Thuế TNCN", IsActive = true },
                new Specialization { Id = 2, Code = "CIT", Name = "Thuế TNDN", IsActive = false }
            };
            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(data);

            var query = new SpecializationQueryParameters
            {
                IsActive = true,
                Search = "TNCN"
            };

            var result = await _service.GetAllAsync(query);

            result.Should().HaveCount(1);
            result.First().Code.Should().Be("PIT");
        }

        #endregion

        #region GetByIdAsync

        [Fact]
        public async Task UTCID02_GetByIdAsync_Found_ReturnsDto()
        {
            var item = new Specialization { Id = 1, Code = "PIT", Name = "Thuế TNCN", IsActive = true };
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Specialization, bool>>>()))
                     .ReturnsAsync(new List<Specialization> { item });

            var result = await _service.GetByIdAsync(1);

            result.Should().NotBeNull();
            result.Code.Should().Be("PIT");
        }

        [Fact]
        public async Task UTCID03_GetByIdAsync_NotFound_ThrowsNotFoundException()
        {
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Specialization, bool>>>()))
                     .ReturnsAsync(new List<Specialization>());

            await Assert.ThrowsAsync<NotFoundException>(() => _service.GetByIdAsync(999));
        }

        #endregion

        #region GetByCodeAsync

        [Fact]
        public async Task UTCID04_GetByCodeAsync_EmptyCode_ThrowsBadRequestException()
        {
            var ex = await Assert.ThrowsAsync<BadRequestException>(() => _service.GetByCodeAsync("  "));
            ex.ErrorCode.Should().Be("INVALID_CODE");
        }

        [Fact]
        public async Task UTCID05_GetByCodeAsync_NotFound_ThrowsNotFoundException()
        {
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Specialization, bool>>>()))
                     .ReturnsAsync(new List<Specialization>());

            await Assert.ThrowsAsync<NotFoundException>(() => _service.GetByCodeAsync("UNKNOWN"));
        }

        [Fact]
        public async Task UTCID05_B_GetByCodeAsync_Found_ReturnsDto()
        {
            var item = new Specialization { Id = 1, Code = "PIT", Name = "Thuế TNCN", IsActive = true };
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Specialization, bool>>>()))
                     .ReturnsAsync(new List<Specialization> { item });

            var result = await _service.GetByCodeAsync("pit");

            result.Should().NotBeNull();
            result.Code.Should().Be("PIT");
        }

        #endregion

        #region CreateAsync

        [Fact]
        public async Task UTCID06_CreateAsync_DuplicateCode_ThrowsConflictException()
        {
            var dto = new CreateSpecializationDto { Code = "PIT", Name = "Thuế TNCN" };
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Specialization, bool>>>()))
                     .ReturnsAsync(new List<Specialization> { new Specialization { Code = "PIT" } });

            var ex = await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(dto));
            ex.ErrorCode.Should().Be("SPECIALIZATION_CODE_EXISTS");
        }

        [Fact]
        public async Task UTCID07_CreateAsync_Valid_ReturnsDto()
        {
            var dto = new CreateSpecializationDto { Code = "VAT", Name = "Thuế GTGT", IsActive = true };
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Specialization, bool>>>()))
                     .ReturnsAsync(new List<Specialization>());

            var result = await _service.CreateAsync(dto);

            result.Should().NotBeNull();
            result.Code.Should().Be("VAT");
            _mockRepo.Verify(r => r.AddAsync(It.IsAny<Specialization>()), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion

        #region UpdateAsync

        [Fact]
        public async Task UTCID08_UpdateAsync_NotFound_ThrowsNotFoundException()
        {
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Specialization, bool>>>()))
                     .ReturnsAsync(new List<Specialization>());

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.UpdateAsync(999, new UpdateSpecializationDto { Name = "Updated" }));
        }

        [Fact]
        public async Task UTCID09_UpdateAsync_Valid_UpdatesSuccessfully()
        {
            var item = new Specialization { Id = 1, Code = "PIT", Name = "Old Name", IsActive = false };
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Specialization, bool>>>()))
                     .ReturnsAsync(new List<Specialization> { item });

            var dto = new UpdateSpecializationDto
            {
                Name = "New Name",
                Description = "New Desc",
                IsActive = true
            };

            var result = await _service.UpdateAsync(1, dto);

            result.Should().NotBeNull();
            result.Name.Should().Be("New Name");
            result.Description.Should().Be("New Desc");
            result.IsActive.Should().BeTrue();
            _mockRepo.Verify(r => r.Update(item), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion

        #region DeleteAsync

        [Fact]
        public async Task UTCID10_DeleteAsync_NotFound_ThrowsNotFoundException()
        {
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Specialization, bool>>>()))
                     .ReturnsAsync(new List<Specialization>());

            await Assert.ThrowsAsync<NotFoundException>(() => _service.DeleteAsync(999));
        }

        [Fact]
        public async Task UTCID11_DeleteAsync_InUse_SoftDeletes()
        {
            var item = new Specialization { Id = 1, Code = "PIT", IsActive = true };
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Specialization, bool>>>()))
                     .ReturnsAsync(new List<Specialization> { item });

            var mockAppSpecRepo = new Mock<IGenericRepository<ExpertApplicationSpecialization>>();
            mockAppSpecRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationSpecialization, bool>>>()))
                           .ReturnsAsync(new List<ExpertApplicationSpecialization> { new ExpertApplicationSpecialization() });
            _mockUnitOfWork.Setup(u => u.Repository<ExpertApplicationSpecialization>()).Returns(mockAppSpecRepo.Object);

            var result = await _service.DeleteAsync(1);

            result.Should().BeTrue();
            item.IsActive.Should().BeFalse();
            _mockRepo.Verify(r => r.Update(item), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task UTCID12_DeleteAsync_NotInUse_HardDeletes()
        {
            var item = new Specialization { Id = 1, Code = "PIT", IsActive = true };
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Specialization, bool>>>()))
                     .ReturnsAsync(new List<Specialization> { item });

            var mockAppSpecRepo = new Mock<IGenericRepository<ExpertApplicationSpecialization>>();
            mockAppSpecRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ExpertApplicationSpecialization, bool>>>()))
                           .ReturnsAsync(new List<ExpertApplicationSpecialization>());
            _mockUnitOfWork.Setup(u => u.Repository<ExpertApplicationSpecialization>()).Returns(mockAppSpecRepo.Object);

            var result = await _service.DeleteAsync(1);

            result.Should().BeTrue();
            _mockRepo.Verify(r => r.Remove(item), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion
    }
}
