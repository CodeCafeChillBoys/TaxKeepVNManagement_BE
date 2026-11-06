using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using TaxKeepVN.Application.DTOs.TaxSettlement;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Implementations;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Domain.IRepositories;
using Xunit;

namespace TaxKeepVN.UnitTests.Application.Services
{
    public class TaxSettlementServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<ISystemConfigService> _mockConfigService;
        private readonly Mock<IGenericRepository<TaxSettlementDossier>> _mockDossierRepo;
        private readonly Mock<IGenericRepository<TaxDeductionPolicy>> _mockPolicyRepo;
        private readonly Mock<IGenericRepository<IncomeSource>> _mockIncomeRepo;
        private readonly Mock<IGenericRepository<Dependent>> _mockDependentRepo;
        private readonly TaxSettlementService _service;

        public TaxSettlementServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockConfigService = new Mock<ISystemConfigService>();
            _mockDossierRepo = new Mock<IGenericRepository<TaxSettlementDossier>>();
            _mockPolicyRepo = new Mock<IGenericRepository<TaxDeductionPolicy>>();
            _mockIncomeRepo = new Mock<IGenericRepository<IncomeSource>>();
            _mockDependentRepo = new Mock<IGenericRepository<Dependent>>();

            _mockUnitOfWork.Setup(u => u.Repository<TaxSettlementDossier>()).Returns(_mockDossierRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<TaxDeductionPolicy>()).Returns(_mockPolicyRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<IncomeSource>()).Returns(_mockIncomeRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(_mockDependentRepo.Object);

            _service = new TaxSettlementService(_mockUnitOfWork.Object, _mockConfigService.Object);
        }

        #region GetByIdAsync

        [Fact]
        public async Task UTCID01_GetByIdAsync_NotFound_ThrowsNotFoundException()
        {
            _mockDossierRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                            .ReturnsAsync((TaxSettlementDossier?)null);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.GetByIdAsync(Guid.NewGuid(), Guid.NewGuid()));
        }

        [Fact]
        public async Task UTCID02_GetByIdAsync_Forbidden_ThrowsForbiddenException()
        {
            var taxpayerId = Guid.NewGuid();
            var anotherUserId = Guid.NewGuid();
            var dossier = new TaxSettlementDossier { Id = Guid.NewGuid(), TaxpayerId = anotherUserId };

            _mockDossierRepo.Setup(r => r.GetByIdAsync(dossier.Id))
                            .ReturnsAsync(dossier);

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                _service.GetByIdAsync(dossier.Id, taxpayerId));
        }

        #endregion

        #region GetListAsync

        [Fact]
        public async Task UTCID03_GetListAsync_ReturnsList()
        {
            var taxpayerId = Guid.NewGuid();
            var dossiers = new List<TaxSettlementDossier>
            {
                new TaxSettlementDossier
                {
                    Id = Guid.NewGuid(),
                    TaxpayerId = taxpayerId,
                    TaxYear = 2024,
                    TaxPayable = 5000000m,
                    Status = "DRAFT"
                }
            };

            _mockDossierRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TaxSettlementDossier, bool>>>()))
                            .ReturnsAsync(dossiers);

            var result = await _service.GetListAsync(taxpayerId);

            result.Should().HaveCount(1);
            result[0].TaxYear.Should().Be(2024);
        }

        #endregion

        #region PreviewAsync & ExportAsync

        [Fact]
        public async Task UTCID04_PreviewAsync_2025_WithIncomesAndDependents_ReturnsPreview()
        {
            var taxpayerId = Guid.NewGuid();
            var mockBracketRepo = new Mock<IGenericRepository<TaxBracket>>();
            _mockUnitOfWork.Setup(u => u.Repository<TaxBracket>()).Returns(mockBracketRepo.Object);

            var mockDocRepo = new Mock<IGenericRepository<Document>>();
            _mockUnitOfWork.Setup(u => u.Repository<Document>()).Returns(mockDocRepo.Object);

            _mockPolicyRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TaxDeductionPolicy, bool>>>()))
                .ReturnsAsync(new List<TaxDeductionPolicy>
                {
                    new TaxDeductionPolicy
                    {
                        Id = Guid.NewGuid(),
                        AppliesFromYear = 2025,
                        PersonalMonthly = 11000000m,
                        DependentMonthly = 4400000m,
                        IsActive = true
                    }
                });

            mockBracketRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TaxBracket, bool>>>()))
                .ReturnsAsync(new List<TaxBracket>
                {
                    new TaxBracket { BracketNo = 1, FromMonthly = 0, ToMonthly = 5000000, Rate = 0.05m, QuickDeduct = 0, AppliesFromYear = 2025, IsActive = true },
                    new TaxBracket { BracketNo = 2, FromMonthly = 5000000, ToMonthly = 10000000, Rate = 0.10m, QuickDeduct = 250000, AppliesFromYear = 2025, IsActive = true }
                });

            var income = new IncomeSource
            {
                Id = Guid.NewGuid(),
                TaxpayerId = taxpayerId,
                TaxYear = 2025,
                CompanyName = "TechCorp",
                CompanyTaxCode = "0101234567",
                TotalIncome = 240000000m,
                TaxWithheld = 10000000m,
                InsuranceDeducted = 20000000m,
                IsActive = true
            };
            _mockIncomeRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<IncomeSource, bool>>>()))
                .ReturnsAsync(new List<IncomeSource> { income });

            var dependent = new Dependent
            {
                Id = Guid.NewGuid(),
                TaxpayerId = taxpayerId,
                FullName = "Nguyen Con",
                Relationship = DependentRelationship.CHILD,
                EffectiveFromMonth = "2025-01",
                EffectiveToMonth = "2025-12",
                Status = DependentStatus.ACTIVE,
                IsDeleted = false
            };
            _mockDependentRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Dependent, bool>>>()))
                .ReturnsAsync(new List<Dependent> { dependent });

            var request = new TaxSettlementPreviewRequest
            {
                TaxYear = 2025,
                CutoffDate = "2025-12-31",
                CharityDeduction = 5000000m
            };

            var result = await _service.PreviewAsync(taxpayerId, request);

            result.Should().NotBeNull();
            result.TaxYear.Should().Be(2025);
            result.TotalGrossIncome.Should().Be(240000000m);
            result.TotalInsuranceDeduction.Should().Be(20000000m);
            result.PersonalDeductionAmount.Should().Be(132000000m);
            result.DependentDeductionAmount.Should().Be(52800000m);
            result.CharityDeduction.Should().Be(5000000m);
            result.Status.Should().Be("DRAFT");
        }

        [Fact]
        public async Task UTCID05_PreviewAsync_2026_WithReceipts_ReturnsPreviewWithMedicalEduDeductions()
        {
            var taxpayerId = Guid.NewGuid();
            var mockBracketRepo = new Mock<IGenericRepository<TaxBracket>>();
            _mockUnitOfWork.Setup(u => u.Repository<TaxBracket>()).Returns(mockBracketRepo.Object);

            var mockDocRepo = new Mock<IGenericRepository<Document>>();
            _mockUnitOfWork.Setup(u => u.Repository<Document>()).Returns(mockDocRepo.Object);

            _mockPolicyRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TaxDeductionPolicy, bool>>>()))
                .ReturnsAsync(new List<TaxDeductionPolicy>
                {
                    new TaxDeductionPolicy
                    {
                        Id = Guid.NewGuid(),
                        AppliesFromYear = 2026,
                        PersonalMonthly = 15000000m,
                        DependentMonthly = 6000000m,
                        MedicalMaxYearly = 20000000m,
                        EducationMaxYearly = 30000000m,
                        IsActive = true
                    }
                });

            mockBracketRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TaxBracket, bool>>>()))
                .ReturnsAsync(new List<TaxBracket>
                {
                    new TaxBracket { BracketNo = 1, FromMonthly = 0, ToMonthly = 10000000, Rate = 0.05m, QuickDeduct = 0, AppliesFromYear = 2026, IsActive = true }
                });

            _mockIncomeRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<IncomeSource, bool>>>()))
                .ReturnsAsync(new List<IncomeSource>());
            _mockDependentRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Dependent, bool>>>()))
                .ReturnsAsync(new List<Dependent>());

            var period = new TaxPeriod { UserId = taxpayerId, TaxYear = 2026 };
            var docs = new List<Document>
            {
                new Document { DocTypeCode = "MEDICAL_RECEIPT", Status = "CONFIRMED", TotalAmount = 15000000m, Period = period },
                new Document { DocTypeCode = "EDUCATION_RECEIPT", Status = "CONFIRMED", TotalAmount = 25000000m, Period = period }
            };
            mockDocRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Document, bool>>>()))
                .ReturnsAsync(docs);

            var request = new TaxSettlementPreviewRequest
            {
                TaxYear = 2026,
                CutoffDate = "2026-12-31"
            };

            var result = await _service.PreviewAsync(taxpayerId, request);

            result.Should().NotBeNull();
            result.MedicalDeduction.Should().Be(15000000m);
            result.EducationDeduction.Should().Be(25000000m);
        }

        [Fact]
        public async Task UTCID06_ExportAsync_Valid_CreatesDossierAndReturnsLockedPreview()
        {
            var taxpayerId = Guid.NewGuid();
            var mockBracketRepo = new Mock<IGenericRepository<TaxBracket>>();
            _mockUnitOfWork.Setup(u => u.Repository<TaxBracket>()).Returns(mockBracketRepo.Object);

            var mockDocRepo = new Mock<IGenericRepository<Document>>();
            _mockUnitOfWork.Setup(u => u.Repository<Document>()).Returns(mockDocRepo.Object);

            _mockPolicyRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TaxDeductionPolicy, bool>>>()))
                .ReturnsAsync(new List<TaxDeductionPolicy>
                {
                    new TaxDeductionPolicy { AppliesFromYear = 2025, PersonalMonthly = 11000000m, DependentMonthly = 4400000m, IsActive = true }
                });

            mockBracketRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TaxBracket, bool>>>()))
                .ReturnsAsync(new List<TaxBracket>
                {
                    new TaxBracket { BracketNo = 1, FromMonthly = 0, ToMonthly = 5000000, Rate = 0.05m, AppliesFromYear = 2025, IsActive = true }
                });

            var incomeId = Guid.NewGuid();
            var income = new IncomeSource
            {
                Id = incomeId,
                TaxpayerId = taxpayerId,
                TaxYear = 2025,
                CompanyName = "ABC",
                TotalIncome = 100000000m,
                IsActive = true
            };
            _mockIncomeRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<IncomeSource, bool>>>()))
                .ReturnsAsync(new List<IncomeSource> { income });
            _mockDependentRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Dependent, bool>>>()))
                .ReturnsAsync(new List<Dependent>());

            var request = new TaxSettlementExportRequest
            {
                TaxYear = 2025,
                CutoffDate = "2025-12-31",
                SelectedIncomeSourceIds = new List<Guid> { incomeId },
                Note = "Bao cao quyet toan chinh thuc"
            };

            var result = await _service.ExportAsync(taxpayerId, request);

            result.Should().NotBeNull();
            result.Status.Should().Be("LOCKED");
            result.DossierId.Should().NotBeNull();
            _mockDossierRepo.Verify(r => r.AddAsync(It.IsAny<TaxSettlementDossier>()), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task UTCID07_GetByIdAsync_Valid_ReturnsPreview()
        {
            var taxpayerId = Guid.NewGuid();
            var dossierId = Guid.NewGuid();
            var dossier = new TaxSettlementDossier
            {
                Id = dossierId,
                TaxpayerId = taxpayerId,
                TaxYear = 2025,
                CutoffDate = new DateOnly(2025, 12, 31),
                TotalGrossIncome = 200000000m,
                TotalTaxWithheld = 10000000m,
                TotalInsuranceDeduction = 15000000m,
                PersonalDeductionMonths = 12,
                PersonalDeductionAmount = 132000000m,
                DependentDeductionPersonMonths = 0,
                DependentDeductionAmount = 0m,
                TotalDeductions = 147000000m,
                TaxableIncomeYearly = 53000000m,
                TaxableIncomeMonthly = 4416666m,
                TaxPayable = 2650000m,
                RefundAmount = 7350000m,
                DueAmount = 0m,
                Status = "LOCKED",
                CreatedAt = DateTime.UtcNow
            };

            _mockDossierRepo.Setup(r => r.GetByIdAsync(dossierId)).ReturnsAsync(dossier);

            var mockBracketRepo = new Mock<IGenericRepository<TaxBracket>>();
            _mockUnitOfWork.Setup(u => u.Repository<TaxBracket>()).Returns(mockBracketRepo.Object);

            _mockPolicyRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TaxDeductionPolicy, bool>>>()))
                .ReturnsAsync(new List<TaxDeductionPolicy>
                {
                    new TaxDeductionPolicy { AppliesFromYear = 2025, PersonalMonthly = 11000000m, DependentMonthly = 4400000m, IsActive = true }
                });

            mockBracketRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TaxBracket, bool>>>()))
                .ReturnsAsync(new List<TaxBracket>
                {
                    new TaxBracket { BracketNo = 1, FromMonthly = 0, ToMonthly = 5000000, Rate = 0.05m, AppliesFromYear = 2025, IsActive = true }
                });

            var result = await _service.GetByIdAsync(dossierId, taxpayerId);

            result.Should().NotBeNull();
            result.DossierId.Should().Be(dossierId);
            result.TaxYear.Should().Be(2025);
            result.TotalGrossIncome.Should().Be(200000000m);
            result.RefundAmount.Should().Be(7350000m);
        }

        #endregion
    }
}
