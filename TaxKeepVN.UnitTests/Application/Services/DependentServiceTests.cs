using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Moq;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Requests.Dependent;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Implementations;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Domain.IRepositories;
using Xunit;

namespace TaxKeepVN.UnitTests.Application.Services
{
    public class DependentServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IMemoryCache> _mockCache;
        private readonly Mock<IConfiguration> _mockConfig;
        private readonly DependentService _dependentService;

        public DependentServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockCache = new Mock<IMemoryCache>();
            _mockConfig = new Mock<IConfiguration>();

            var mockCacheEntry = new Mock<ICacheEntry>();
            _mockCache.Setup(c => c.CreateEntry(It.IsAny<object>())).Returns(mockCacheEntry.Object);

            // Setup Mock Configuration cho DependentSettings:GroupOrders:CHILD
            var mockChildSection = new Mock<IConfigurationSection>();
            var childUnder18 = new Mock<IConfigurationSection>();
            childUnder18.Setup(c => c.Key).Returns("CHILD_UNDER_18");
            var childOver18Studying = new Mock<IConfigurationSection>();
            childOver18Studying.Setup(c => c.Key).Returns("CHILD_OVER_18_STUDYING");
            var childOver18Disabled = new Mock<IConfigurationSection>();
            childOver18Disabled.Setup(c => c.Key).Returns("CHILD_OVER_18_DISABLED");

            mockChildSection.Setup(s => s.GetChildren()).Returns(new[] { childUnder18.Object, childOver18Studying.Object, childOver18Disabled.Object });
            _mockConfig.Setup(c => c.GetSection("DependentSettings:GroupOrders:CHILD")).Returns(mockChildSection.Object);

            var secUnder18 = new Mock<IConfigurationSection>();
            secUnder18.Setup(s => s.Value).Returns("1");
            _mockConfig.Setup(c => c.GetSection("DependentSettings:GroupOrders:CHILD:CHILD_UNDER_18")).Returns(secUnder18.Object);

            var secStudying = new Mock<IConfigurationSection>();
            secStudying.Setup(s => s.Value).Returns("2");
            _mockConfig.Setup(c => c.GetSection("DependentSettings:GroupOrders:CHILD:CHILD_OVER_18_STUDYING")).Returns(secStudying.Object);

            var secDisabled = new Mock<IConfigurationSection>();
            secDisabled.Setup(s => s.Value).Returns("3");
            _mockConfig.Setup(c => c.GetSection("DependentSettings:GroupOrders:CHILD:CHILD_OVER_18_DISABLED")).Returns(secDisabled.Object);

            var defaultRels = new List<DependentRelationshipCatalog>
            {
                new() { Code = "CHILD", Name = "Con cái", DisplayOrder = 1, IsActive = true },
                new() { Code = "SPOUSE", Name = "Vợ/Chồng", DisplayOrder = 2, IsActive = true },
                new() { Code = "PARENT", Name = "Cha/Mẹ", DisplayOrder = 3, IsActive = true },
                new() { Code = "OTHER_DEPENDENT", Name = "Người phụ thuộc khác", DisplayOrder = 4, IsActive = true }
            };
            var mockRelRepo = new Mock<IGenericRepository<DependentRelationshipCatalog>>();
            mockRelRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentRelationshipCatalog, bool>>>()))
                .ReturnsAsync(defaultRels);
            _mockUnitOfWork.Setup(u => u.Repository<DependentRelationshipCatalog>()).Returns(mockRelRepo.Object);

            var defaultGroups = new List<DependentGroupCatalog>
            {
                new() { Code = "CHILD_UNDER_18", Name = "Con dưới 18 tuổi", RelationshipCode = "CHILD", MaxAge = 18, Level = 0, DisplayOrder = 1, IsActive = true },
                new() { Code = "CHILD_OVER_18_STUDYING", Name = "Con từ 18 tuổi trở lên đang đi học", RelationshipCode = "CHILD", MinAge = 18, Level = 1, RequireStudying = true, DisplayOrder = 2, IsActive = true },
                new() { Code = "CHILD_OVER_18_DISABLED", Name = "Con từ 18 tuổi trở lên bị khuyết tật", RelationshipCode = "CHILD", MinAge = 18, Level = 1, RequireDisabled = true, DisplayOrder = 3, IsActive = true },
                new() { Code = "SPOUSE_DISABLED", Name = "Vợ/Chồng khuyết tật", RelationshipCode = "SPOUSE", Level = 0, RequireDisabled = true, DisplayOrder = 4, IsActive = true },
                new() { Code = "SPOUSE_RETIRED", Name = "Vợ/Chồng ngoài độ tuổi lao động", RelationshipCode = "SPOUSE", Level = 1, DisplayOrder = 5, IsActive = true },
                new() { Code = "PARENT_DISABLED", Name = "Cha/Mẹ khuyết tật", RelationshipCode = "PARENT", Level = 0, RequireDisabled = true, DisplayOrder = 6, IsActive = true },
                new() { Code = "PARENT_RETIRED", Name = "Cha/Mẹ ngoài độ tuổi lao động", RelationshipCode = "PARENT", Level = 1, DisplayOrder = 7, IsActive = true },
                new() { Code = "OTHER_HELPLESS", Name = "Cá nhân không nơi nương tựa khác", RelationshipCode = "OTHER_DEPENDENT", Level = 0, DisplayOrder = 8, IsActive = true }
            };
            var mockGroupRepo = new Mock<IGenericRepository<DependentGroupCatalog>>();
            mockGroupRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentGroupCatalog, bool>>>()))
                .ReturnsAsync(defaultGroups);
            _mockUnitOfWork.Setup(u => u.Repository<DependentGroupCatalog>()).Returns(mockGroupRepo.Object);

            _dependentService = new DependentService(
                _mockUnitOfWork.Object,
                _mockCache.Object,
                _mockConfig.Object
            );
        }

        private void SetupDependentIncomeConfigs(params SystemConfig[] configs)
        {
            var configRepo = new Mock<IGenericRepository<SystemConfig>>();
            configRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SystemConfig, bool>>>()))
                .Returns((Expression<Func<SystemConfig, bool>> predicate) =>
                    Task.FromResult<IEnumerable<SystemConfig>>(configs.Where(predicate.Compile()).ToList()));
            _mockUnitOfWork.Setup(u => u.Repository<SystemConfig>()).Returns(configRepo.Object);
        }

        #region FN_DEP_01: CreateDependentAsync

        [Fact]
        public async Task UTCID01_CreateDependentAsync_InvalidRelationship_ThrowsBadRequestException()
        {
            var request = new CreateDependentRequest
            {
                Relationship = "INVALID_TYPE",
                CurrentGroup = "CHILD_UNDER_18"
            };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _dependentService.CreateDependentAsync(Guid.NewGuid(), request));
            ex.ErrorCode.Should().Be("INVALID_RELATIONSHIP");
        }

        [Fact]
        public async Task UTCID02_CreateDependentAsync_InvalidCurrentGroup_ThrowsBadRequestException()
        {
            var request = new CreateDependentRequest
            {
                Relationship = "CHILD",
                CurrentGroup = "INVALID_GROUP"
            };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _dependentService.CreateDependentAsync(Guid.NewGuid(), request));
            ex.ErrorCode.Should().Be("INVALID_CURRENT_GROUP");
        }

        [Fact]
        public async Task CreateDependentAsync_InvalidGender_ThrowsBadRequestException()
        {
            var request = new CreateDependentRequest
            {
                Relationship = "CHILD",
                CurrentGroup = "CHILD_UNDER_18",
                Gender = "UNKNOWN"
            };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _dependentService.CreateDependentAsync(Guid.NewGuid(), request));
            ex.ErrorCode.Should().Be("INVALID_GENDER");
        }

        [Fact]
        public async Task CreateParentBelowConfiguredAge_DoesNotRequireIncomeCommitment()
        {
            SetupDependentIncomeConfigs(
                new SystemConfig { ConfigKey = "DEPENDENT_PARENT_MALE_MAX_AGE", ConfigValue = "62", IsActive = true },
                new SystemConfig { ConfigKey = "DEPENDENT_PARENT_FEMALE_MAX_AGE", ConfigValue = "60", IsActive = true },
                new SystemConfig { ConfigKey = "DEPENDENT_PARENT_MONTHLY_INCOME_LIMIT_VND", ConfigValue = "1000000", IsActive = true });

            var dependentRepo = new Mock<IGenericRepository<Dependent>>();
            dependentRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Dependent, bool>>>()))
                .ReturnsAsync(new List<Dependent>());
            dependentRepo.Setup(r => r.AddAsync(It.IsAny<Dependent>())).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(dependentRepo.Object);

            var request = new CreateDependentRequest
            {
                FullName = "Nguyen Van Parent",
                Relationship = "PARENT",
                CurrentGroup = "PARENT_RETIRED",
                Gender = "MALE",
                BirthDate = DateTime.UtcNow.Date.AddYears(-61),
                CitizenId = "079201009999",
                EffectiveFromMonth = "2026-01",
                EffectiveToMonth = "2026-12"
            };

            var result = await _dependentService.CreateDependentAsync(Guid.NewGuid(), request);

            result.Gender.Should().Be("MALE");
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task CreateParentAtConfiguredAge_RequiresIncomeCommitment()
        {
            SetupDependentIncomeConfigs(
                new SystemConfig { ConfigKey = "DEPENDENT_PARENT_MALE_MAX_AGE", ConfigValue = "62", IsActive = true },
                new SystemConfig { ConfigKey = "DEPENDENT_PARENT_FEMALE_MAX_AGE", ConfigValue = "60", IsActive = true },
                new SystemConfig { ConfigKey = "DEPENDENT_PARENT_MONTHLY_INCOME_LIMIT_VND", ConfigValue = "1000000", IsActive = true });

            var request = new CreateDependentRequest
            {
                FullName = "Nguyen Van Parent",
                Relationship = "PARENT",
                CurrentGroup = "PARENT_RETIRED",
                Gender = "MALE",
                BirthDate = DateTime.UtcNow.Date.AddYears(-62),
                CitizenId = "079201009999",
                EffectiveFromMonth = "2026-01",
                EffectiveToMonth = "2026-12"
            };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _dependentService.CreateDependentAsync(Guid.NewGuid(), request));

            ex.ErrorCode.Should().Be("INCOME_COMMITMENT_REQUIRED");
            ex.Message.Should().Contain("1,000,000 VND");
        }

        [Fact]
        public async Task CreateParentWithoutAgeConfig_FailsWithConfigurationError()
        {
            SetupDependentIncomeConfigs();

            var request = new CreateDependentRequest
            {
                FullName = "Nguyen Van Parent",
                Relationship = "PARENT",
                CurrentGroup = "PARENT_RETIRED",
                Gender = "MALE",
                BirthDate = DateTime.UtcNow.Date.AddYears(-61),
                CitizenId = "079201009999",
                EffectiveFromMonth = "2026-01",
                EffectiveToMonth = "2026-12"
            };

            var ex = await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
                _dependentService.CreateDependentAsync(Guid.NewGuid(), request));

            ex.ErrorCode.Should().Be("DEPENDENT_CONFIG_NOT_CONFIGURED");
            ex.Message.Should().Contain("DEPENDENT_PARENT_MALE_MAX_AGE");
        }

        [Fact]
        public async Task CreateParentWithIncomeCommitment_Succeeds()
        {
            SetupDependentIncomeConfigs(
                new SystemConfig { ConfigKey = "DEPENDENT_PARENT_MALE_MAX_AGE", ConfigValue = "62", IsActive = true },
                new SystemConfig { ConfigKey = "DEPENDENT_PARENT_FEMALE_MAX_AGE", ConfigValue = "60", IsActive = true },
                new SystemConfig { ConfigKey = "DEPENDENT_PARENT_MONTHLY_INCOME_LIMIT_VND", ConfigValue = "1000000", IsActive = true });

            var dependentRepo = new Mock<IGenericRepository<Dependent>>();
            dependentRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Dependent, bool>>>()))
                .ReturnsAsync(new List<Dependent>());
            dependentRepo.Setup(r => r.AddAsync(It.IsAny<Dependent>())).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(dependentRepo.Object);

            var request = new CreateDependentRequest
            {
                FullName = "Nguyen Van Parent",
                Relationship = "PARENT",
                CurrentGroup = "PARENT_RETIRED",
                Gender = "MALE",
                BirthDate = DateTime.UtcNow.Date.AddYears(-62),
                CitizenId = "079201009999",
                EffectiveFromMonth = "2026-01",
                EffectiveToMonth = "2026-12",
                ConfirmIncomeBelowThreshold = true
            };

            var result = await _dependentService.CreateDependentAsync(Guid.NewGuid(), request);

            result.Gender.Should().Be("MALE");
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task UTCID03_CreateDependentAsync_MissingCitizenIdAndBirthCert_ThrowsBadRequestException()
        {
            var request = new CreateDependentRequest
            {
                Relationship = "CHILD",
                CurrentGroup = "CHILD_UNDER_18",
                Gender = "MALE",
                CitizenId = null,
                BirthCertNumber = ""
            };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _dependentService.CreateDependentAsync(Guid.NewGuid(), request));
            ex.ErrorCode.Should().Be("IDENTIFIER_REQUIRED");
        }

        [Fact]
        public async Task UTCID04_CreateDependentAsync_EffectiveFromGreaterThanTo_ThrowsBadRequestException()
        {
            var request = new CreateDependentRequest
            {
                Relationship = "CHILD",
                CurrentGroup = "CHILD_UNDER_18",
                Gender = "MALE",
                CitizenId = "079201009999",
                EffectiveFromMonth = "2024-12",
                EffectiveToMonth = "2024-01"
            };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _dependentService.CreateDependentAsync(Guid.NewGuid(), request));
            ex.ErrorCode.Should().Be("INVALID_PERIOD");
        }

        [Fact]
        public async Task UTCID04b_CreateDependentAsync_EffectiveBeforeBirthDate_ThrowsBadRequestException()
        {
            var request = new CreateDependentRequest
            {
                Relationship = "CHILD",
                CurrentGroup = "CHILD_UNDER_18",
                Gender = "MALE",
                CitizenId = "079201009999",
                BirthDate = new DateTime(2024, 5, 15),
                EffectiveFromMonth = "2024-04",
                EffectiveToMonth = "2024-12"
            };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _dependentService.CreateDependentAsync(Guid.NewGuid(), request));
            ex.ErrorCode.Should().Be("PERIOD_BEFORE_BIRTH");
            ex.Message.Should().Contain("không được trước tháng sinh");
        }

        [Fact]
        public async Task CreateDependentAsync_EffectiveEqualsBirthMonth_Succeeds()
        {
            var taxpayerId = Guid.NewGuid();
            var request = new CreateDependentRequest
            {
                FullName = "Nguyen Con",
                Relationship = "CHILD",
                CurrentGroup = "CHILD_UNDER_18",
                Gender = "female",
                CitizenId = "079201009999",
                BirthDate = new DateTime(2024, 5, 15),
                EffectiveFromMonth = "2024-05",
                EffectiveToMonth = "2024-12"
            };

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Dependent, bool>>>()))
                .ReturnsAsync(new List<Dependent>());
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var res = await _dependentService.CreateDependentAsync(taxpayerId, request);
            res.Should().NotBeNull();
            res.EffectiveFromMonth.Should().Be("2024-05");
            res.Gender.Should().Be("FEMALE");
        }

        [Fact]
        public async Task UTCID05_CreateDependentAsync_GroupRelationshipMismatch_ThrowsBadRequestException()
        {
            var request = new CreateDependentRequest
            {
                Relationship = "SPOUSE",
                CurrentGroup = "CHILD_UNDER_18"
            };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _dependentService.CreateDependentAsync(Guid.NewGuid(), request));
            ex.ErrorCode.Should().Be("GROUP_RELATIONSHIP_MISMATCH");
        }

        [Fact]
        public async Task UTCID06_CreateDependentAsync_CitizenIdOverlap_ThrowsBadRequestException()
        {
            var taxpayerId = Guid.NewGuid();
            var request = new CreateDependentRequest
            {
                FullName = "Nguyen Van Con",
                Relationship = "CHILD",
                CurrentGroup = "CHILD_UNDER_18",
                Gender = "MALE",
                CitizenId = "079201009999",
                EffectiveFromMonth = "2024-06",
                EffectiveToMonth = "2024-12"
            };

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Dependent, bool>>>()))
                .ReturnsAsync(new List<Dependent>
                {
                    new Dependent
                    {
                        CitizenId = "079201009999",
                        EffectiveFromMonth = "2024-01",
                        EffectiveToMonth = "2024-08"
                    }
                });
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _dependentService.CreateDependentAsync(taxpayerId, request));
            ex.ErrorCode.Should().Be("DEPENDENT_PERIOD_OVERLAP");
            ex.Message.Should().Contain("********9999");
            ex.Message.Should().NotContain("079201009999");
        }

        [Fact]
        public async Task CreateDependentAsync_OverlapWithDeletedDependent_IgnoredAndSucceeds()
        {
            var taxpayerId = Guid.NewGuid();
            var request = new CreateDependentRequest
            {
                FullName = "Nguyen Van Con",
                Relationship = "CHILD",
                CurrentGroup = "CHILD_UNDER_18",
                Gender = "MALE",
                CitizenId = "079201009999",
                EffectiveFromMonth = "2024-06",
                EffectiveToMonth = "2024-12"
            };

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            // Database only returns non-deleted records when queried with !d.IsDeleted
            mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Dependent, bool>>>()))
                .ReturnsAsync(new List<Dependent>());
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var res = await _dependentService.CreateDependentAsync(taxpayerId, request);
            res.Should().NotBeNull();
        }

        [Fact]
        public async Task CreateDependentAsync_SettlementLockedYear_ThrowsConflictException()
        {
            var taxpayerId = Guid.NewGuid();
            var request = new CreateDependentRequest
            {
                FullName = "Nguyen Van Con",
                Relationship = "CHILD",
                CurrentGroup = "CHILD_UNDER_18",
                Gender = "MALE",
                CitizenId = "079201009999",
                EffectiveFromMonth = "2025-06",
                EffectiveToMonth = "2025-12"
            };

            var mockSettlementRepo = new Mock<IGenericRepository<TaxSettlementDossier>>();
            mockSettlementRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TaxSettlementDossier, bool>>>()))
                .ReturnsAsync(new List<TaxSettlementDossier>
                {
                    new TaxSettlementDossier
                    {
                        TaxpayerId = taxpayerId,
                        TaxYear = 2025,
                        Status = "LOCKED"
                    }
                });
            _mockUnitOfWork.Setup(u => u.Repository<TaxSettlementDossier>()).Returns(mockSettlementRepo.Object);

            var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                _dependentService.CreateDependentAsync(taxpayerId, request));
            ex.ErrorCode.Should().Be("SETTLEMENT_LOCKED_PERIOD");
            ex.Message.Should().Contain("2025");
        }

        [Fact]
        public async Task UTCID07_CreateDependentAsync_BirthCertOverlap_ThrowsBadRequestException()
        {
            var taxpayerId = Guid.NewGuid();
            var request = new CreateDependentRequest
            {
                FullName = "Nguyen Van Em",
                Relationship = "CHILD",
                CurrentGroup = "CHILD_UNDER_18",
                Gender = "MALE",
                BirthCertNumber = "KS-999999",
                EffectiveFromMonth = "2024-05",
                EffectiveToMonth = "2024-10"
            };

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Dependent, bool>>>()))
                .ReturnsAsync(new List<Dependent>
                {
                    new Dependent
                    {
                        BirthCertNumber = "KS-999999",
                        EffectiveFromMonth = "2024-01",
                        EffectiveToMonth = "2024-06"
                    }
                });
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _dependentService.CreateDependentAsync(taxpayerId, request));
            ex.ErrorCode.Should().Be("DEPENDENT_PERIOD_OVERLAP");
            ex.Message.Should().Contain("**9999");
        }

        [Fact]
        public async Task UTCID08_CreateDependentAsync_ValidData_Success()
        {
            var taxpayerId = Guid.NewGuid();
            var request = new CreateDependentRequest
            {
                FullName = "Nguyen Van Con",
                Relationship = "CHILD",
                CurrentGroup = "CHILD_UNDER_18",
                Gender = "MALE",
                CitizenId = "079201009999",
                BirthDate = new DateTime(2015, 5, 20),
                EffectiveFromMonth = "2024-01",
                EffectiveToMonth = "2024-12",
                Note = "Ghi chú hợp lệ"
            };

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Dependent, bool>>>()))
                .ReturnsAsync(new List<Dependent>());
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var mockRuleRepo = new Mock<IGenericRepository<DependentDocumentRule>>();
            mockRuleRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocumentRule, bool>>>()))
                .ReturnsAsync(new List<DependentDocumentRule>
                {
                    new DependentDocumentRule { TargetGroup = "CHILD_UNDER_18", DocType = "BIRTH_CERTIFICATE", AlternativeGroup = "RELATION_PROOF", IsMandatory = true, IsActive = true },
                    new DependentDocumentRule { TargetGroup = "CHILD_UNDER_18", DocType = "BIRTH_CERTIFICATE", AlternativeGroup = "IDENTITY_PROOF", IsMandatory = true, IsActive = true }
                });
            _mockUnitOfWork.Setup(u => u.Repository<DependentDocumentRule>()).Returns(mockRuleRepo.Object);

            var result = await _dependentService.CreateDependentAsync(taxpayerId, request);

            result.Should().NotBeNull();
            result.FullName.Should().Be(request.FullName);
            result.Relationship.Should().Be("CHILD");
            result.CurrentGroup.Should().Be("CHILD_UNDER_18");
            result.RequiredDocuments.Should().Contain("BIRTH_CERTIFICATE");
            result.RequiredDocuments.Should().ContainSingle(document => document == "BIRTH_CERTIFICATE");
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion

        #region FN_DEP_02: GetDependentsAsync

        [Fact]
        public async Task UTCID09_GetDependentsAsync_FilterAndSort_ReturnsPagedResult()
        {
            var taxpayerId = Guid.NewGuid();
            var query = new DependentQueryParameters
            {
                Status = "PENDING_DOCUMENTS",
                Relationship = "CHILD",
                Search = "Nguyen",
                Sort = "-fullname",
                Page = 1,
                Size = 10
            };

            var dependents = new List<Dependent>
            {
                new Dependent
                {
                    Id = Guid.NewGuid(),
                    TaxpayerId = taxpayerId,
                    FullName = "Nguyen Van B",
                    Relationship = "CHILD",
                    CurrentGroup = "CHILD_UNDER_18",
                    Status = DependentStatus.PENDING_DOCUMENTS,
                    IsDeleted = false,
                    CreatedAt = DateTimeOffset.UtcNow
                },
                new Dependent
                {
                    Id = Guid.NewGuid(),
                    TaxpayerId = taxpayerId,
                    FullName = "Nguyen Van A",
                    Relationship = "CHILD",
                    CurrentGroup = "CHILD_UNDER_18",
                    Status = DependentStatus.PENDING_DOCUMENTS,
                    IsDeleted = false,
                    CreatedAt = DateTimeOffset.UtcNow
                }
            };

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Dependent, bool>>>()))
                .ReturnsAsync(dependents);
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var result = await _dependentService.GetDependentsAsync(taxpayerId, query);

            result.Should().NotBeNull();
            result.Pagination.TotalItems.Should().Be(2);
            result.Items.First().FullName.Should().Be("Nguyen Van B"); // Descending sort
        }

        #endregion

        #region FN_DEP_03: GetDependentByIdAsync

        [Fact]
        public async Task UTCID10_GetDependentByIdAsync_NotFoundOrDeleted_ThrowsNotFoundException()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync((Dependent?)null);
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _dependentService.GetDependentByIdAsync(taxpayerId, depId));

            // Case deleted
            var deletedDep = new Dependent { Id = depId, TaxpayerId = taxpayerId, IsDeleted = true };
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(deletedDep);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _dependentService.GetDependentByIdAsync(taxpayerId, depId));
        }

        [Fact]
        public async Task UTCID11_GetDependentByIdAsync_Forbidden_ThrowsForbiddenException()
        {
            var taxpayerId = Guid.NewGuid();
            var otherUser = Guid.NewGuid();
            var depId = Guid.NewGuid();

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(new Dependent
            {
                Id = depId,
                TaxpayerId = otherUser,
                IsDeleted = false
            });
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                _dependentService.GetDependentByIdAsync(taxpayerId, depId));
        }

        [Fact]
        public async Task UTCID12_GetDependentByIdAsync_Success_ReturnsDetail()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();
            var dependent = new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                FullName = "Nguyen Van Con",
                Relationship = "CHILD",
                CurrentGroup = "CHILD_UNDER_18",
                Status = DependentStatus.PENDING_DOCUMENTS,
                IsDeleted = false,
                CreatedAt = DateTimeOffset.UtcNow
            };

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(dependent);
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var mockDocRepo = new Mock<IGenericRepository<DependentDocument>>();
            mockDocRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocument, bool>>>()))
                .ReturnsAsync(new List<DependentDocument>
                {
                    new DependentDocument
                    {
                        Id = Guid.NewGuid(),
                        DependentId = depId,
                        DocType = "BIRTH_CERTIFICATE",
                        FileUrl = "/uploads/documents/doc.pdf",
                        FileMimeType = "application/pdf",
                        IsReadable = true,
                        UploadedAt = DateTime.UtcNow
                    }
                });
            _mockUnitOfWork.Setup(u => u.Repository<DependentDocument>()).Returns(mockDocRepo.Object);

            var mockRuleRepo = new Mock<IGenericRepository<DependentDocumentRule>>();
            mockRuleRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocumentRule, bool>>>()))
                .ReturnsAsync(new List<DependentDocumentRule>());
            _mockUnitOfWork.Setup(u => u.Repository<DependentDocumentRule>()).Returns(mockRuleRepo.Object);

            var result = await _dependentService.GetDependentByIdAsync(taxpayerId, depId);

            result.Should().NotBeNull();
            result.DependentId.Should().Be(depId);
            result.Documents.Should().HaveCount(1);
        }

        [Fact]
        public async Task UTCID12b_GetDependentByIdAsync_DuplicatesInDatabase_ReturnsOnlyLatestDocumentPerType()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();
            var dependent = new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                FullName = "Vu Thi Lan",
                Relationship = "PARENT",
                CurrentGroup = "PARENT_RETIRED",
                Status = DependentStatus.PENDING_DOCUMENTS,
                IsDeleted = false,
                CreatedAt = DateTimeOffset.UtcNow
            };

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(dependent);
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var latestCccdId = Guid.NewGuid();
            var mockDocRepo = new Mock<IGenericRepository<DependentDocument>>();
            mockDocRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocument, bool>>>()))
                .ReturnsAsync(new List<DependentDocument>
                {
                    new DependentDocument
                    {
                        Id = Guid.NewGuid(),
                        DependentId = depId,
                        DocType = "CITIZEN_ID",
                        FileUrl = "/uploads/cccd1.jpg",
                        UploadedAt = DateTime.UtcNow.AddHours(-2)
                    },
                    new DependentDocument
                    {
                        Id = latestCccdId,
                        DependentId = depId,
                        DocType = "CITIZEN_ID",
                        FileUrl = "/uploads/cccd_latest.jpg",
                        UploadedAt = DateTime.UtcNow
                    },
                    new DependentDocument
                    {
                        Id = Guid.NewGuid(),
                        DependentId = depId,
                        DocType = "CITIZEN_ID",
                        FileUrl = "/uploads/cccd2.jpg",
                        UploadedAt = DateTime.UtcNow.AddHours(-1)
                    }
                });
            _mockUnitOfWork.Setup(u => u.Repository<DependentDocument>()).Returns(mockDocRepo.Object);

            var mockRuleRepo = new Mock<IGenericRepository<DependentDocumentRule>>();
            mockRuleRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocumentRule, bool>>>()))
                .ReturnsAsync(new List<DependentDocumentRule>());
            _mockUnitOfWork.Setup(u => u.Repository<DependentDocumentRule>()).Returns(mockRuleRepo.Object);

            var result = await _dependentService.GetDependentByIdAsync(taxpayerId, depId);

            result.Should().NotBeNull();
            result.Documents.Should().HaveCount(1);
            result.Documents[0].DocId.Should().Be(latestCccdId);
            result.Documents[0].FileUrl.Should().Be("/uploads/cccd_latest.jpg");
        }

        #endregion

        #region FN_DEP_04: UpdateGroupAsync

        [Fact]
        public async Task UTCID13_UpdateGroupAsync_NotFound_ThrowsNotFoundException()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync((Dependent?)null);
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _dependentService.UpdateGroupAsync(taxpayerId, depId, new UpdateDependentGroupRequest { NewGroup = "CHILD_OVER_18_STUDYING" }));
        }

        [Fact]
        public async Task UTCID14_UpdateGroupAsync_Forbidden_ThrowsForbiddenException()
        {
            var taxpayerId = Guid.NewGuid();
            var otherUser = Guid.NewGuid();
            var depId = Guid.NewGuid();

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(new Dependent
            {
                Id = depId,
                TaxpayerId = otherUser,
                IsDeleted = false
            });
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                _dependentService.UpdateGroupAsync(taxpayerId, depId, new UpdateDependentGroupRequest { NewGroup = "CHILD_OVER_18_STUDYING" }));
        }

        [Fact]
        public async Task UTCID15_UpdateGroupAsync_InvalidOrSameGroup_ThrowsBadRequestException()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();
            var dependent = new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                Relationship = "CHILD",
                CurrentGroup = "CHILD_UNDER_18",
                IsDeleted = false
            };

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(dependent);
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            // Invalid group
            var ex1 = await Assert.ThrowsAsync<BadRequestException>(() =>
                _dependentService.UpdateGroupAsync(taxpayerId, depId, new UpdateDependentGroupRequest { NewGroup = "INVALID_GROUP" }));
            ex1.ErrorCode.Should().Be("INVALID_GROUP");

            // Same group
            var ex2 = await Assert.ThrowsAsync<BadRequestException>(() =>
                _dependentService.UpdateGroupAsync(taxpayerId, depId, new UpdateDependentGroupRequest { NewGroup = "CHILD_UNDER_18" }));
            ex2.ErrorCode.Should().Be("SAME_GROUP");
        }

        [Fact]
        public async Task UTCID16_UpdateGroupAsync_DowngradeNotAllowed_ThrowsBadRequestException()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();
            var dependent = new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                Relationship = "CHILD",
                CurrentGroup = "CHILD_OVER_18_STUDYING", // Order = 2
                IsDeleted = false
            };

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(dependent);
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            // New group: CHILD_UNDER_18 (Order = 1) -> 1 < 2
            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _dependentService.UpdateGroupAsync(taxpayerId, depId, new UpdateDependentGroupRequest { NewGroup = "CHILD_UNDER_18" }));
            ex.ErrorCode.Should().Be("GROUP_DOWNGRADE_NOT_ALLOWED");
        }

        [Fact]
        public async Task UTCID17_UpdateGroupAsync_Success_UpdatesAndNotifies()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();
            var dependent = new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                FullName = "Nguyen Van Con",
                Relationship = "CHILD",
                CurrentGroup = "CHILD_UNDER_18", // Order = 1
                BirthDate = DateTime.UtcNow.AddYears(-19),
                IsDeleted = false
            };

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(dependent);
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var mockDocRepo = new Mock<IGenericRepository<DependentDocument>>();
            mockDocRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocument, bool>>>()))
                .ReturnsAsync(new List<DependentDocument>());
            _mockUnitOfWork.Setup(u => u.Repository<DependentDocument>()).Returns(mockDocRepo.Object);

            var mockRuleRepo = new Mock<IGenericRepository<DependentDocumentRule>>();
            mockRuleRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocumentRule, bool>>>()))
                .ReturnsAsync(new List<DependentDocumentRule>());
            _mockUnitOfWork.Setup(u => u.Repository<DependentDocumentRule>()).Returns(mockRuleRepo.Object);

            var mockNotifRepo = new Mock<IGenericRepository<SystemNotification>>();
            _mockUnitOfWork.Setup(u => u.Repository<SystemNotification>()).Returns(mockNotifRepo.Object);

            var result = await _dependentService.UpdateGroupAsync(taxpayerId, depId, new UpdateDependentGroupRequest
            {
                NewGroup = "CHILD_OVER_18_STUDYING",
                Note = "Chuyen sang dai hoc"
            });

            result.Should().NotBeNull();
            result.PreviousGroup.Should().Be("CHILD_UNDER_18");
            result.CurrentGroup.Should().Be("CHILD_OVER_18_STUDYING");
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task UpdateGroupAsync_SoftDeleted_ThrowsNotFoundException()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                IsDeleted = true
            });
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _dependentService.UpdateGroupAsync(taxpayerId, depId, new UpdateDependentGroupRequest { NewGroup = "CHILD_OVER_18_STUDYING" }));
        }

        [Fact]
        public async Task UpdateGroupAsync_RelationshipMismatch_ThrowsBadRequestException()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();
            var dependent = new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                Relationship = "CHILD",
                CurrentGroup = "CHILD_UNDER_18",
                IsDeleted = false
            };

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(dependent);
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _dependentService.UpdateGroupAsync(taxpayerId, depId, new UpdateDependentGroupRequest { NewGroup = "SPOUSE_DISABLED" }));
            ex.ErrorCode.Should().Be("GROUP_RELATIONSHIP_MISMATCH");
        }

        [Fact]
        public async Task UpdateGroupAsync_NullNote_PreservesExistingNote()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();
            var dependent = new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                FullName = "Nguyen Van Con",
                Relationship = "CHILD",
                CurrentGroup = "CHILD_UNDER_18",
                BirthDate = DateTime.UtcNow.AddYears(-19),
                Note = "Ghi chu ban dau",
                IsDeleted = false
            };

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(dependent);
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var mockDocRepo = new Mock<IGenericRepository<DependentDocument>>();
            mockDocRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocument, bool>>>()))
                .ReturnsAsync(new List<DependentDocument>());
            _mockUnitOfWork.Setup(u => u.Repository<DependentDocument>()).Returns(mockDocRepo.Object);

            var mockRuleRepo = new Mock<IGenericRepository<DependentDocumentRule>>();
            mockRuleRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocumentRule, bool>>>()))
                .ReturnsAsync(new List<DependentDocumentRule>());
            _mockUnitOfWork.Setup(u => u.Repository<DependentDocumentRule>()).Returns(mockRuleRepo.Object);

            var mockNotifRepo = new Mock<IGenericRepository<SystemNotification>>();
            _mockUnitOfWork.Setup(u => u.Repository<SystemNotification>()).Returns(mockNotifRepo.Object);

            var result = await _dependentService.UpdateGroupAsync(taxpayerId, depId, new UpdateDependentGroupRequest
            {
                NewGroup = "CHILD_OVER_18_DISABLED",
                Note = null
            });

            result.Note.Should().Be("Ghi chu ban dau");
            dependent.Note.Should().Be("Ghi chu ban dau");
        }

        [Fact]
        public async Task UpdateGroupAsync_WithExistingDocuments_MapsDocumentsInResponse()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();
            var dependent = new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                FullName = "Nguyen Van Con",
                Relationship = "CHILD",
                CurrentGroup = "CHILD_UNDER_18",
                BirthDate = DateTime.UtcNow.AddYears(-19),
                IsDeleted = false
            };

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(dependent);
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var existingDoc = new DependentDocument
            {
                Id = Guid.NewGuid(),
                DependentId = depId,
                DocType = "BIRTH_CERTIFICATE",
                FileUrl = "https://storage.taxkeep.vn/docs/bc.pdf",
                FileMimeType = "application/pdf",
                IsReadable = true,
                UploadedAt = DateTime.UtcNow
            };

            var mockDocRepo = new Mock<IGenericRepository<DependentDocument>>();
            mockDocRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocument, bool>>>()))
                .ReturnsAsync(new List<DependentDocument> { existingDoc });
            _mockUnitOfWork.Setup(u => u.Repository<DependentDocument>()).Returns(mockDocRepo.Object);

            var mockRuleRepo = new Mock<IGenericRepository<DependentDocumentRule>>();
            mockRuleRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocumentRule, bool>>>()))
                .ReturnsAsync(new List<DependentDocumentRule>());
            _mockUnitOfWork.Setup(u => u.Repository<DependentDocumentRule>()).Returns(mockRuleRepo.Object);

            var mockNotifRepo = new Mock<IGenericRepository<SystemNotification>>();
            _mockUnitOfWork.Setup(u => u.Repository<SystemNotification>()).Returns(mockNotifRepo.Object);

            var result = await _dependentService.UpdateGroupAsync(taxpayerId, depId, new UpdateDependentGroupRequest
            {
                NewGroup = "CHILD_OVER_18_STUDYING"
            });

            result.Documents.Should().HaveCount(1);
            result.Documents[0].DocType.Should().Be("BIRTH_CERTIFICATE");
        }

        [Fact]
        public async Task UpdateGroupAsync_Under18_ToOver18Studying_ThrowsGroupAgeMismatch()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();
            var dependent = new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                FullName = "Nguyen Van Con",
                Relationship = "CHILD",
                CurrentGroup = "CHILD_UNDER_18",
                BirthDate = DateTime.UtcNow.AddYears(-15),
                IsDeleted = false
            };

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(dependent);
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _dependentService.UpdateGroupAsync(taxpayerId, depId, new UpdateDependentGroupRequest
                {
                    NewGroup = "CHILD_OVER_18_STUDYING"
                }));

            ex.ErrorCode.Should().Be("GROUP_AGE_MISMATCH");
            ex.Message.Should().Contain("chưa đủ 18 tuổi");
        }

        [Fact]
        public async Task UpdateGroupAsync_Under18_ToOver18Disabled_ThrowsGroupAgeMismatch()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();
            var dependent = new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                FullName = "Nguyen Van Con",
                Relationship = "CHILD",
                CurrentGroup = "CHILD_UNDER_18",
                BirthDate = DateTime.UtcNow.AddYears(-10),
                IsDeleted = false
            };

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(dependent);
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _dependentService.UpdateGroupAsync(taxpayerId, depId, new UpdateDependentGroupRequest
                {
                    NewGroup = "CHILD_OVER_18_DISABLED"
                }));

            ex.ErrorCode.Should().Be("GROUP_AGE_MISMATCH");
            ex.Message.Should().Contain("chưa đủ 18 tuổi");
        }

        #endregion

        #region FN_DEP_04_B: UpdateDependentAsync

        [Fact]
        public async Task UpdateDependentAsync_NotFound_ThrowsNotFoundException()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync((Dependent?)null);
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _dependentService.UpdateDependentAsync(taxpayerId, depId, new UpdateDependentRequest
                {
                    FullName = "Nguyen Van A",
                    EffectiveFromMonth = "2026-01",
                    EffectiveToMonth = "2026-12",
                    CitizenId = "079201009999"
                }));
        }

        [Fact]
        public async Task UpdateDependentAsync_Forbidden_ThrowsForbiddenException()
        {
            var taxpayerId = Guid.NewGuid();
            var otherUser = Guid.NewGuid();
            var depId = Guid.NewGuid();

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(new Dependent
            {
                Id = depId,
                TaxpayerId = otherUser,
                IsDeleted = false
            });
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                _dependentService.UpdateDependentAsync(taxpayerId, depId, new UpdateDependentRequest
                {
                    FullName = "Nguyen Van A",
                    EffectiveFromMonth = "2026-01",
                    EffectiveToMonth = "2026-12",
                    CitizenId = "079201009999"
                }));
        }

        [Fact]
        public async Task UpdateDependentAsync_IdentifierRequired_ThrowsBadRequestException()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                IsDeleted = false
            });
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _dependentService.UpdateDependentAsync(taxpayerId, depId, new UpdateDependentRequest
                {
                    FullName = "Nguyen Van A",
                    EffectiveFromMonth = "2026-01",
                    EffectiveToMonth = "2026-12",
                    CitizenId = "",
                    BirthCertNumber = ""
                }));

            ex.ErrorCode.Should().Be("IDENTIFIER_REQUIRED");
        }

        [Fact]
        public async Task UpdateDependentAsync_InvalidPeriod_ThrowsBadRequestException()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                IsDeleted = false
            });
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _dependentService.UpdateDependentAsync(taxpayerId, depId, new UpdateDependentRequest
                {
                    FullName = "Nguyen Van A",
                    CitizenId = "079201009999",
                    EffectiveFromMonth = "2026-12",
                    EffectiveToMonth = "2026-01"
                }));

            ex.ErrorCode.Should().Be("INVALID_PERIOD");
        }

        [Fact]
        public async Task UpdateDependentAsync_EffectiveBeforeBirthDate_ThrowsBadRequestException()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                IsDeleted = false,
                BirthDate = new DateTime(2024, 5, 15),
                Relationship = "CHILD",
                CurrentGroup = "CHILD_UNDER_18"
            });
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _dependentService.UpdateDependentAsync(taxpayerId, depId, new UpdateDependentRequest
                {
                    FullName = "Nguyen Van A",
                    CitizenId = "079201009999",
                    BirthDate = new DateTime(2024, 5, 15),
                    EffectiveFromMonth = "2024-04",
                    EffectiveToMonth = "2024-12"
                }));

            ex.ErrorCode.Should().Be("PERIOD_BEFORE_BIRTH");
            ex.Message.Should().Contain("không được trước tháng sinh");
        }

        [Fact]
        public async Task UpdateDependentAsync_EffectiveEqualsBirthMonth_Succeeds()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                IsDeleted = false,
                BirthDate = new DateTime(2024, 5, 15),
                Relationship = "CHILD",
                CurrentGroup = "CHILD_UNDER_18",
                CitizenId = "079201009999",
                EffectiveFromMonth = "2024-06",
                EffectiveToMonth = "2024-12"
            });
            mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Dependent, bool>>>()))
                .ReturnsAsync(new List<Dependent>());
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var res = await _dependentService.UpdateDependentAsync(taxpayerId, depId, new UpdateDependentRequest
            {
                BirthDate = new DateTime(2024, 5, 15),
                EffectiveFromMonth = "2024-05",
                EffectiveToMonth = "2024-12"
            });

            res.Should().NotBeNull();
            res.EffectiveFromMonth.Should().Be("2024-05");
        }

        [Fact]
        public async Task UpdateDependentAsync_SettlementLockedYear_ChangedMonthsAffectLockedYear_ThrowsConflictException()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                IsDeleted = false,
                BirthDate = new DateTime(2020, 1, 1),
                Relationship = "CHILD",
                CurrentGroup = "CHILD_UNDER_18",
                CitizenId = "079201009999",
                EffectiveFromMonth = "2025-01",
                EffectiveToMonth = "2026-12"
            });
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var mockSettlementRepo = new Mock<IGenericRepository<TaxSettlementDossier>>();
            mockSettlementRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TaxSettlementDossier, bool>>>()))
                .ReturnsAsync(new List<TaxSettlementDossier>
                {
                    new TaxSettlementDossier
                    {
                        TaxpayerId = taxpayerId,
                        TaxYear = 2025,
                        Status = "LOCKED"
                    }
                });
            _mockUnitOfWork.Setup(u => u.Repository<TaxSettlementDossier>()).Returns(mockSettlementRepo.Object);

            // Sửa kỳ từ 01/2025-12/2026 thành 06/2025-12/2026 -> ảnh hưởng năm 2025 (đã LOCKED) -> BỊ CHẶN!
            var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                _dependentService.UpdateDependentAsync(taxpayerId, depId, new UpdateDependentRequest
                {
                    EffectiveFromMonth = "2025-06",
                    EffectiveToMonth = "2026-12"
                }));

            ex.ErrorCode.Should().Be("SETTLEMENT_LOCKED_PERIOD");
            ex.Message.Should().Contain("2025");
        }

        [Fact]
        public async Task UpdateDependentAsync_SettlementLockedYear_ChangedMonthsDoNotAffectLockedYear_Succeeds()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                IsDeleted = false,
                BirthDate = new DateTime(2020, 1, 1),
                Relationship = "CHILD",
                CurrentGroup = "CHILD_UNDER_18",
                CitizenId = "079201009999",
                EffectiveFromMonth = "2025-01",
                EffectiveToMonth = "2026-12"
            });
            mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Dependent, bool>>>()))
                .ReturnsAsync(new List<Dependent>());
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var mockSettlementRepo = new Mock<IGenericRepository<TaxSettlementDossier>>();
            mockSettlementRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TaxSettlementDossier, bool>>>()))
                .ReturnsAsync(new List<TaxSettlementDossier>
                {
                    new TaxSettlementDossier
                    {
                        TaxpayerId = taxpayerId,
                        TaxYear = 2025,
                        Status = "LOCKED"
                    }
                });
            _mockUnitOfWork.Setup(u => u.Repository<TaxSettlementDossier>()).Returns(mockSettlementRepo.Object);

            // Sửa kỳ từ 01/2025-12/2026 thành 01/2025-06/2026 -> chỉ đổi tháng năm 2026. Năm 2025 không đổi -> QUA!
            var res = await _dependentService.UpdateDependentAsync(taxpayerId, depId, new UpdateDependentRequest
            {
                EffectiveFromMonth = "2025-01",
                EffectiveToMonth = "2026-06"
            });

            res.Should().NotBeNull();
            res.EffectiveToMonth.Should().Be("2026-06");
        }

        [Fact]
        public async Task UpdateDependentAsync_DifferentCurrentGroup_ThrowsBadRequestException()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                IsDeleted = false,
                BirthDate = new DateTime(2020, 1, 1),
                Relationship = "CHILD",
                CurrentGroup = "CHILD_UNDER_18",
                CitizenId = "079201009999"
            });
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _dependentService.UpdateDependentAsync(taxpayerId, depId, new UpdateDependentRequest
                {
                    CurrentGroup = "CHILD_OVER_18_STUDYING"
                }));

            ex.ErrorCode.Should().Be("USE_CHANGE_GROUP_API");
        }

        [Fact]
        public async Task UpdateDependentAsync_Active_IdentityChangedWithoutConfirmation_ThrowsConflictException()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                IsDeleted = false,
                Status = DependentStatus.ACTIVE,
                FullName = "Nguyen Van Cu",
                BirthDate = new DateTime(2020, 1, 1),
                Relationship = "CHILD",
                CurrentGroup = "CHILD_UNDER_18",
                CitizenId = "079201009999"
            });
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            // Đổi họ tên khi ACTIVE mà ConfirmIdentityChange = false -> 409 IDENTITY_CHANGE_REQUIRES_REVERIFY
            var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                _dependentService.UpdateDependentAsync(taxpayerId, depId, new UpdateDependentRequest
                {
                    FullName = "Nguyen Van Moi",
                    ConfirmIdentityChange = false
                }));

            ex.ErrorCode.Should().Be("IDENTITY_CHANGE_REQUIRES_REVERIFY");
        }

        [Fact]
        public async Task UpdateDependentAsync_Active_IdentityChangedWithConfirmation_SetsPendingDocumentsAndClearsComplete()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();

            var dependent = new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                IsDeleted = false,
                Status = DependentStatus.ACTIVE,
                IsProfileComplete = true,
                FullName = "Nguyen Van Cu",
                BirthDate = new DateTime(2020, 1, 1),
                Relationship = "CHILD",
                CurrentGroup = "CHILD_UNDER_18",
                CitizenId = "079201009999"
            };

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(dependent);
            mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Dependent, bool>>>()))
                .ReturnsAsync(new List<Dependent>());
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var mockNotifRepo = new Mock<IGenericRepository<SystemNotification>>();
            _mockUnitOfWork.Setup(u => u.Repository<SystemNotification>()).Returns(mockNotifRepo.Object);

            var res = await _dependentService.UpdateDependentAsync(taxpayerId, depId, new UpdateDependentRequest
            {
                FullName = "Nguyen Van Moi",
                ConfirmIdentityChange = true
            });

            res.Status.Should().Be("PENDING_DOCUMENTS");
            res.StatusChanged.Should().BeTrue();
            res.DocumentsNeedReupload.Should().BeTrue();
            dependent.Status.Should().Be(DependentStatus.PENDING_DOCUMENTS);
            dependent.IsProfileComplete.Should().BeFalse();
            dependent.IdentityUpdatedAt.Should().NotBeNull();
            mockNotifRepo.Verify(r => r.AddAsync(It.IsAny<SystemNotification>()), Times.Once);
        }

        [Fact]
        public async Task UpdateDependentAsync_Active_OnlyPeriodOrNoteChanged_StaysActive()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();

            var dependent = new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                IsDeleted = false,
                Status = DependentStatus.ACTIVE,
                IsProfileComplete = true,
                FullName = "Nguyen Van Cu",
                BirthDate = new DateTime(2020, 1, 1),
                Relationship = "CHILD",
                CurrentGroup = "CHILD_UNDER_18",
                CitizenId = "079201009999",
                EffectiveFromMonth = "2024-01",
                EffectiveToMonth = "2025-12",
                Note = "Ghi chu cu"
            };

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(dependent);
            mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Dependent, bool>>>()))
                .ReturnsAsync(new List<Dependent>());
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var res = await _dependentService.UpdateDependentAsync(taxpayerId, depId, new UpdateDependentRequest
            {
                Note = "Ghi chu moi",
                EffectiveToMonth = "2026-12"
            });

            res.Status.Should().Be("ACTIVE");
            res.StatusChanged.Should().BeFalse();
            res.DocumentsNeedReupload.Should().BeFalse();
            dependent.Status.Should().Be(DependentStatus.ACTIVE);
            dependent.IsProfileComplete.Should().BeTrue();
            dependent.IdentityUpdatedAt.Should().BeNull();
        }

        [Fact]
        public async Task UpdateDependentAsync_ChildUnder18NowOver18_OnlyPeriodOrNoteChanged_DoesNotThrowGroupAgeMismatch()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();

            // Sinh năm 2005 (nay đã 21 tuổi), nhưng nhóm cũ là CHILD_UNDER_18
            var dependent = new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                IsDeleted = false,
                Status = DependentStatus.ACTIVE,
                IsProfileComplete = true,
                FullName = "Nguyen Van Lon",
                BirthDate = new DateTime(2005, 5, 10, 0, 0, 0, DateTimeKind.Utc),
                Relationship = "CHILD",
                CurrentGroup = "CHILD_UNDER_18",
                CitizenId = "079205001111",
                EffectiveFromMonth = "2020-01",
                EffectiveToMonth = "2022-12",
                Note = "Ghi chu cu"
            };

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(dependent);
            mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Dependent, bool>>>()))
                .ReturnsAsync(new List<Dependent>());
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            // Sửa ghi chú và gia hạn kỳ hiệu lực mà không đổi ngày sinh
            var res = await _dependentService.UpdateDependentAsync(taxpayerId, depId, new UpdateDependentRequest
            {
                Note = "Ghi chu da tot nghiep THPT",
                EffectiveToMonth = "2023-05"
            });

            res.Should().NotBeNull();
            dependent.Note.Should().Be("Ghi chu da tot nghiep THPT");
            dependent.EffectiveToMonth.Should().Be("2023-05");
        }

        [Fact]
        public async Task UpdateDependentAsync_PendingDocuments_IdentityChangedWithExistingDocs_SetsIdentityUpdatedAt()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();

            var dependent = new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                IsDeleted = false,
                Status = DependentStatus.PENDING_DOCUMENTS,
                FullName = "Nguyen Van Cu",
                BirthDate = new DateTime(2020, 1, 1),
                Relationship = "CHILD",
                CurrentGroup = "CHILD_UNDER_18",
                CitizenId = "079201009999"
            };

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(dependent);
            mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Dependent, bool>>>()))
                .ReturnsAsync(new List<Dependent>());
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var mockDocRepo = new Mock<IGenericRepository<DependentDocument>>();
            mockDocRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocument, bool>>>()))
                .ReturnsAsync(new List<DependentDocument>
                {
                    new DependentDocument
                    {
                        Id = Guid.NewGuid(),
                        DependentId = depId,
                        DocType = "BIRTH_CERTIFICATE",
                        UploadedAt = DateTime.UtcNow.AddDays(-2)
                    }
                });
            _mockUnitOfWork.Setup(u => u.Repository<DependentDocument>()).Returns(mockDocRepo.Object);

            var res = await _dependentService.UpdateDependentAsync(taxpayerId, depId, new UpdateDependentRequest
            {
                FullName = "Nguyen Van Moi"
            });

            dependent.IdentityUpdatedAt.Should().NotBeNull();
            res.DocumentsNeedReupload.Should().BeTrue();
        }

        [Fact]
        public async Task UpdateDependentAsync_OmittedBirthCertNumber_PreservesExistingValue()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();

            var dependent = new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                IsDeleted = false,
                FullName = "Nguyen Van Cu",
                BirthDate = new DateTime(2020, 1, 1),
                Relationship = "CHILD",
                CurrentGroup = "CHILD_UNDER_18",
                BirthCertNumber = "KS-12345"
            };

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(dependent);
            mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Dependent, bool>>>()))
                .ReturnsAsync(new List<Dependent>());
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var res = await _dependentService.UpdateDependentAsync(taxpayerId, depId, new UpdateDependentRequest
            {
                FullName = "Nguyen Van Cu",
                BirthCertNumber = null // không gửi
            });

            dependent.BirthCertNumber.Should().Be("KS-12345");
        }

        [Fact]
        public async Task UpdateDependentAsync_CitizenIdOverlap_ThrowsBadRequestException()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();
            var otherDepId = Guid.NewGuid();

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                BirthDate = DateTime.UtcNow.AddYears(-10),
                IsDeleted = false
            });
            mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Dependent, bool>>>()))
                .ReturnsAsync(new List<Dependent>
                {
                    new Dependent
                    {
                        Id = otherDepId,
                        CitizenId = "079201009999",
                        EffectiveFromMonth = "2026-01",
                        EffectiveToMonth = "2026-06",
                        IsDeleted = false
                    }
                });
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _dependentService.UpdateDependentAsync(taxpayerId, depId, new UpdateDependentRequest
                {
                    FullName = "Nguyen Van A",
                    BirthDate = DateTime.UtcNow.AddYears(-10),
                    CitizenId = "079201009999",
                    EffectiveFromMonth = "2026-05",
                    EffectiveToMonth = "2026-12"
                }));

            ex.ErrorCode.Should().Be("DEPENDENT_PERIOD_OVERLAP");
            ex.Message.Should().Contain("********9999");
            ex.Message.Should().NotContain("079201009999");
        }

        [Fact]
        public async Task UpdateDependentAsync_Success_UpdatesAndReturnsResponse()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();

            var dependent = new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                FullName = "Ten Cu",
                Relationship = "CHILD",
                CurrentGroup = "CHILD_UNDER_18",
                BirthDate = DateTime.UtcNow.AddYears(-10),
                CitizenId = "079201001111",
                EffectiveFromMonth = "2026-01",
                EffectiveToMonth = "2026-06",
                Status = DependentStatus.PENDING_DOCUMENTS,
                IsDeleted = false
            };

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(dependent);
            mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Dependent, bool>>>()))
                .ReturnsAsync(new List<Dependent>());
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var mockDocRepo = new Mock<IGenericRepository<DependentDocument>>();
            mockDocRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocument, bool>>>()))
                .ReturnsAsync(new List<DependentDocument>());
            _mockUnitOfWork.Setup(u => u.Repository<DependentDocument>()).Returns(mockDocRepo.Object);

            var mockRuleRepo = new Mock<IGenericRepository<DependentDocumentRule>>();
            mockRuleRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocumentRule, bool>>>()))
                .ReturnsAsync(new List<DependentDocumentRule>());
            _mockUnitOfWork.Setup(u => u.Repository<DependentDocumentRule>()).Returns(mockRuleRepo.Object);

            var result = await _dependentService.UpdateDependentAsync(taxpayerId, depId, new UpdateDependentRequest
            {
                FullName = "Nguyen Van Moi",
                BirthDate = DateTime.UtcNow.AddYears(-12),
                CitizenId = "079201002222",
                EffectiveFromMonth = "2026-01",
                EffectiveToMonth = "2026-12",
                Note = "Cap nhat ghi chu"
            });

            result.Should().NotBeNull();
            result.FullName.Should().Be("Nguyen Van Moi");
            result.CitizenId.Should().Be("079201002222");
            result.EffectiveToMonth.Should().Be("2026-12");
            result.Note.Should().Be("Cap nhat ghi chu");
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion

        #region FN_DEP_05: DeleteDependentAsync

        [Fact]
        public async Task UTCID18_DeleteDependentAsync_NotFound_ThrowsNotFoundException()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync((Dependent?)null);
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _dependentService.DeleteDependentAsync(taxpayerId, depId, new DeleteDependentRequest()));
        }

        [Fact]
        public async Task UTCID19_DeleteDependentAsync_Forbidden_ThrowsForbiddenException()
        {
            var taxpayerId = Guid.NewGuid();
            var otherUser = Guid.NewGuid();
            var depId = Guid.NewGuid();

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(new Dependent
            {
                Id = depId,
                TaxpayerId = otherUser,
                IsDeleted = false
            });
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                _dependentService.DeleteDependentAsync(taxpayerId, depId, new DeleteDependentRequest()));
        }

        [Fact]
        public async Task UTCID20_DeleteDependentAsync_Success_SoftDeletes()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();
            var dependent = new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                FullName = "Nguyen Van Con",
                Relationship = "CHILD",
                CurrentGroup = "CHILD_UNDER_18",
                IsDeleted = false,
                Status = DependentStatus.ACTIVE
            };

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(dependent);
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var mockNotifRepo = new Mock<IGenericRepository<SystemNotification>>();
            _mockUnitOfWork.Setup(u => u.Repository<SystemNotification>()).Returns(mockNotifRepo.Object);

            var result = await _dependentService.DeleteDependentAsync(taxpayerId, depId, new DeleteDependentRequest { Reason = "Da co thu nhap" });

            result.Should().NotBeNull();
            dependent.IsDeleted.Should().BeTrue();
            dependent.Status.Should().Be(DependentStatus.INACTIVE);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task DeleteDependentAsync_AlreadyDeleted_ThrowsNotFoundException()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                IsDeleted = true
            });
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _dependentService.DeleteDependentAsync(taxpayerId, depId, new DeleteDependentRequest()));
        }

        [Fact]
        public async Task DeleteDependentAsync_NullOrWhitespaceReason_PreservesExistingNote()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();
            var dependent = new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                FullName = "Nguyen Van Con",
                Relationship = "CHILD",
                CurrentGroup = "CHILD_UNDER_18",
                Note = "Ghi chu cu",
                IsDeleted = false,
                Status = DependentStatus.ACTIVE
            };

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(dependent);
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var mockNotifRepo = new Mock<IGenericRepository<SystemNotification>>();
            _mockUnitOfWork.Setup(u => u.Repository<SystemNotification>()).Returns(mockNotifRepo.Object);

            var result = await _dependentService.DeleteDependentAsync(taxpayerId, depId, new DeleteDependentRequest { Reason = "   " });

            result.Should().NotBeNull();
            dependent.Note.Should().Be("Ghi chu cu");
            dependent.IsDeleted.Should().BeTrue();
            dependent.Status.Should().Be(DependentStatus.INACTIVE);
        }

        [Fact]
        public async Task DeleteDependentAsync_PendingOrRejectedStatus_DeactivatesSuccessfully()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();
            var dependent = new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                FullName = "Nguyen Van Con",
                Relationship = "CHILD",
                CurrentGroup = "CHILD_UNDER_18",
                IsDeleted = false,
                Status = DependentStatus.PENDING_DOCUMENTS
            };

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(dependent);
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var mockNotifRepo = new Mock<IGenericRepository<SystemNotification>>();
            _mockUnitOfWork.Setup(u => u.Repository<SystemNotification>()).Returns(mockNotifRepo.Object);

            var result = await _dependentService.DeleteDependentAsync(taxpayerId, depId, new DeleteDependentRequest { Reason = "Huy ho so" });

            result.Should().NotBeNull();
            dependent.Status.Should().Be(DependentStatus.INACTIVE);
            dependent.IsDeleted.Should().BeTrue();
            dependent.Note.Should().Be("[Vô hiệu hóa] Huy ho so");
        }

        [Fact]
        public async Task DeleteDependentAsync_BoundaryReasonLength_Success()
        {
            var taxpayerId = Guid.NewGuid();
            var depId = Guid.NewGuid();
            var dependent = new Dependent
            {
                Id = depId,
                TaxpayerId = taxpayerId,
                FullName = "Nguyen Van Con",
                Relationship = "CHILD",
                CurrentGroup = "CHILD_UNDER_18",
                IsDeleted = false,
                Status = DependentStatus.ACTIVE
            };

            var mockRepo = new Mock<IGenericRepository<Dependent>>();
            mockRepo.Setup(r => r.GetByIdAsync(depId)).ReturnsAsync(dependent);
            _mockUnitOfWork.Setup(u => u.Repository<Dependent>()).Returns(mockRepo.Object);

            var mockNotifRepo = new Mock<IGenericRepository<SystemNotification>>();
            _mockUnitOfWork.Setup(u => u.Repository<SystemNotification>()).Returns(mockNotifRepo.Object);

            // Boundary min: 1 char
            var res1 = await _dependentService.DeleteDependentAsync(taxpayerId, depId, new DeleteDependentRequest { Reason = "A" });
            dependent.Note.Should().Be("[Vô hiệu hóa] A");

            // Boundary max: 500 chars
            dependent.IsDeleted = false; // reset for test
            var longReason = new string('X', 500);
            var res2 = await _dependentService.DeleteDependentAsync(taxpayerId, depId, new DeleteDependentRequest { Reason = longReason });
            dependent.Note.Should().Be($"[Vô hiệu hóa] {longReason}");
        }

        #endregion
    }
}
