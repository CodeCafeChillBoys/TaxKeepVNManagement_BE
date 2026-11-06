using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using TaxKeepVN.Application.DTOs.Requests.Income;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Implementations;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;
using Xunit;

namespace TaxKeepVN.UnitTests.Application.Services
{
    public class IncomeServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IGenericRepository<Income>> _mockIncomeRepo;
        private readonly Mock<IFileStorageService> _mockFileStorage;
        private readonly IncomeService _incomeService;

        public IncomeServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockIncomeRepo = new Mock<IGenericRepository<Income>>();
            _mockFileStorage = new Mock<IFileStorageService>();

            _mockUnitOfWork.Setup(u => u.Repository<Income>()).Returns(_mockIncomeRepo.Object);

            _incomeService = new IncomeService(
                _mockUnitOfWork.Object,
                _mockFileStorage.Object
            );
        }

        #region FN_INC_01: CreateIncomeAsync

        [Fact]
        public async Task UTCID01_CreateIncomeAsync_ValidRequest_Success()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var request = new CreateIncomeRequest
            {
                OrganizationName = "Công ty TNHH Phần mềm ABC",
                TaxIdNumber = "0312345678",
                Month = 5,
                Year = 2024,
                TotalTaxableIncome = 30000000m,
                InsuranceDeducted = 3150000m,
                TaxAlreadyDeducted = 2000000m
            };

            _mockIncomeRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Income, bool>>>()))
                           .ReturnsAsync(new List<Income>());

            // Act
            var result = await _incomeService.CreateIncomeAsync(userId, request);

            // Assert
            result.Should().NotBeNull();
            result.OrganizationName.Should().Be(request.OrganizationName);
            result.TotalTaxableIncome.Should().Be(30000000m);
            _mockIncomeRepo.Verify(r => r.AddAsync(It.IsAny<Income>()), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task UTCID02_CreateIncomeAsync_DuplicateIncome_ThrowsBadRequestException()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var request = new CreateIncomeRequest
            {
                OrganizationName = "Công ty FPT",
                Month = 10,
                Year = 2024
            };

            var existingIncome = new Income { Id = Guid.NewGuid(), OrganizationName = request.OrganizationName, Month = 10, Year = 2024 };
            _mockIncomeRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Income, bool>>>()))
                           .ReturnsAsync(new List<Income> { existingIncome });

            // Act & Assert
            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _incomeService.CreateIncomeAsync(userId, request));
            ex.ErrorCode.Should().Be("DUPLICATE_INCOME");
        }

        [Fact]
        public async Task UTCID03_CreateIncomeAsync_WithPayslipFile_SavesFileSuccessfully()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(1024);

            var request = new CreateIncomeRequest
            {
                OrganizationName = "Công ty ABC",
                Month = 1,
                Year = 2024,
                PayslipFile = mockFile.Object
            };

            _mockIncomeRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Income, bool>>>()))
                           .ReturnsAsync(new List<Income>());

            _mockFileStorage.Setup(f => f.SaveFileAsync(mockFile.Object, "incomes"))
                            .ReturnsAsync("https://storage.taxkeep.vn/incomes/slip.pdf");

            // Act
            var result = await _incomeService.CreateIncomeAsync(userId, request);

            // Assert
            result.PayslipFileUrl.Should().Be("https://storage.taxkeep.vn/incomes/slip.pdf");
            _mockFileStorage.Verify(f => f.SaveFileAsync(mockFile.Object, "incomes"), Times.Once);
        }

        #endregion

        #region FN_INC_02: GetIncomeByIdAsync

        [Fact]
        public async Task UTCID04_GetIncomeByIdAsync_Found_ReturnsDto()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var incomeId = Guid.NewGuid();
            var income = new Income
            {
                Id = incomeId,
                UserId = userId,
                OrganizationName = "FPT Software",
                TotalTaxableIncome = 25000000m
            };

            _mockIncomeRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Income, bool>>>()))
                           .ReturnsAsync(new List<Income> { income });

            // Act
            var result = await _incomeService.GetIncomeByIdAsync(userId, incomeId);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(incomeId);
            result.OrganizationName.Should().Be("FPT Software");
        }

        [Fact]
        public async Task UTCID05_GetIncomeByIdAsync_NotFound_ThrowsNotFoundException()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var incomeId = Guid.NewGuid();

            _mockIncomeRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Income, bool>>>()))
                           .ReturnsAsync(new List<Income>());

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() =>
                _incomeService.GetIncomeByIdAsync(userId, incomeId));
        }

        #endregion

        #region FN_INC_03: DeleteIncomeAsync

        [Fact]
        public async Task UTCID06_DeleteIncomeAsync_ValidId_DeletesSuccessfully()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var incomeId = Guid.NewGuid();
            var income = new Income { Id = incomeId, UserId = userId };

            _mockIncomeRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Income, bool>>>()))
                           .ReturnsAsync(new List<Income> { income });

            // Act
            await _incomeService.DeleteIncomeAsync(userId, incomeId);

            // Assert
            _mockIncomeRepo.Verify(r => r.Remove(income), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task UTCID07_DeleteIncomeAsync_NotFound_ThrowsNotFoundException()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var incomeId = Guid.NewGuid();

            _mockIncomeRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Income, bool>>>()))
                           .ReturnsAsync(new List<Income>());

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() =>
                _incomeService.DeleteIncomeAsync(userId, incomeId));
        }

        #endregion

        #region FN_INC_04: GetMyIncomesAsync

        [Fact]
        public async Task UTCID08_GetMyIncomesAsync_ReturnsGroupedIncomes()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var year = 2024;

            var incomes = new List<Income>
            {
                new Income
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    OrganizationName = "Công ty A",
                    TaxIdNumber = "0101234567",
                    Month = 2,
                    Year = year,
                    TotalTaxableIncome = 20000000m
                },
                new Income
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    OrganizationName = "Công ty A",
                    TaxIdNumber = "0101234567",
                    Month = 1,
                    Year = year,
                    TotalTaxableIncome = 20000000m
                },
                new Income
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    OrganizationName = "Công ty B",
                    TaxIdNumber = "0109999999",
                    Month = 1,
                    Year = year,
                    TotalTaxableIncome = 15000000m
                }
            };

            _mockIncomeRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Income, bool>>>()))
                           .ReturnsAsync(incomes);

            // Act
            var result = await _incomeService.GetMyIncomesAsync(userId, year);

            // Assert
            result.Should().HaveCount(2);
            var groupA = result.Find(g => g.OrganizationName == "Công ty A");
            groupA.Should().NotBeNull();
            groupA!.TotalIncomeCompany.Should().Be(40000000m);
            groupA.Details.Should().HaveCount(2);
            groupA.Details[0].Month.Should().Be(1); // Ordered by month
            groupA.Details[1].Month.Should().Be(2);
        }

        #endregion

        #region FN_INC_05: UpdateIncomeAsync

        [Fact]
        public async Task UTCID09_UpdateIncomeAsync_NotFound_ThrowsNotFoundException()
        {
            var userId = Guid.NewGuid();
            var incomeId = Guid.NewGuid();

            _mockIncomeRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Income, bool>>>()))
                           .ReturnsAsync(new List<Income>());

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _incomeService.UpdateIncomeAsync(userId, incomeId, new UpdateIncomeRequest()));
        }

        [Fact]
        public async Task UTCID10_UpdateIncomeAsync_Valid_UpdatesSuccessfully()
        {
            var userId = Guid.NewGuid();
            var incomeId = Guid.NewGuid();
            var existing = new Income
            {
                Id = incomeId,
                UserId = userId,
                OrganizationName = "Old Company",
                Month = 1,
                Year = 2024,
                TotalTaxableIncome = 10000000m
            };

            _mockIncomeRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Income, bool>>>()))
                           .ReturnsAsync(new List<Income> { existing });

            var request = new UpdateIncomeRequest
            {
                OrganizationName = "New Company",
                TaxIdNumber = "0123456789",
                Month = 2,
                Year = 2024,
                TotalTaxableIncome = 15000000m,
                InsuranceDeducted = 1500000m,
                TaxAlreadyDeducted = 1000000m,
                PayslipFileUrl = "/uploads/payslips/slip.pdf"
            };

            var result = await _incomeService.UpdateIncomeAsync(userId, incomeId, request);

            result.Should().NotBeNull();
            result.OrganizationName.Should().Be("New Company");
            result.TotalTaxableIncome.Should().Be(15000000m);
            _mockIncomeRepo.Verify(r => r.Update(existing), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion
    }
}
