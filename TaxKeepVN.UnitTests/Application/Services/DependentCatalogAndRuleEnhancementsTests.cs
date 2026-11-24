using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using TaxKeepVN.Application.Constants;
using TaxKeepVN.Application.DTOs.Catalog;
using TaxKeepVN.Application.DTOs.TaxAI;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Implementations;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;
using Xunit;

namespace TaxKeepVN.UnitTests.Application.Services
{
    public class DependentCatalogAndRuleEnhancementsTests
    {
        private readonly Mock<IUnitOfWork> _mockUow;
        private readonly Mock<IGenericRepository<DependentDocumentRule>> _mockRuleRepo;
        private readonly Mock<IGenericRepository<DependentGroupCatalog>> _mockGroupRepo;
        private readonly Mock<IGenericRepository<DependentRelationshipCatalog>> _mockRelRepo;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _config;
        private readonly DependentRuleService _ruleService;
        private readonly DependentCatalogService _catalogService;

        public DependentCatalogAndRuleEnhancementsTests()
        {
            _mockUow = new Mock<IUnitOfWork>();
            _mockRuleRepo = new Mock<IGenericRepository<DependentDocumentRule>>();
            _mockGroupRepo = new Mock<IGenericRepository<DependentGroupCatalog>>();
            _mockRelRepo = new Mock<IGenericRepository<DependentRelationshipCatalog>>();

            _mockUow.Setup(u => u.Repository<DependentDocumentRule>()).Returns(_mockRuleRepo.Object);
            _mockUow.Setup(u => u.Repository<DependentGroupCatalog>()).Returns(_mockGroupRepo.Object);
            _mockUow.Setup(u => u.Repository<DependentRelationshipCatalog>()).Returns(_mockRelRepo.Object);

            _cache = new MemoryCache(new MemoryCacheOptions());
            _config = new ConfigurationBuilder().AddInMemoryCollection().Build();

            _ruleService = new DependentRuleService(_mockUow.Object, _cache, _config);
            _catalogService = new DependentCatalogService(_mockUow.Object, _cache);
        }

        #region 1. ComputeMissingDocumentsAsync Tests

        [Fact]
        public async Task ComputeMissingDocumentsAsync_ChildUnder18_UploadedCitizenId_IsComplete()
        {
            // Rules: BIRTH_CERTIFICATE and CITIZEN_ID in alternative_group IDENTITY_PROOF
            var rules = new List<DependentDocumentRule>
            {
                new() { TargetGroup = "CHILD_UNDER_18", DocType = "BIRTH_CERTIFICATE", AlternativeGroup = "IDENTITY_PROOF", IsMandatory = true, IsActive = true, CreatedAt = DateTime.UtcNow.AddMinutes(-2) },
                new() { TargetGroup = "CHILD_UNDER_18", DocType = "CITIZEN_ID", AlternativeGroup = "IDENTITY_PROOF", IsMandatory = true, IsActive = true, CreatedAt = DateTime.UtcNow.AddMinutes(-1) }
            };
            _mockRuleRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocumentRule, bool>>>()))
                .ReturnsAsync(rules);

            var (isComplete, missing) = await _ruleService.ComputeMissingDocumentsAsync("CHILD_UNDER_18", new[] { "CITIZEN_ID" });

            isComplete.Should().BeTrue();
            missing.Should().BeEmpty();
        }

        [Fact]
        public async Task ComputeMissingDocumentsAsync_ChildUnder18_UploadedBirthCert_IsComplete()
        {
            var rules = new List<DependentDocumentRule>
            {
                new() { TargetGroup = "CHILD_UNDER_18", DocType = "BIRTH_CERTIFICATE", AlternativeGroup = "IDENTITY_PROOF", IsMandatory = true, IsActive = true, CreatedAt = DateTime.UtcNow.AddMinutes(-2) },
                new() { TargetGroup = "CHILD_UNDER_18", DocType = "CITIZEN_ID", AlternativeGroup = "IDENTITY_PROOF", IsMandatory = true, IsActive = true, CreatedAt = DateTime.UtcNow.AddMinutes(-1) }
            };
            _mockRuleRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocumentRule, bool>>>()))
                .ReturnsAsync(rules);

            var (isComplete, missing) = await _ruleService.ComputeMissingDocumentsAsync("CHILD_UNDER_18", new[] { "BIRTH_CERTIFICATE" });

            isComplete.Should().BeTrue();
            missing.Should().BeEmpty();
        }

        [Fact]
        public async Task ComputeMissingDocumentsAsync_ChildUnder18_UploadedNone_ReturnsFirstRuleInAltGroup()
        {
            var rules = new List<DependentDocumentRule>
            {
                new() { TargetGroup = "CHILD_UNDER_18", DocType = "BIRTH_CERTIFICATE", AlternativeGroup = "IDENTITY_PROOF", IsMandatory = true, IsActive = true, CreatedAt = DateTime.UtcNow.AddMinutes(-2) },
                new() { TargetGroup = "CHILD_UNDER_18", DocType = "CITIZEN_ID", AlternativeGroup = "IDENTITY_PROOF", IsMandatory = true, IsActive = true, CreatedAt = DateTime.UtcNow.AddMinutes(-1) }
            };
            _mockRuleRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocumentRule, bool>>>()))
                .ReturnsAsync(rules);

            var (isComplete, missing) = await _ruleService.ComputeMissingDocumentsAsync("CHILD_UNDER_18", Array.Empty<string>());

            isComplete.Should().BeFalse();
            missing.Should().ContainSingle().Which.Should().Be("BIRTH_CERTIFICATE");
        }

        [Fact]
        public async Task ComputeMissingDocumentsAsync_ParentDisabled_UploadedAll_IsComplete()
        {
            var rules = new List<DependentDocumentRule>
            {
                new() { TargetGroup = "PARENT_DISABLED", DocType = "CITIZEN_ID", IsMandatory = true, IsActive = true },
                new() { TargetGroup = "PARENT_DISABLED", DocType = "RELATIONSHIP_CERTIFICATE", AlternativeGroup = "RELATION_PROOF", IsMandatory = true, IsActive = true, CreatedAt = DateTime.UtcNow.AddMinutes(-2) },
                new() { TargetGroup = "PARENT_DISABLED", DocType = "BIRTH_CERTIFICATE", AlternativeGroup = "RELATION_PROOF", IsMandatory = true, IsActive = true, CreatedAt = DateTime.UtcNow.AddMinutes(-1) },
                new() { TargetGroup = "PARENT_DISABLED", DocType = "DISABILITY_CERTIFICATE", IsMandatory = true, IsActive = true }
            };
            _mockRuleRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocumentRule, bool>>>()))
                .ReturnsAsync(rules);

            // Uploaded CT07 (normalized to RELATIONSHIP_CERTIFICATE) + CCCD (CITIZEN_ID) + DISABILITY_CERTIFICATE
            var (isComplete, missing) = await _ruleService.ComputeMissingDocumentsAsync("PARENT_DISABLED", new[] { "CT07", "CCCD", "DISABILITY_CERTIFICATE" });

            isComplete.Should().BeTrue();
            missing.Should().BeEmpty();
        }

        [Fact]
        public async Task ComputeMissingDocumentsAsync_ParentDisabled_OnlyUploadedCitizenId_ReturnsRelationAndDisability()
        {
            var rules = new List<DependentDocumentRule>
            {
                new() { TargetGroup = "PARENT_DISABLED", DocType = "CITIZEN_ID", IsMandatory = true, IsActive = true },
                new() { TargetGroup = "PARENT_DISABLED", DocType = "RELATIONSHIP_CERTIFICATE", AlternativeGroup = "RELATION_PROOF", IsMandatory = true, IsActive = true, CreatedAt = DateTime.UtcNow.AddMinutes(-2) },
                new() { TargetGroup = "PARENT_DISABLED", DocType = "BIRTH_CERTIFICATE", AlternativeGroup = "RELATION_PROOF", IsMandatory = true, IsActive = true, CreatedAt = DateTime.UtcNow.AddMinutes(-1) },
                new() { TargetGroup = "PARENT_DISABLED", DocType = "DISABILITY_CERTIFICATE", IsMandatory = true, IsActive = true }
            };
            _mockRuleRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocumentRule, bool>>>()))
                .ReturnsAsync(rules);

            var (isComplete, missing) = await _ruleService.ComputeMissingDocumentsAsync("PARENT_DISABLED", new[] { "CITIZEN_ID" });

            isComplete.Should().BeFalse();
            missing.Should().Contain("RELATIONSHIP_CERTIFICATE");
            missing.Should().Contain("DISABILITY_CERTIFICATE");
        }

        [Fact]
        public async Task ComputeMissingDocumentsAsync_NoActiveRules_ReturnsRulesNotConfigured()
        {
            _mockRuleRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocumentRule, bool>>>()))
                .ReturnsAsync(new List<DependentDocumentRule>());

            var (isComplete, missing) = await _ruleService.ComputeMissingDocumentsAsync("UNKNOWN_GROUP", new[] { "CITIZEN_ID" });

            isComplete.Should().BeFalse();
            missing.Should().ContainSingle().Which.Should().Be(ErrorCodes.Dependent.RulesNotConfigured);
        }

        #endregion

        #region 2. SyncFromAiAsync Tests

        [Fact]
        public async Task SyncFromAiAsync_NewAiGroup_CreatesInactiveGroupWithSourceAI()
        {
            // DB currently has no rules and seeded groups
            _mockRuleRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<DependentDocumentRule>());
            _mockGroupRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<DependentGroupCatalog>());
            _mockRelRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<DependentRelationshipCatalog>
            {
                new() { Code = "OTHER_DEPENDENT", Name = "Cá nhân khác", IsActive = true }
            });

            DependentGroupCatalog? capturedNewGroup = null;
            _mockGroupRepo.Setup(r => r.AddAsync(It.IsAny<DependentGroupCatalog>()))
                .Callback<DependentGroupCatalog>(g => capturedNewGroup = g)
                .Returns(Task.CompletedTask);

            var aiRules = new List<DependentRuleFromAiDto>
            {
                new()
                {
                    DependentType = "FOSTER_CHILD_SPECIAL",
                    Name = "Trẻ em nhận nuôi đặc biệt",
                    Relationship = "OTHER_DEPENDENT",
                    IsDisabled = true,
                    IsStudying = false,
                    RequiredDocuments = new List<RequiredDocumentFromAiDto>
                    {
                        new() { DocType = "ADOPTION_CERTIFICATE", IsMandatory = true, Name = "Giấy chứng nhận con nuôi" }
                    }
                }
            };

            var (added, updated, deactivated) = await _ruleService.SyncFromAiAsync(aiRules);

            added.Should().Be(1);
            updated.Should().Be(0);
            deactivated.Should().Be(0);

            capturedNewGroup.Should().NotBeNull();
            capturedNewGroup!.Code.Should().Be("FOSTER_CHILD_SPECIAL");
            capturedNewGroup.IsActive.Should().BeFalse();
            capturedNewGroup.Source.Should().Be("AI");
            capturedNewGroup.RelationshipCode.Should().Be("OTHER_DEPENDENT");
            capturedNewGroup.RequireDisabled.Should().BeTrue();
        }

        [Fact]
        public async Task SyncFromAiAsync_DeactivatesStaleAiRules_PreservesAdminRules()
        {
            var existingRules = new List<DependentDocumentRule>
            {
                // AI rule in CHILD_UNDER_18 that is NOT in incoming list -> should be deactivated
                new() { RuleId = Guid.NewGuid(), TargetGroup = "CHILD_UNDER_18", DocType = "OLD_AI_DOC", Source = "AI", IsActive = true },
                // Admin rule in CHILD_UNDER_18 that is NOT in incoming list -> should stay active
                new() { RuleId = Guid.NewGuid(), TargetGroup = "CHILD_UNDER_18", DocType = "SPECIAL_ADMIN_DOC", Source = "ADMIN", IsActive = true },
                // Existing AI rule in incoming list -> updated
                new() { RuleId = Guid.NewGuid(), TargetGroup = "CHILD_UNDER_18", DocType = "BIRTH_CERTIFICATE", Source = "AI", IsActive = true, IsMandatory = false }
            };

            _mockRuleRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(existingRules);
            _mockGroupRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<DependentGroupCatalog>
            {
                new() { Code = "CHILD_UNDER_18", IsActive = true }
            });

            var aiRules = new List<DependentRuleFromAiDto>
            {
                new()
                {
                    DependentType = "CHILD_UNDER_18",
                    RequiredDocuments = new List<RequiredDocumentFromAiDto>
                    {
                        new() { DocType = "BIRTH_CERTIFICATE", IsMandatory = true, Name = "Giấy khai sinh mới", AlternativeGroup = "IDENTITY_PROOF" }
                    }
                }
            };

            var (added, updated, deactivated) = await _ruleService.SyncFromAiAsync(aiRules);

            added.Should().Be(0);
            updated.Should().Be(1);
            deactivated.Should().Be(1);

            // Stale AI doc should be deactivated
            existingRules.First(r => r.DocType == "OLD_AI_DOC").IsActive.Should().BeFalse();
            // Admin doc should remain active
            existingRules.First(r => r.DocType == "SPECIAL_ADMIN_DOC").IsActive.Should().BeTrue();
            // Updated doc should have new name and alt group
            var birthCert = existingRules.First(r => r.DocType == "BIRTH_CERTIFICATE");
            birthCert.IsMandatory.Should().BeTrue();
            birthCert.DocName.Should().Be("Giấy khai sinh mới");
            birthCert.AlternativeGroup.Should().Be("IDENTITY_PROOF");
        }

        #endregion

        #region 3. GetActiveGroupsAsync Relationship Matching

        [Fact]
        public async Task GetActiveGroupsAsync_OtherDependentFilter_MatchesOtherHelpless()
        {
            var groups = new List<DependentGroupCatalog>
            {
                new() { Code = "CHILD_UNDER_18", RelationshipCode = "CHILD", IsActive = true, DisplayOrder = 1 },
                new() { Code = "OTHER_HELPLESS", RelationshipCode = "OTHER_DEPENDENT", IsActive = true, DisplayOrder = 8 }
            };
            var activeRules = new List<DependentDocumentRule>
            {
                new() { TargetGroup = "CHILD_UNDER_18", DocType = "BIRTH_CERTIFICATE", IsActive = true },
                new() { TargetGroup = "OTHER_HELPLESS", DocType = "CITIZEN_ID", IsActive = true }
            };

            _mockGroupRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentGroupCatalog, bool>>>()))
                .ReturnsAsync(groups);
            _mockRuleRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocumentRule, bool>>>()))
                .ReturnsAsync(activeRules);

            var result = await _ruleService.GetActiveGroupsAsync("OTHER_DEPENDENT");

            result.Should().ContainSingle().Which.Should().Be("OTHER_HELPLESS");
        }

        #endregion

        #region 4. CatalogService Tests

        [Fact]
        public async Task GetActiveCatalogAsync_ReturnsActiveRelationshipsAndGroups()
        {
            var rels = new List<DependentRelationshipCatalog>
            {
                new() { Code = "CHILD", Name = "Con", DisplayOrder = 1, IsActive = true },
                new() { Code = "SPOUSE", Name = "Vợ/chồng", DisplayOrder = 2, IsActive = true }
            };
            var groups = new List<DependentGroupCatalog>
            {
                new() { Code = "CHILD_UNDER_18", Name = "Con dưới 18 tuổi", RelationshipCode = "CHILD", DisplayOrder = 1, IsActive = true, MaxAge = 18, Level = 0 }
            };

            _mockRelRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentRelationshipCatalog, bool>>>()))
                .ReturnsAsync(rels);
            _mockGroupRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentGroupCatalog, bool>>>()))
                .ReturnsAsync(groups);

            var catalog = await _catalogService.GetActiveCatalogAsync();

            catalog.Relationships.Should().HaveCount(2);
            catalog.Groups.Should().HaveCount(1);
            catalog.Groups[0].Code.Should().Be("CHILD_UNDER_18");
            catalog.Groups[0].MaxAge.Should().Be(18);
        }

        [Fact]
        public async Task ActivateAiGroupAsync_ValidRelationship_ActivatesGroup()
        {
            var aiGroup = new DependentGroupCatalog
            {
                Code = "NEW_GROUP",
                Name = "Nhóm mới",
                RelationshipCode = null,
                IsActive = false,
                Source = "AI"
            };

            _mockGroupRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentGroupCatalog, bool>>>()))
                .ReturnsAsync(new List<DependentGroupCatalog> { aiGroup });
            _mockRelRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentRelationshipCatalog, bool>>>()))
                .ReturnsAsync(new List<DependentRelationshipCatalog>
                {
                    new() { Code = "PARENT", Name = "Cha/mẹ", IsActive = true }
                });

            var result = await _catalogService.ActivateAiGroupAsync("NEW_GROUP", new ActivateAiGroupRequest
            {
                RelationshipCode = "PARENT",
                Name = "Cha/mẹ nuôi"
            });

            result.IsActive.Should().BeTrue();
            result.RelationshipCode.Should().Be("PARENT");
            result.Name.Should().Be("Cha/mẹ nuôi");
            _mockUow.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task ActivateAiGroupAsync_InvalidRelationship_ThrowsBadRequest()
        {
            var aiGroup = new DependentGroupCatalog
            {
                Code = "NEW_GROUP",
                IsActive = false
            };

            _mockGroupRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentGroupCatalog, bool>>>()))
                .ReturnsAsync(new List<DependentGroupCatalog> { aiGroup });
            _mockRelRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentRelationshipCatalog, bool>>>()))
                .ReturnsAsync(new List<DependentRelationshipCatalog>()); // No relationship matches

            var act = async () => await _catalogService.ActivateAiGroupAsync("NEW_GROUP", new ActivateAiGroupRequest
            {
                RelationshipCode = "INVALID_REL"
            });

            await act.Should().ThrowAsync<BadRequestException>()
                .Where(e => e.ErrorCode == "INVALID_RELATIONSHIP");
        }

        #endregion
    }
}
