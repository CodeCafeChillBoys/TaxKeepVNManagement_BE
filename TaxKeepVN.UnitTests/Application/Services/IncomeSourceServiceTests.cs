using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.IncomeSources;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Implementations;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;
using Xunit;

namespace TaxKeepVN.UnitTests.Application.Services
{
    public class IncomeSourceServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IGenericRepository<IncomeSource>> _mockRepo;
        private readonly IncomeSourceService _service;

        public IncomeSourceServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockRepo = new Mock<IGenericRepository<IncomeSource>>();

            _mockUnitOfWork.Setup(u => u.Repository<IncomeSource>()).Returns(_mockRepo.Object);

            _service = new IncomeSourceService(_mockUnitOfWork.Object);
        }

        #region GetByIdAsync

        [Fact]
        public async Task UTCID01_GetByIdAsync_NotFound_ThrowsNotFoundException()
        {
            var id = Guid.NewGuid();
            var userId = Guid.NewGuid();
            _mockRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((IncomeSource?)null);

            await Assert.ThrowsAsync<NotFoundException>(() => _service.GetByIdAsync(id, userId));
        }

        [Fact]
        public async Task UTCID02_GetByIdAsync_Forbidden_ThrowsForbiddenException()
        {
            var id = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();

            var source = new IncomeSource { Id = id, TaxpayerId = ownerId };
            _mockRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(source);

            await Assert.ThrowsAsync<ForbiddenException>(() => _service.GetByIdAsync(id, otherUserId));
        }

        [Fact]
        public async Task UTCID03_GetByIdAsync_Found_ReturnsDto()
        {
            var id = Guid.NewGuid();
            var userId = Guid.NewGuid();

            var source = new IncomeSource
            {
                Id = id,
                TaxpayerId = userId,
                CompanyName = "FPT Software",
                CompanyTaxCode = "0101248141",
                TaxYear = 2024,
                TotalIncome = 120000000m
            };
            _mockRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(source);

            var result = await _service.GetByIdAsync(id, userId);

            result.Should().NotBeNull();
            result.CompanyName.Should().Be("FPT Software");
            result.TotalIncome.Should().Be(120000000m);
        }

        #endregion

        #region CreateAsync

        [Fact]
        public async Task UTCID04_CreateAsync_EmptyTaxCode_ThrowsBadRequestException()
        {
            var userId = Guid.NewGuid();
            var dto = new IncomeSourceCreateDto { CompanyTaxCode = "" };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() => _service.CreateAsync(userId, dto));
            ex.ErrorCode.Should().Be("INVALID_TAX_CODE");
        }

        [Fact]
        public async Task UTCID05_CreateAsync_InvalidTaxCodeFormat_ThrowsBadRequestException()
        {
            var userId = Guid.NewGuid();
            var dto = new IncomeSourceCreateDto { CompanyTaxCode = "123" }; // less than 10 digits

            var ex = await Assert.ThrowsAsync<BadRequestException>(() => _service.CreateAsync(userId, dto));
            ex.ErrorCode.Should().Be("INVALID_TAX_CODE_FORMAT");
        }

        [Fact]
        public async Task UTCID06_CreateAsync_DuplicateTaxCode_ThrowsConflictException()
        {
            var userId = Guid.NewGuid();
            var validMst = "0101248141"; // Valid MST checksum

            var dto = new IncomeSourceCreateDto
            {
                CompanyName = "Công ty ABC",
                CompanyTaxCode = validMst,
                TaxYear = 2024
            };

            var existing = new IncomeSource { CompanyTaxCode = validMst, TaxYear = 2024, TaxpayerId = userId };
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<IncomeSource, bool>>>()))
                     .ReturnsAsync(new List<IncomeSource> { existing });

            var ex = await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(userId, dto));
            ex.ErrorCode.Should().Be("DUPLICATE_TAX_CODE");
        }

        [Fact]
        public async Task UTCID07_CreateAsync_Valid_CreatesSuccessfully()
        {
            var userId = Guid.NewGuid();
            var validMst = "0101248141"; // Valid MST checksum

            var dto = new IncomeSourceCreateDto
            {
                CompanyName = "Công ty ABC",
                CompanyTaxCode = validMst,
                TaxYear = 2024,
                TotalIncome = 50000000m,
                TaxWithheld = 5000000m,
                InsuranceDeducted = 2000000m
            };

            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<IncomeSource, bool>>>()))
                     .ReturnsAsync(new List<IncomeSource>());

            var result = await _service.CreateAsync(userId, dto);

            result.Should().NotBeNull();
            result.CompanyName.Should().Be("Công ty ABC");
            _mockRepo.Verify(r => r.AddAsync(It.IsAny<IncomeSource>()), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion

        #region GetAllByUserIdAsync & GetSummaryByUserIdAsync

        [Fact]
        public async Task UTCID08_GetAllByUserIdAsync_FiltersAndSorts_ReturnsPagedResult()
        {
            var userId = Guid.NewGuid();
            var query = new IncomeSourceQueryParameters
            {
                TaxYear = 2024,
                IsActive = true,
                Search = "fpt",
                Sort = "-totalincome",
                Page = 1,
                Size = 10
            };

            var sources = new List<IncomeSource>
            {
                new IncomeSource
                {
                    Id = Guid.NewGuid(),
                    TaxpayerId = userId,
                    CompanyName = "FPT Software",
                    CompanyTaxCode = "0101248141",
                    TaxYear = 2024,
                    TotalIncome = 100000000m,
                    IsActive = true
                },
                new IncomeSource
                {
                    Id = Guid.NewGuid(),
                    TaxpayerId = userId,
                    CompanyName = "FPT Telecom",
                    CompanyTaxCode = "0101248141",
                    TaxYear = 2024,
                    TotalIncome = 200000000m,
                    IsActive = true
                }
            };

            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<IncomeSource, bool>>>()))
                .ReturnsAsync(sources);

            var result = await _service.GetAllByUserIdAsync(userId, query);

            result.Should().NotBeNull();
            result.Pagination.TotalItems.Should().Be(2);
            result.Items.First().TotalIncome.Should().Be(200000000m); // Descending sort
        }

        [Fact]
        public async Task UTCID09_GetSummaryByUserIdAsync_ReturnsAggregatedDto()
        {
            var userId = Guid.NewGuid();
            var taxYear = 2024;
            var sources = new List<IncomeSource>
            {
                new IncomeSource
                {
                    Id = Guid.NewGuid(),
                    TaxpayerId = userId,
                    TaxYear = taxYear,
                    TotalIncome = 100000000m,
                    TaxWithheld = 10000000m,
                    InsuranceDeducted = 8000000m
                },
                new IncomeSource
                {
                    Id = Guid.NewGuid(),
                    TaxpayerId = userId,
                    TaxYear = taxYear,
                    TotalIncome = 50000000m,
                    TaxWithheld = 5000000m,
                    InsuranceDeducted = 4000000m
                }
            };

            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<IncomeSource, bool>>>()))
                .ReturnsAsync(sources);

            var result = await _service.GetSummaryByUserIdAsync(userId, taxYear);

            result.Should().NotBeNull();
            result.TaxYear.Should().Be(taxYear);
            result.TotalIncome.Should().Be(150000000m);
            result.TotalTaxWithheld.Should().Be(15000000m);
            result.TotalInsuranceDeducted.Should().Be(12000000m);
            result.TotalSources.Should().Be(2);
        }

        #endregion

        #region UpdateAsync & DeleteAsync

        [Fact]
        public async Task UTCID10_UpdateAsync_DuplicateTaxCode_ThrowsConflictException()
        {
            var id = Guid.NewGuid();
            var otherId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var validMst = "0101248141";

            var existing = new IncomeSource { Id = id, TaxpayerId = userId, CompanyTaxCode = "0300123456", TaxYear = 2024 };
            _mockRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(existing);

            var duplicate = new IncomeSource { Id = otherId, TaxpayerId = userId, CompanyTaxCode = validMst, TaxYear = 2024 };
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<IncomeSource, bool>>>()))
                .ReturnsAsync(new List<IncomeSource> { duplicate });

            var dto = new IncomeSourceUpdateDto
            {
                CompanyName = "Cong ty Moi",
                CompanyTaxCode = validMst,
                TaxYear = 2024
            };

            var ex = await Assert.ThrowsAsync<ConflictException>(() => _service.UpdateAsync(id, userId, dto));
            ex.ErrorCode.Should().Be("DUPLICATE_TAX_CODE");
        }

        [Fact]
        public async Task UTCID11_UpdateAsync_Valid_UpdatesAndReturnsDto()
        {
            var id = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var validMst = "0101248141";

            var existing = new IncomeSource { Id = id, TaxpayerId = userId, CompanyTaxCode = "0300123456", TaxYear = 2024, CompanyName = "Old Co" };
            _mockRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(existing);

            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<IncomeSource, bool>>>()))
                .ReturnsAsync(new List<IncomeSource>());

            var dto = new IncomeSourceUpdateDto
            {
                CompanyName = "New Co",
                CompanyTaxCode = validMst,
                TaxYear = 2024,
                TotalIncome = 90000000m,
                TaxWithheld = 9000000m,
                InsuranceDeducted = 3000000m,
                IsActive = true
            };

            var result = await _service.UpdateAsync(id, userId, dto);

            result.Should().NotBeNull();
            result.CompanyName.Should().Be("New Co");
            result.TotalIncome.Should().Be(90000000m);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task UTCID12_DeleteAsync_Valid_RemovesAndSaves()
        {
            var id = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var existing = new IncomeSource { Id = id, TaxpayerId = userId };
            _mockRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(existing);

            await _service.DeleteAsync(id, userId);

            _mockRepo.Verify(r => r.Remove(existing), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion

        #region CrossCheckAsync

        [Fact]
        public async Task UTCID13_CrossCheckAsync_DiscrepanciesDetected_ReturnsExpectedDiffs()
        {
            var userId = Guid.NewGuid();
            var taxYear = 2024;
            var companyName = "FPT Software";

            var mockIncomeRepo = new Mock<IGenericRepository<Income>>();
            mockIncomeRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Income, bool>>>()))
                .ReturnsAsync(new List<Income>
                {
                    new Income
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        Year = taxYear,
                        OrganizationName = companyName,
                        TotalTaxableIncome = 50000000m,
                        TaxAlreadyDeducted = 5000000m,
                        InsuranceDeducted = 2000000m
                    }
                });
            _mockUnitOfWork.Setup(u => u.Repository<Income>()).Returns(mockIncomeRepo.Object);

            var request = new IncomeSourceCrossCheckRequestDto
            {
                TaxYear = taxYear,
                CompanyName = companyName,
                CertificateTotalIncome = 60000000m,
                CertificateTaxWithheld = 6000000m,
                CertificateInsuranceDeducted = 2500000m
            };

            var result = await _service.CrossCheckAsync(userId, request);

            result.Should().NotBeNull();
            result.SummedTotalIncome.Should().Be(50000000m);
            result.DiffTotalIncome.Should().Be(10000000m);
            result.DiffTaxWithheld.Should().Be(1000000m);
            result.DiffInsuranceDeducted.Should().Be(500000m);
            result.IsMatch.Should().BeFalse();
        }

        #endregion
    }
}
