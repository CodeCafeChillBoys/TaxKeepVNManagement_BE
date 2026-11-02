using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TaxKeepVN.Application.Constants;
using TaxKeepVN.Application.DTOs.TaxDocumentTypes;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Implementations;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;
using Xunit;

namespace TaxKeepVN.UnitTests.Application.Services
{
    public class TaxDocumentTypeServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IGenericRepository<TaxDocumentType>> _mockRepo;
        private readonly Mock<ILogger<TaxDocumentTypeService>> _mockLogger;
        private readonly TaxDocumentTypeService _service;

        public TaxDocumentTypeServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockRepo = new Mock<IGenericRepository<TaxDocumentType>>();
            _mockLogger = new Mock<ILogger<TaxDocumentTypeService>>();

            _mockUnitOfWork.Setup(u => u.Repository<TaxDocumentType>()).Returns(_mockRepo.Object);

            _service = new TaxDocumentTypeService(_mockUnitOfWork.Object, _mockLogger.Object);
        }

        #region GetByCodeAsync

        [Fact]
        public async Task UTCID01_GetByCodeAsync_EmptyCode_ThrowsBadRequestException()
        {
            var ex = await Assert.ThrowsAsync<BadRequestException>(() => _service.GetByCodeAsync(""));
            ex.ErrorCode.Should().Be(ErrorCodes.InvalidCode);
        }

        [Fact]
        public async Task UTCID02_GetByCodeAsync_NotFound_ThrowsNotFoundException()
        {
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TaxDocumentType, bool>>>()))
                     .ReturnsAsync(new List<TaxDocumentType>());

            await Assert.ThrowsAsync<NotFoundException>(() => _service.GetByCodeAsync("NONEXISTENT"));
        }

        [Fact]
        public async Task UTCID03_GetByCodeAsync_Found_ReturnsDto()
        {
            var docType = new TaxDocumentType
            {
                Code = "MEDICAL_RECEIPT",
                Name = "Hóa đơn y tế",
                IsTaxEligible = true
            };

            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TaxDocumentType, bool>>>()))
                     .ReturnsAsync(new List<TaxDocumentType> { docType });

            var result = await _service.GetByCodeAsync("medical_receipt");

            result.Should().NotBeNull();
            result.Code.Should().Be("MEDICAL_RECEIPT");
            result.IsTaxEligible.Should().BeTrue();
        }

        #endregion

        #region CreateAsync

        [Fact]
        public async Task UTCID04_CreateAsync_DuplicateCode_ThrowsConflictException()
        {
            var dto = new CreateTaxDocumentTypeDto { Code = "DOC_01", Name = "Chứng từ 1" };
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TaxDocumentType, bool>>>()))
                     .ReturnsAsync(new List<TaxDocumentType> { new TaxDocumentType { Code = "DOC_01" } });

            var ex = await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(dto));
            ex.ErrorCode.Should().Be(ErrorCodes.DuplicateCode);
        }

        [Fact]
        public async Task UTCID05_CreateAsync_Valid_CreatesSuccessfully()
        {
            var dto = new CreateTaxDocumentTypeDto
            {
                Code = "EDUCATION_RECEIPT",
                Name = "Hóa đơn học phí",
                IsTaxEligible = true
            };

            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TaxDocumentType, bool>>>()))
                     .ReturnsAsync(new List<TaxDocumentType>());

            var result = await _service.CreateAsync(dto);

            result.Should().NotBeNull();
            result.Code.Should().Be("EDUCATION_RECEIPT");
            _mockRepo.Verify(r => r.AddAsync(It.IsAny<TaxDocumentType>()), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion

        #region GetAllAsync

        [Fact]
        public async Task UTCID06_GetAllAsync_WithoutFilter_ReturnsAll()
        {
            var data = new List<TaxDocumentType>
            {
                new TaxDocumentType { Code = "DOC1", Name = "Chứng từ 1", IsTaxEligible = true },
                new TaxDocumentType { Code = "DOC2", Name = "Chứng từ 2", IsTaxEligible = false }
            };

            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(data);

            var result = await _service.GetAllAsync(null);

            result.Should().HaveCount(2);
        }

        [Fact]
        public async Task UTCID07_GetAllAsync_WithFilters_FiltersCorrectly()
        {
            var data = new List<TaxDocumentType>
            {
                new TaxDocumentType { Code = "VAT_INVOICE", Name = "Hóa đơn VAT", IsTaxEligible = true },
                new TaxDocumentType { Code = "SALARY_SLIP", Name = "Phiếu lương", IsTaxEligible = false }
            };

            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(data);

            var query = new TaxDocumentTypeQueryParameters
            {
                IsTaxEligible = true,
                Search = "VAT"
            };

            var result = await _service.GetAllAsync(query);

            result.Should().HaveCount(1);
            result.First().Code.Should().Be("VAT_INVOICE");
        }

        #endregion

        #region UpdateAsync

        [Fact]
        public async Task UTCID08_UpdateAsync_NotFound_ThrowsNotFoundException()
        {
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TaxDocumentType, bool>>>()))
                     .ReturnsAsync(new List<TaxDocumentType>());

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.UpdateAsync("NONEXISTENT", new UpdateTaxDocumentTypeDto { Name = "Test" }));
        }

        [Fact]
        public async Task UTCID09_UpdateAsync_Valid_UpdatesSuccessfully()
        {
            var entity = new TaxDocumentType
            {
                Code = "MEDICAL_RECEIPT",
                Name = "Tên cũ",
                Description = "Mô tả cũ",
                IsTaxEligible = false
            };

            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TaxDocumentType, bool>>>()))
                     .ReturnsAsync(new List<TaxDocumentType> { entity });

            var dto = new UpdateTaxDocumentTypeDto
            {
                Name = "Tên mới",
                Description = "Mô tả mới",
                IsTaxEligible = true
            };

            var result = await _service.UpdateAsync("medical_receipt", dto);

            result.Should().NotBeNull();
            result.Name.Should().Be("Tên mới");
            result.Description.Should().Be("Mô tả mới");
            result.IsTaxEligible.Should().BeTrue();
            _mockRepo.Verify(r => r.Update(entity), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion
    }
}
