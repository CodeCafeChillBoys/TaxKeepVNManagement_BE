using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TaxKeepVN.Application.Constants;
using TaxKeepVN.Application.Service.Implementations;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;
using Xunit;

namespace TaxKeepVN.UnitTests.Application.Services
{
    public class MedicalRuleEngineServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<ILogger<MedicalRuleEngineService>> _mockLogger;
        private readonly Mock<IGenericRepository<SystemConfig>> _mockConfigRepo;
        private readonly Mock<IGenericRepository<User>> _mockUserRepo;
        private readonly Mock<IGenericRepository<Dependent>> _mockDependentRepo;
        private readonly Mock<IGenericRepository<BhytCoverageCatalog>> _mockCatalogRepo;
        private readonly MedicalRuleEngineService _service;

        private readonly Guid _userId = Guid.NewGuid();
        private readonly User _taxpayer;

        public MedicalRuleEngineServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockLogger = new Mock<ILogger<MedicalRuleEngineService>>();
            _mockConfigRepo = new Mock<IGenericRepository<SystemConfig>>();
            _mockUserRepo = new Mock<IGenericRepository<User>>();
            _mockDependentRepo = new Mock<IGenericRepository<Dependent>>();
            _mockCatalogRepo = new Mock<IGenericRepository<BhytCoverageCatalog>>();

            _mockUnitOfWork.Setup(u => u.Repository<SystemConfig>()).Returns(_mockConfigRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(_mockUserRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(_mockDependentRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<BhytCoverageCatalog>()).Returns(_mockCatalogRepo.Object);

            _taxpayer = new User
            {
                UserId = _userId,
                FullName = "Nguyễn Văn A"
            };
            _mockUserRepo.Setup(r => r.GetByIdAsync(_userId)).ReturnsAsync(_taxpayer);

            // Default system configs
            var configs = new List<SystemConfig>
            {
                new() { ConfigKey = MedicalConfigKeys.MedicalRequireStatement, ConfigValue = "true", IsActive = true },
                new() { ConfigKey = MedicalConfigKeys.MedicalNoCardExcludedGroupCodes, ConfigValue = "12", IsActive = true },
                new() { ConfigKey = MedicalConfigKeys.MedicalStatementAmountTolerance, ConfigValue = "1000", IsActive = true }
            };
            _mockConfigRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SystemConfig, bool>>>())).ReturnsAsync(configs);

            // Default empty dependents & catalogs
            _mockDependentRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Dependent, bool>>>())).ReturnsAsync(new List<Dependent>());
            _mockCatalogRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<BhytCoverageCatalog, bool>>>())).ReturnsAsync(new List<BhytCoverageCatalog>());

            _service = new MedicalRuleEngineService(_mockUnitOfWork.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task TC01_InsuranceCard80_CopayCalculated_NonCoveredExcluded()
        {
            // TC1: Bảng kê có thẻ 80%:
            // 1. Khám: BH 50k, cùng trả 10k
            // 2. XN: cùng trả 20k
            // 3. Thuốc A: cùng trả 40k
            // 4. Bổ gan B: BH 0 (tỷ lệ 0%), tự trả 350k -> Loại
            // 5. Phí chọn BS: BH 0 (tỷ lệ 0%), tự trả 300k -> Loại
            // Kỳ vọng: Eligible = 70.000; 2 dòng EXCLUDED

            var receipt = new Document
            {
                Id = Guid.NewGuid(),
                DocTypeCode = MedicalDocTypes.MedicalReceipt,
                PatientName = "Nguyễn Văn A",
                SellerName = "Bệnh viện Đa khoa",
                TotalAmount = 770000m,
                IsNotReimbursed = true
            };

            var statement = new Document
            {
                Id = Guid.NewGuid(),
                ParentDocumentId = receipt.Id,
                DocTypeCode = MedicalDocTypes.MedicalCostStatement,
                PatientName = "Nguyễn Văn A",
                FacilityName = "Bệnh viện Đa khoa",
                HasInsuranceCard = true,
                InsuranceBenefitRate = 0.8m,
                Items = new List<DocumentItem>
                {
                    new() { ItemOrder = 1, ItemName = "Khám bệnh", HasInsuranceCard = true, InsuranceRate = 100m, PatientCoPay = 10000m, PatientSelfPay = 0m, UnitPrice = 50000m, UnitPriceInsurance = 50000m },
                    new() { ItemOrder = 2, ItemName = "Xét nghiệm máu", HasInsuranceCard = true, InsuranceRate = 100m, PatientCoPay = 20000m, PatientSelfPay = 0m, UnitPrice = 100000m, UnitPriceInsurance = 100000m },
                    new() { ItemOrder = 3, ItemName = "Thuốc A", HasInsuranceCard = true, InsuranceRate = 100m, PatientCoPay = 40000m, PatientSelfPay = 0m, UnitPrice = 200000m, UnitPriceInsurance = 200000m },
                    new() { ItemOrder = 4, ItemName = "Bổ gan B", HasInsuranceCard = true, InsuranceRate = 0m, PatientCoPay = 0m, PatientSelfPay = 350000m, UnitPrice = 350000m },
                    new() { ItemOrder = 5, ItemName = "Phí chọn BS", HasInsuranceCard = true, InsuranceRate = 0m, PatientCoPay = 0m, PatientSelfPay = 300000m, UnitPrice = 300000m, GroupCode = "12" }
                }
            };

            await _service.EvaluateClaimAsync(receipt, statement, _userId, 2026);

            receipt.MedicalClaimStatus.Should().Be(MedicalClaimStatus.ELIGIBLE);
            receipt.EligibleAmount.Should().Be(70000m);
            statement.Items.ElementAt(0).DeductionStatus.Should().Be(MedicalDeductionStatus.ELIGIBLE);
            statement.Items.ElementAt(1).DeductionStatus.Should().Be(MedicalDeductionStatus.ELIGIBLE);
            statement.Items.ElementAt(2).DeductionStatus.Should().Be(MedicalDeductionStatus.ELIGIBLE);
            statement.Items.ElementAt(3).DeductionStatus.Should().Be(MedicalDeductionStatus.EXCLUDED);
            statement.Items.ElementAt(4).DeductionStatus.Should().Be(MedicalDeductionStatus.EXCLUDED);
        }

        [Fact]
        public async Task TC02_MissingStatement_ClaimStatusIsMissingStatement_ZeroEligible()
        {
            // TC2: Hóa đơn y tế không kèm bảng kê -> MISSING_STATEMENT, eligible = 0
            var receipt = new Document
            {
                Id = Guid.NewGuid(),
                DocTypeCode = MedicalDocTypes.MedicalReceipt,
                PatientName = "Nguyễn Văn A",
                SellerName = "Phòng khám Đa khoa",
                TotalAmount = 500000m,
                IsNotReimbursed = true
            };

            await _service.EvaluateClaimAsync(receipt, null, _userId, 2026);

            receipt.MedicalClaimStatus.Should().Be(MedicalClaimStatus.MISSING_STATEMENT);
            receipt.EligibleAmount.Should().Be(0m);
        }

        [Fact]
        public async Task TC04_PriceDifference_SelfPayDefaultOptOut_EligibleCopayOnly()
        {
            // TC4: Có thẻ 80%, X-quang: cột 4 = 250k, cột 5 = 50k, cột 8 = 100%, cột 11 = 10k, cột 13 = 200k
            // Eligible = 10.000; self_pay_amount = 200.000 (PRICE_DIFFERENCE_SELF_PAY), opt-in mặc định false
            var receipt = new Document
            {
                Id = Guid.NewGuid(),
                DocTypeCode = MedicalDocTypes.MedicalReceipt,
                PatientName = "Nguyễn Văn A",
                TotalAmount = 210000m,
                IsNotReimbursed = true
            };

            var statement = new Document
            {
                Id = Guid.NewGuid(),
                ParentDocumentId = receipt.Id,
                DocTypeCode = MedicalDocTypes.MedicalCostStatement,
                PatientName = "Nguyễn Văn A",
                HasInsuranceCard = true,
                Items = new List<DocumentItem>
                {
                    new()
                    {
                        ItemOrder = 1,
                        ItemName = "Chụp X-quang",
                        HasInsuranceCard = true,
                        UnitPrice = 250000m,
                        UnitPriceInsurance = 50000m,
                        InsuranceRate = 100m,
                        PatientCoPay = 10000m,
                        PatientSelfPay = 200000m,
                        SelfPayOptIn = false
                    }
                }
            };

            await _service.EvaluateClaimAsync(receipt, statement, _userId, 2026);

            receipt.EligibleAmount.Should().Be(10000m);
            var item = statement.Items.First();
            item.DeductionStatus.Should().Be(MedicalDeductionStatus.ELIGIBLE);
            item.SelfPayAmount.Should().Be(200000m);
            item.SelfPayReason.Should().Be(MedicalDeductionReason.PRICE_DIFFERENCE_SELF_PAY);

            // Khi user bật opt-in -> eligible = 210.000
            item.SelfPayOptIn = true;
            await _service.EvaluateClaimAsync(receipt, statement, _userId, 2026);
            receipt.EligibleAmount.Should().Be(210000m);
        }

        [Fact]
        public async Task TC05_PatientNotTaxpayerOrDependent_Rejected()
        {
            // TC5: Tên người bệnh không phải NNT và không có trong NPT -> REJECTED
            var receipt = new Document
            {
                Id = Guid.NewGuid(),
                DocTypeCode = MedicalDocTypes.MedicalReceipt,
                PatientName = "Trần Thị B", // Không khớp "Nguyễn Văn A"
                TotalAmount = 500000m,
                IsNotReimbursed = true
            };

            var statement = new Document
            {
                Id = Guid.NewGuid(),
                PatientName = "Trần Thị B",
                Items = new List<DocumentItem>()
            };

            await _service.EvaluateClaimAsync(receipt, statement, _userId, 2026);

            receipt.MedicalClaimStatus.Should().Be(MedicalClaimStatus.REJECTED);
            receipt.EligibleAmount.Should().Be(0m);
        }

        [Fact]
        public async Task TC10_PackageSubLineAndCoveredVsNonCovered()
        {
            // TC10: Gói TBYT mục 10 có thẻ: thủy tinh thể (cột 8 = 100%, BH 3tr, cùng trả 600k, tự trả 11.877.200)
            // + dịch nhầy (cột 8 = 0%, tự trả 756k)
            var receipt = new Document
            {
                Id = Guid.NewGuid(),
                PatientName = "Nguyễn Văn A",
                TotalAmount = 13233200m,
                IsNotReimbursed = true
            };

            var statement = new Document
            {
                Id = Guid.NewGuid(),
                ParentDocumentId = receipt.Id,
                PatientName = "Nguyễn Văn A",
                HasInsuranceCard = true,
                Items = new List<DocumentItem>
                {
                    new()
                    {
                        ItemOrder = 1,
                        ItemName = "Thủy tinh thể IOL",
                        HasInsuranceCard = true,
                        GroupCode = "10.1",
                        UnitPrice = 14877200m,
                        UnitPriceInsurance = 3000000m,
                        InsuranceRate = 100m,
                        PatientCoPay = 600000m,
                        PatientSelfPay = 11877200m,
                        SelfPayOptIn = false
                    },
                    new()
                    {
                        ItemOrder = 2,
                        ItemName = "Dịch nhầy",
                        HasInsuranceCard = true,
                        GroupCode = "10.1",
                        UnitPrice = 756000m,
                        InsuranceRate = 0m,
                        PatientCoPay = 0m,
                        PatientSelfPay = 756000m
                    }
                }
            };

            await _service.EvaluateClaimAsync(receipt, statement, _userId, 2026);

            receipt.EligibleAmount.Should().Be(600000m);
            statement.Items.ElementAt(0).SelfPayAmount.Should().Be(11877200m);
            statement.Items.ElementAt(0).SelfPayReason.Should().Be(MedicalDeductionReason.PRICE_DIFFERENCE_SELF_PAY);
            statement.Items.ElementAt(1).DeductionStatus.Should().Be(MedicalDeductionStatus.EXCLUDED);
        }

        [Fact]
        public async Task TC13_PartialRateBHYT_SelfPayPartialRate()
        {
            // TC13: Có thẻ 80%, thuốc có tỷ lệ BHYT 50%: cột 4 = cột 5, cột 7 = 200k, cột 8 = 50%, cột 11 = 20k, cột 13 = 100k
            // Eligible = 20.000; self_pay_amount = 100.000 (PARTIAL_RATE_SELF_PAY)
            var receipt = new Document
            {
                Id = Guid.NewGuid(),
                PatientName = "Nguyễn Văn A",
                TotalAmount = 120000m,
                IsNotReimbursed = true
            };

            var statement = new Document
            {
                Id = Guid.NewGuid(),
                PatientName = "Nguyễn Văn A",
                HasInsuranceCard = true,
                Items = new List<DocumentItem>
                {
                    new()
                    {
                        ItemOrder = 1,
                        ItemName = "Thuốc đặc trị B",
                        HasInsuranceCard = true,
                        UnitPrice = 200000m,
                        UnitPriceInsurance = 200000m,
                        InsuranceRate = 50m,
                        PatientCoPay = 20000m,
                        PatientSelfPay = 100000m
                    }
                }
            };

            await _service.EvaluateClaimAsync(receipt, statement, _userId, 2026);

            receipt.EligibleAmount.Should().Be(20000m);
            var item = statement.Items.First();
            item.SelfPayAmount.Should().Be(100000m);
            item.SelfPayReason.Should().Be(MedicalDeductionReason.PARTIAL_RATE_SELF_PAY);
        }

        [Fact]
        public async Task TC14_NoCard_InCatalog_WithPrice_EligiblePriceMin()
        {
            // TC14: Không thẻ, X-quang có trong danh mục, cột 5 có in 50k, cột 7 = cột 13 = 250k
            // Eligible = 50.000; self_pay = 200.000 (PRICE_DIFFERENCE_SELF_PAY)
            var catalogs = new List<BhytCoverageCatalog>
            {
                new() { Name = "Chụp X-quang", NormalizedName = "chup x quang", IsActive = true }
            };
            _mockCatalogRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<BhytCoverageCatalog, bool>>>())).ReturnsAsync(catalogs);

            var receipt = new Document
            {
                Id = Guid.NewGuid(),
                PatientName = "Nguyễn Văn A",
                TotalAmount = 250000m,
                IsNotReimbursed = true
            };

            var statement = new Document
            {
                Id = Guid.NewGuid(),
                PatientName = "Nguyễn Văn A",
                HasInsuranceCard = false,
                Items = new List<DocumentItem>
                {
                    new()
                    {
                        ItemOrder = 1,
                        ItemName = "Chụp X-quang",
                        HasInsuranceCard = false,
                        Quantity = 1,
                        UnitPrice = 250000m,
                        HospitalAmount = 250000m,
                        UnitPriceInsurance = 50000m,
                        PatientSelfPay = 250000m
                    }
                }
            };

            await _service.EvaluateClaimAsync(receipt, statement, _userId, 2026);

            receipt.EligibleAmount.Should().Be(50000m);
            var item = statement.Items.First();
            item.DeductionStatus.Should().Be(MedicalDeductionStatus.ELIGIBLE);
            item.SelfPayAmount.Should().Be(200000m);
            item.SelfPayReason.Should().Be(MedicalDeductionReason.PRICE_DIFFERENCE_SELF_PAY);
        }

        [Fact]
        public async Task TC15_NoCard_InCatalog_NoPrice_NeedReview()
        {
            // TC15: Không thẻ, X-quang có trong danh mục, cột 5 trống -> NEED_REVIEW (NO_CARD_PRICE_UNKNOWN)
            var catalogs = new List<BhytCoverageCatalog>
            {
                new() { Name = "Chụp X-quang", NormalizedName = "chup x quang", IsActive = true }
            };
            _mockCatalogRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<BhytCoverageCatalog, bool>>>())).ReturnsAsync(catalogs);

            var receipt = new Document
            {
                Id = Guid.NewGuid(),
                PatientName = "Nguyễn Văn A",
                TotalAmount = 250000m,
                IsNotReimbursed = true
            };

            var statement = new Document
            {
                Id = Guid.NewGuid(),
                PatientName = "Nguyễn Văn A",
                HasInsuranceCard = false,
                Items = new List<DocumentItem>
                {
                    new()
                    {
                        ItemOrder = 1,
                        ItemName = "Chụp X-quang",
                        HasInsuranceCard = false,
                        Quantity = 1,
                        UnitPrice = 250000m,
                        HospitalAmount = 250000m,
                        UnitPriceInsurance = null, // Trống
                        PatientSelfPay = 250000m
                    }
                }
            };

            await _service.EvaluateClaimAsync(receipt, statement, _userId, 2026);

            receipt.MedicalClaimStatus.Should().Be(MedicalClaimStatus.NEED_REVIEW);
            var item = statement.Items.First();
            item.DeductionStatus.Should().Be(MedicalDeductionStatus.NEED_REVIEW);
            item.DeductionReason.Should().Be(MedicalDeductionReason.NO_CARD_PRICE_UNKNOWN);
        }

        [Fact]
        public async Task TC16_InsuranceCard_SelfPayUnknown_NeedReview()
        {
            // TC16: Có thẻ, cột 8 > 0, cột 13 > 0 nhưng cột 4 = cột 5, cột 6 = cột 8 = 100% -> NEED_REVIEW (SELF_PAY_UNKNOWN)
            var receipt = new Document
            {
                Id = Guid.NewGuid(),
                PatientName = "Nguyễn Văn A",
                TotalAmount = 150000m,
                IsNotReimbursed = true
            };

            var statement = new Document
            {
                Id = Guid.NewGuid(),
                PatientName = "Nguyễn Văn A",
                HasInsuranceCard = true,
                Items = new List<DocumentItem>
                {
                    new()
                    {
                        ItemOrder = 1,
                        ItemName = "Dịch vụ Z",
                        HasInsuranceCard = true,
                        UnitPrice = 100000m,
                        UnitPriceInsurance = 100000m,
                        ServiceRate = 100m,
                        InsuranceRate = 100m,
                        PatientCoPay = 20000m,
                        PatientSelfPay = 50000m // Không rõ lý do vì sao phát sinh tự trả khi cột 4=cột 5 và tỷ lệ 100%
                    }
                }
            };

            await _service.EvaluateClaimAsync(receipt, statement, _userId, 2026);

            receipt.MedicalClaimStatus.Should().Be(MedicalClaimStatus.NEED_REVIEW);
            var item = statement.Items.First();
            item.DeductionStatus.Should().Be(MedicalDeductionStatus.NEED_REVIEW);
            item.DeductionReason.Should().Be(MedicalDeductionReason.SELF_PAY_UNKNOWN);
        }
    }
}
