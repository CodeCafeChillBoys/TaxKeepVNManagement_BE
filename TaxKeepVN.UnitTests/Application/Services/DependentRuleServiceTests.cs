using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Moq;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Rules;
using TaxKeepVN.Application.DTOs.TaxAI;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Helpers;
using TaxKeepVN.Application.Service.Implementations;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;
using Xunit;

namespace TaxKeepVN.UnitTests.Application.Services
{
    public class DependentRuleServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IGenericRepository<DependentDocumentRule>> _mockRepo;
        private readonly Mock<IGenericRepository<DependentGroupCatalog>> _mockGroupRepo;
        private readonly Mock<IGenericRepository<DependentRelationshipCatalog>> _mockRelRepo;
        private readonly IMemoryCache _cache;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly DependentRuleService _service;

        public DependentRuleServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockRepo = new Mock<IGenericRepository<DependentDocumentRule>>();
            _mockGroupRepo = new Mock<IGenericRepository<DependentGroupCatalog>>();
            _mockRelRepo = new Mock<IGenericRepository<DependentRelationshipCatalog>>();
            _cache = new MemoryCache(new MemoryCacheOptions());
            _mockConfiguration = new Mock<IConfiguration>();

            var defaultRels = new List<DependentRelationshipCatalog>
            {
                new() { Code = "CHILD", Name = "Con cái", DisplayOrder = 1, IsActive = true },
                new() { Code = "SPOUSE", Name = "Vợ/Chồng", DisplayOrder = 2, IsActive = true },
                new() { Code = "PARENT", Name = "Cha/Mẹ", DisplayOrder = 3, IsActive = true },
                new() { Code = "OTHER_DEPENDENT", Name = "Người phụ thuộc khác", DisplayOrder = 4, IsActive = true }
            };
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

            _mockRelRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(defaultRels);
            _mockRelRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentRelationshipCatalog, bool>>>()))
                .ReturnsAsync(defaultRels);
            _mockUnitOfWork.Setup(u => u.Repository<DependentRelationshipCatalog>()).Returns(_mockRelRepo.Object);

            _mockGroupRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(defaultGroups);
            _mockGroupRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentGroupCatalog, bool>>>()))
                .ReturnsAsync(defaultGroups);
            _mockUnitOfWork.Setup(u => u.Repository<DependentGroupCatalog>()).Returns(_mockGroupRepo.Object);

            _mockUnitOfWork.Setup(u => u.Repository<DependentDocumentRule>()).Returns(_mockRepo.Object);

            _service = new DependentRuleService(_mockUnitOfWork.Object, _cache, _mockConfiguration.Object);
        }

        #region GetAllAsync

        [Fact]
        public async Task UTCID01_GetAllAsync_ReturnsPaged()
        {
            var data = new List<DependentDocumentRule>
            {
                new DependentDocumentRule { RuleId = Guid.NewGuid(), TargetGroup = "CHILD_UNDER_18", DocType = "BIRTH_CERTIFICATE", IsActive = true },
                new DependentDocumentRule { RuleId = Guid.NewGuid(), TargetGroup = "CHILD_OVER_18_STUDENT", DocType = "STUDENT_CARD", IsActive = true }
            };

            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(data);

            var query = new DependentRuleQueryParameters { Page = 1, Size = 10 };
            var result = await _service.GetAllAsync(query);

            result.Should().NotBeNull();
            result.Items.Should().HaveCount(2);
        }

        #endregion

        #region GetByIdAsync

        [Fact]
        public async Task UTCID02_GetByIdAsync_NotFound_ThrowsNotFoundException()
        {
            _mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((DependentDocumentRule?)null);

            await Assert.ThrowsAsync<NotFoundException>(() => _service.GetByIdAsync(Guid.NewGuid()));
        }

        [Fact]
        public async Task UTCID03_GetByIdAsync_Found_ReturnsDto()
        {
            var rule = new DependentDocumentRule
            {
                RuleId = Guid.NewGuid(),
                TargetGroup = "CHILD_UNDER_18",
                DocType = "BIRTH_CERTIFICATE"
            };

            _mockRepo.Setup(r => r.GetByIdAsync(rule.RuleId)).ReturnsAsync(rule);

            var result = await _service.GetByIdAsync(rule.RuleId);

            result.Should().NotBeNull();
            result.DocType.Should().Be("BIRTH_CERTIFICATE");
        }

        #endregion

        #region CreateAsync

        [Fact]
        public async Task UTCID04_CreateAsync_DuplicateRule_ThrowsConflictException()
        {
            var existing = new DependentDocumentRule { TargetGroup = "CHILD_UNDER_18", DocType = "BIRTH_CERT" };
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocumentRule, bool>>>()))
                     .ReturnsAsync(new List<DependentDocumentRule> { existing });

            var dto = new CreateDependentRuleDto
            {
                TargetGroup = "CHILD_UNDER_18",
                DocType = "BIRTH_CERT"
            };

            var ex = await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(dto));
            ex.ErrorCode.Should().Be("DUPLICATE_RULE");
        }

        [Fact]
        public async Task UTCID05_CreateAsync_Valid_CreatesSuccessfully()
        {
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocumentRule, bool>>>()))
                     .ReturnsAsync(new List<DependentDocumentRule>());

            var dto = new CreateDependentRuleDto
            {
                TargetGroup = "CHILD_UNDER_18",
                DocType = "ID_CARD",
                IsMandatory = true,
                IsActive = true
            };

            var result = await _service.CreateAsync(dto);

            result.Should().NotBeNull();
            result.DocType.Should().Be("ID_CARD");
            _mockRepo.Verify(r => r.AddAsync(It.IsAny<DependentDocumentRule>()), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_AllowsSameDocumentTypeInDifferentAlternativeGroups()
        {
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocumentRule, bool>>>()))
                .ReturnsAsync(new List<DependentDocumentRule>
                {
                    new()
                    {
                        TargetGroup = "CHILD_UNDER_18",
                        DocType = "BIRTH_CERTIFICATE",
                        AlternativeGroup = "RELATION_PROOF"
                    }
                });

            var result = await _service.CreateAsync(new CreateDependentRuleDto
            {
                TargetGroup = "CHILD_UNDER_18",
                DocType = "BIRTH_CERTIFICATE",
                AlternativeGroup = "identity_proof"
            });

            result.AlternativeGroup.Should().Be("IDENTITY_PROOF");
            _mockRepo.Verify(r => r.AddAsync(It.Is<DependentDocumentRule>(
                rule => rule.AlternativeGroup == "IDENTITY_PROOF")), Times.Once);
        }

        [Fact]
        public async Task UTCID05b_CreateAsync_WithDocName_SavesDocName()
        {
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocumentRule, bool>>>()))
                     .ReturnsAsync(new List<DependentDocumentRule>());

            var dto = new CreateDependentRuleDto
            {
                TargetGroup = "PARENT_RETIRED",
                DocType = "PENSION_STATEMENT",
                DocName = "Sổ hưu trí",
                IsMandatory = true,
                IsActive = true
            };

            var result = await _service.CreateAsync(dto);

            result.Should().NotBeNull();
            result.DocType.Should().Be("PENSION_STATEMENT");
            result.DocName.Should().Be("Sổ hưu trí");
            _mockRepo.Verify(r => r.AddAsync(It.Is<DependentDocumentRule>(rule => rule.DocName == "Sổ hưu trí")), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion

        #region DeleteAsync

        [Fact]
        public async Task UTCID06_DeleteAsync_NotFound_ThrowsNotFoundException()
        {
            _mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((DependentDocumentRule?)null);

            await Assert.ThrowsAsync<NotFoundException>(() => _service.DeleteAsync(Guid.NewGuid()));
        }

        [Fact]
        public async Task UTCID07_DeleteAsync_Found_RemovesSuccessfully()
        {
            var rule = new DependentDocumentRule { RuleId = Guid.NewGuid(), TargetGroup = "CHILD_UNDER_18" };
            _mockRepo.Setup(r => r.GetByIdAsync(rule.RuleId)).ReturnsAsync(rule);

            await _service.DeleteAsync(rule.RuleId);

            _mockRepo.Verify(r => r.Remove(rule), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion

        #region UpdateAsync

        [Fact]
        public async Task UTCID08_UpdateAsync_NotFound_ThrowsNotFoundException()
        {
            _mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((DependentDocumentRule?)null);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.UpdateAsync(Guid.NewGuid(), new UpdateDependentRuleDto()));
        }

        [Fact]
        public async Task UTCID09_UpdateAsync_Valid_UpdatesSuccessfully()
        {
            var rule = new DependentDocumentRule
            {
                RuleId = Guid.NewGuid(),
                TargetGroup = "CHILD_UNDER_18",
                DocType = "BIRTH_CERTIFICATE",
                IsMandatory = false
            };
            _mockRepo.Setup(r => r.GetByIdAsync(rule.RuleId)).ReturnsAsync(rule);

            var dto = new UpdateDependentRuleDto
            {
                IsMandatory = true,
                Description = "Bắt buộc có giấy khai sinh",
                IsActive = true
            };

            var result = await _service.UpdateAsync(rule.RuleId, dto);

            result.Should().NotBeNull();
            result.IsMandatory.Should().BeTrue();
            _mockRepo.Verify(r => r.Update(rule), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_RejectsAnExistingAlternativeGroupVariant()
        {
            var rule = new DependentDocumentRule
            {
                RuleId = Guid.NewGuid(),
                TargetGroup = "CHILD_UNDER_18",
                DocType = "BIRTH_CERTIFICATE",
                AlternativeGroup = "RELATION_PROOF"
            };
            var conflictingRule = new DependentDocumentRule
            {
                RuleId = Guid.NewGuid(),
                TargetGroup = "CHILD_UNDER_18",
                DocType = "BIRTH_CERTIFICATE",
                AlternativeGroup = "IDENTITY_PROOF"
            };
            _mockRepo.Setup(r => r.GetByIdAsync(rule.RuleId)).ReturnsAsync(rule);
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocumentRule, bool>>>()))
                .ReturnsAsync(new List<DependentDocumentRule> { conflictingRule });

            var exception = await Assert.ThrowsAsync<ConflictException>(() =>
                _service.UpdateAsync(rule.RuleId, new UpdateDependentRuleDto
                {
                    AlternativeGroup = "identity_proof"
                }));

            exception.ErrorCode.Should().Be("DUPLICATE_RULE");
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
        }

        [Fact]
        public async Task UTCID09b_UpdateAsync_WithDocName_UpdatesDocName()
        {
            var rule = new DependentDocumentRule
            {
                RuleId = Guid.NewGuid(),
                TargetGroup = "PARENT_RETIRED",
                DocType = "PENSION_STATEMENT",
                DocName = "Sổ hưu"
            };
            _mockRepo.Setup(r => r.GetByIdAsync(rule.RuleId)).ReturnsAsync(rule);

            var dto = new UpdateDependentRuleDto
            {
                DocName = "Sổ hưu trí mới",
                IsMandatory = true
            };

            var result = await _service.UpdateAsync(rule.RuleId, dto);

            result.Should().NotBeNull();
            result.DocName.Should().Be("Sổ hưu trí mới");
            rule.DocName.Should().Be("Sổ hưu trí mới");
            _mockRepo.Verify(r => r.Update(rule), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion

        #region GetActiveGroupsAsync

        [Fact]
        public async Task UTCID10_GetActiveGroupsAsync_ReturnsDistinctGroups()
        {
            var testGroups = new List<DependentGroupCatalog>
            {
                new DependentGroupCatalog { Code = "CHILD_UNDER_18", IsActive = true },
                new DependentGroupCatalog { Code = "CHILD_OVER_18", IsActive = true }
            };
            _mockGroupRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentGroupCatalog, bool>>>()))
                .ReturnsAsync(testGroups);

            var rules = new List<DependentDocumentRule>
            {
                new DependentDocumentRule { TargetGroup = "CHILD_UNDER_18", IsActive = true },
                new DependentDocumentRule { TargetGroup = "CHILD_OVER_18", IsActive = true }
            };
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocumentRule, bool>>>()))
                     .ReturnsAsync(rules);

            var result = await _service.GetActiveGroupsAsync();

            result.Should().Contain("CHILD_UNDER_18");
            result.Should().Contain("CHILD_OVER_18");
        }

        #endregion

        #region SyncFromAiAsync

        [Fact]
        public async Task UTCID11_SyncFromAiAsync_NewAndExisting_AddsAndUpdatesCorrectly()
        {
            var existingRule = new DependentDocumentRule
            {
                TargetGroup = "CHILD_UNDER_18",
                DocType = "BIRTH_CERTIFICATE",
                IsMandatory = false
            };
            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<DependentDocumentRule> { existingRule });

            var aiRules = new List<DependentRuleFromAiDto>
            {
                new DependentRuleFromAiDto
                {
                    DependentType = "CHILD_UNDER_18",
                    RequiredDocuments = new List<RequiredDocumentFromAiDto>
                    {
                        new RequiredDocumentFromAiDto { DocType = "BIRTH_CERTIFICATE", IsMandatory = true },
                        new RequiredDocumentFromAiDto { DocType = "CITIZEN_ID", IsMandatory = true }
                    }
                }
            };

            var (added, updated, deactivated) = await _service.SyncFromAiAsync(aiRules);

            added.Should().Be(1);
            updated.Should().Be(1);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task SyncFromAiAsync_PreservesSameDocumentTypeInDifferentAlternativeGroups()
        {
            var addedRules = new List<DependentDocumentRule>();
            _mockRepo.Setup(r => r.GetAllAsync())
                .Returns(() => Task.FromResult<IEnumerable<DependentDocumentRule>>(addedRules.ToList()));
            _mockRepo.Setup(r => r.AddAsync(It.IsAny<DependentDocumentRule>()))
                .Callback<DependentDocumentRule>(rule => addedRules.Add(rule))
                .Returns(Task.CompletedTask);

            var aiRules = new[]
            {
                new DependentRuleFromAiDto
                {
                    DependentType = "CHILD_UNDER_18",
                    RequiredDocuments = new List<RequiredDocumentFromAiDto>
                    {
                        new()
                        {
                            DocType = "BIRTH_CERTIFICATE",
                            Name = "Giấy khai sinh của người nộp thuế",
                            AlternativeGroup = "RELATION_PROOF"
                        },
                        new()
                        {
                            DocType = "BIRTH_CERTIFICATE",
                            Name = "Giấy khai sinh hoặc trích lục khai sinh",
                            AlternativeGroup = "IDENTITY_PROOF"
                        }
                    }
                }
            };

            var (added, updated, _) = await _service.SyncFromAiAsync(aiRules);

            added.Should().Be(2);
            updated.Should().Be(0);
            addedRules.Should().HaveCount(2);
            addedRules.Should().OnlyContain(rule => rule.DocType == "BIRTH_CERTIFICATE");
            addedRules.Select(rule => rule.AlternativeGroup).Should().BeEquivalentTo(
                new[] { "RELATION_PROOF", "IDENTITY_PROOF" });
            addedRules.Select(rule => rule.DocName).Should().BeEquivalentTo(
                new[] { "Giấy khai sinh của người nộp thuế", "Giấy khai sinh hoặc trích lục khai sinh" });

            var (secondAdded, secondUpdated, _) = await _service.SyncFromAiAsync(aiRules);

            secondAdded.Should().Be(0);
            secondUpdated.Should().Be(2);
            addedRules.Should().HaveCount(2);
        }

        [Fact]
        public async Task SyncFromAiAsync_KeepsResidenceCt07SeparateFromRelationshipCertificate()
        {
            var existingRelationshipRule = new DependentDocumentRule
            {
                TargetGroup = "CHILD_UNDER_18",
                DocType = "RELATIONSHIP_CERTIFICATE",
                DocName = "Giấy xác nhận thông tin về cư trú (Mẫu CT07)",
                Source = "AI",
                IsMandatory = true,
                IsActive = true
            };
            var rulesInDb = new List<DependentDocumentRule> { existingRelationshipRule };
            _mockRepo.Setup(r => r.GetAllAsync())
                .Returns(() => Task.FromResult<IEnumerable<DependentDocumentRule>>(rulesInDb.ToList()));
            _mockRepo.Setup(r => r.AddAsync(It.IsAny<DependentDocumentRule>()))
                .Callback<DependentDocumentRule>(rule => rulesInDb.Add(rule))
                .Returns(Task.CompletedTask);

            var aiRules = new[]
            {
                new DependentRuleFromAiDto
                {
                    DependentType = "CHILD_UNDER_18",
                    RequiredDocuments = new List<RequiredDocumentFromAiDto>
                    {
                        new()
                        {
                            DocType = "RELATIONSHIP_CERTIFICATE",
                            Name = "Giấy tờ chứng minh quan hệ nhân thân khác",
                            AlternativeGroup = "RELATION_PROOF"
                        },
                        new()
                        {
                            DocType = "RESIDENCE_CT07",
                            Name = "Giấy xác nhận thông tin về cư trú (Mẫu CT07)",
                            AlternativeGroup = "RELATION_PROOF"
                        }
                    }
                }
            };

            var (added, updated, _) = await _service.SyncFromAiAsync(aiRules);

            added.Should().Be(1);
            updated.Should().Be(1);
            rulesInDb.Should().HaveCount(2);
            rulesInDb.Should().ContainSingle(rule =>
                rule.DocType == "RELATIONSHIP_CERTIFICATE" &&
                rule.DocName == "Giấy tờ chứng minh quan hệ nhân thân khác");
            rulesInDb.Should().ContainSingle(rule =>
                rule.DocType == "RESIDENCE_CT07" &&
                rule.DocName == "Giấy xác nhận thông tin về cư trú (Mẫu CT07)");

            var (secondAdded, secondUpdated, _) = await _service.SyncFromAiAsync(aiRules);

            secondAdded.Should().Be(0);
            secondUpdated.Should().Be(2);
            rulesInDb.Should().HaveCount(2);
        }

        [Fact]
        public async Task ComputeMissingDocumentsAsync_OneDocumentCanSatisfyTwoAlternativeGroups()
        {
            var rules = new List<DependentDocumentRule>
            {
                new()
                {
                    TargetGroup = "CHILD_UNDER_18",
                    DocType = "BIRTH_CERTIFICATE",
                    AlternativeGroup = "RELATION_PROOF",
                    IsMandatory = true,
                    IsActive = true
                },
                new()
                {
                    TargetGroup = "CHILD_UNDER_18",
                    DocType = "BIRTH_CERTIFICATE",
                    AlternativeGroup = "IDENTITY_PROOF",
                    IsMandatory = true,
                    IsActive = true
                }
            };
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocumentRule, bool>>>()))
                .ReturnsAsync(rules);

            var (isComplete, missingDocuments) = await _service.ComputeMissingDocumentsAsync(
                "CHILD_UNDER_18",
                new[] { "BIRTH_CERTIFICATE" });

            isComplete.Should().BeTrue();
            missingDocuments.Should().BeEmpty();

            var (isIncomplete, missingWithoutUpload) = await _service.ComputeMissingDocumentsAsync(
                "CHILD_UNDER_18",
                Array.Empty<string>());

            isIncomplete.Should().BeFalse();
            missingWithoutUpload.Should().ContainSingle().Which.Should().Be("BIRTH_CERTIFICATE");
        }

        [Theory]
        [InlineData("targetgroup")]
        [InlineData("-targetgroup")]
        [InlineData("doctype")]
        [InlineData("-doctype")]
        [InlineData("ismandatory")]
        [InlineData("-ismandatory")]
        [InlineData("isactive")]
        [InlineData("-isactive")]
        [InlineData("createdat")]
        [InlineData("-createdat")]
        [InlineData("updatedat")]
        [InlineData("-updatedat")]
        [InlineData("unknown")]
        [InlineData("-unknown")]
        [InlineData(null)]
        public async Task UTCID12_GetAllAsync_SortVariations_ExecutesSuccessfully(string? sort)
        {
            var data = new List<DependentDocumentRule>
            {
                new DependentDocumentRule { RuleId = Guid.NewGuid(), TargetGroup = "CHILD_UNDER_18", DocType = "BIRTH_CERTIFICATE", IsMandatory = true, IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-2), UpdatedAt = DateTime.UtcNow },
                new DependentDocumentRule { RuleId = Guid.NewGuid(), TargetGroup = "CHILD_OVER_18", DocType = "STUDENT_CARD", IsMandatory = false, IsActive = false, CreatedAt = DateTime.UtcNow.AddDays(-1), UpdatedAt = DateTime.UtcNow.AddHours(-1) },
                new DependentDocumentRule { RuleId = Guid.NewGuid(), TargetGroup = "PARENT_DISABLED", DocType = "DISABILITY_CERTIFICATE", IsMandatory = true, IsActive = true, Description = "Minh chung khuyet tat" }
            };

            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(data);

            var query = new DependentRuleQueryParameters
            {
                Page = 1,
                Size = 10,
                Sort = sort
            };

            var result = await _service.GetAllAsync(query);
            result.Should().NotBeNull();
            result.Items.Should().HaveCount(3);
        }

        [Fact]
        public async Task UTCID13_GetAllAsync_FilteringAndSearching_FiltersCorrectly()
        {
            var data = new List<DependentDocumentRule>
            {
                new DependentDocumentRule { RuleId = Guid.NewGuid(), TargetGroup = "CHILD_UNDER_18", DocType = "BIRTH_CERTIFICATE", IsActive = true, Description = "Khai sinh" },
                new DependentDocumentRule { RuleId = Guid.NewGuid(), TargetGroup = "CHILD_UNDER_18", DocType = "CITIZEN_ID", IsActive = false, Description = "CCCD" },
                new DependentDocumentRule { RuleId = Guid.NewGuid(), TargetGroup = "SPOUSE_DISABLED", DocType = "MARRIAGE_CERTIFICATE", IsActive = true, Description = "Hon thu" }
            };

            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(data);

            var query1 = new DependentRuleQueryParameters
            {
                TargetGroup = "CHILD_UNDER_18",
                IsActive = true,
                Search = "Khai sinh"
            };
            var result1 = await _service.GetAllAsync(query1);
            result1.Items.Should().HaveCount(1);
            result1.Items[0].DocType.Should().Be("BIRTH_CERTIFICATE");

            var query2 = new DependentRuleQueryParameters
            {
                Search = "SPOUSE"
            };
            var result2 = await _service.GetAllAsync(query2);
            result2.Items.Should().HaveCount(1);
        }

        [Fact]
        public async Task UTCID14_GetActiveGroupsAsync_WithFilterAndCache()
        {
            var rules = new List<DependentDocumentRule>
            {
                new DependentDocumentRule { TargetGroup = "CHILD_UNDER_18", IsActive = true },
                new DependentDocumentRule { TargetGroup = "CHILD_OVER_18_DISABLED", IsActive = true },
                new DependentDocumentRule { TargetGroup = "PARENT_RETIRED", IsActive = true }
            };
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocumentRule, bool>>>()))
                     .ReturnsAsync(rules);

            // First call populates cache
            var result1 = await _service.GetActiveGroupsAsync("CHILD");
            result1.Should().Contain("CHILD_UNDER_18");
            result1.Should().Contain("CHILD_OVER_18_DISABLED");
            result1.Should().NotContain("PARENT_RETIRED");

            // Second call hits cache
            var result2 = await _service.GetActiveGroupsAsync();
            result2.Should().HaveCount(3);
        }

        [Fact]
        public async Task UTCID15_SyncFromAiAsync_ComplexMappingAndDuplicates()
        {
            var configData = new Dictionary<string, string?>
            {
                ["TaxAiSettings:TypeMapping:CHILD:IfDisabled:0"] = "CHILD_OVER_18_DISABLED",
                ["TaxAiSettings:TypeMapping:CHILD:IfStudying:0"] = "CHILD_OVER_18_STUDYING",
                ["TaxAiSettings:TypeMapping:CHILD:Default:0"] = "CHILD_UNDER_18",
                ["TaxAiSettings:TypeMapping:DEFAULT:Default:0"] = "OTHER_HELPLESS"
            };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(configData).Build();

            var service = new DependentRuleService(_mockUnitOfWork.Object, _cache, configuration);

            var existingRule = new DependentDocumentRule
            {
                TargetGroup = "CHILD_OVER_18_DISABLED",
                DocType = "CITIZEN_ID",
                IsMandatory = false,
                Description = "Old desc"
            };
            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<DependentDocumentRule> { existingRule });

            var aiRules = new List<DependentRuleFromAiDto>
            {
                // Empty required docs should be skipped
                new DependentRuleFromAiDto { DependentType = "EMPTY", RequiredDocuments = new List<RequiredDocumentFromAiDto>() },
                // Disabled child
                new DependentRuleFromAiDto
                {
                    DependentType = "CHILD",
                    IsDisabled = true,
                    RequiredDocuments = new List<RequiredDocumentFromAiDto>
                    {
                        new RequiredDocumentFromAiDto { DocType = "CITIZEN_ID", IsMandatory = true, Description = "Updated desc" },
                        // Duplicate with same key to trigger incomingMap duplicate merge
                        new RequiredDocumentFromAiDto { DocType = "CITIZEN_ID", IsMandatory = false, Description = null },
                        new RequiredDocumentFromAiDto { DocType = "DISABILITY_CERTIFICATE", IsMandatory = true, Description = "New cert" }
                    }
                },
                // Studying child
                new DependentRuleFromAiDto
                {
                    DependentType = "CHILD",
                    IsStudying = true,
                    RequiredDocuments = new List<RequiredDocumentFromAiDto>
                    {
                        new RequiredDocumentFromAiDto { DocType = "STUDENT_CARD", IsMandatory = true, Description = "The sinh vien" }
                    }
                },
                // Default child
                new DependentRuleFromAiDto
                {
                    DependentType = "CHILD",
                    RequiredDocuments = new List<RequiredDocumentFromAiDto>
                    {
                        new RequiredDocumentFromAiDto { DocType = "BIRTH_CERTIFICATE", IsMandatory = true, Description = "Giay khai sinh" }
                    }
                },
                // Unknown type falls back to DEFAULT
                new DependentRuleFromAiDto
                {
                    DependentType = "UNKNOWN_TYPE",
                    RequiredDocuments = new List<RequiredDocumentFromAiDto>
                    {
                        new RequiredDocumentFromAiDto { DocType = "OTHER_DOC", IsMandatory = false }
                    }
                }
            };

            var (added, updated, deactivated) = await service.SyncFromAiAsync(aiRules);
            updated.Should().Be(1); // CITIZEN_ID was existing
            added.Should().Be(6);   // 1 DISABILITY + 1 STUDENT_CARD + 3 BIRTH_CERT (all 3 CHILD groups) + 1 OTHER_DOC
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.AtLeastOnce());
        }

        [Fact]
        public async Task UTCID16_SyncFromAiAsync_DetailedEnumsAndIsRetired_MapsCorrectly()
        {
            var configData = new Dictionary<string, string?>
            {
                ["TaxAiSettings:TypeMapping:PARENT:IfDisabled:0"] = "PARENT_DISABLED",
                ["TaxAiSettings:TypeMapping:PARENT:IfRetired:0"] = "PARENT_RETIRED",
                ["TaxAiSettings:TypeMapping:PARENT:Default:0"] = "PARENT_DISABLED",
                ["TaxAiSettings:TypeMapping:PARENT_RETIRED:Default:0"] = "PARENT_RETIRED",
                ["TaxAiSettings:TypeMapping:DEFAULT:Default:0"] = "OTHER_HELPLESS"
            };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(configData).Build();
            var service = new DependentRuleService(_mockUnitOfWork.Object, _cache, configuration);

            var addedRules = new List<DependentDocumentRule>();
            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<DependentDocumentRule>());
            _mockRepo.Setup(r => r.AddAsync(It.IsAny<DependentDocumentRule>()))
                     .Callback<DependentDocumentRule>(r => addedRules.Add(r))
                     .Returns(Task.CompletedTask);

            var aiRules = new List<DependentRuleFromAiDto>
            {
                // Direct detailed enum PARENT_RETIRED
                new DependentRuleFromAiDto
                {
                    DependentType = "PARENT_RETIRED",
                    RequiredDocuments = new List<RequiredDocumentFromAiDto>
                    {
                        new RequiredDocumentFromAiDto { DocType = "PENSION_STATEMENT", IsMandatory = true }
                    }
                },
                // PARENT with isRetired = true
                new DependentRuleFromAiDto
                {
                    DependentType = "PARENT",
                    IsRetired = true,
                    RequiredDocuments = new List<RequiredDocumentFromAiDto>
                    {
                        new RequiredDocumentFromAiDto { DocType = "CITIZEN_ID", IsMandatory = true }
                    }
                },
                // PARENT with isDisabled = true
                new DependentRuleFromAiDto
                {
                    DependentType = "PARENT",
                    IsDisabled = true,
                    RequiredDocuments = new List<RequiredDocumentFromAiDto>
                    {
                        new RequiredDocumentFromAiDto { DocType = "DISABILITY_CERTIFICATE", IsMandatory = true }
                    }
                },
                // SPOUSE_RETIRED not in config, but matches DependentGroup enum directly
                new DependentRuleFromAiDto
                {
                    DependentType = "SPOUSE_RETIRED",
                    RequiredDocuments = new List<RequiredDocumentFromAiDto>
                    {
                        new RequiredDocumentFromAiDto { DocType = "CITIZEN_ID", IsMandatory = true }
                    }
                }
            };

            var (added, updated, deactivated) = await service.SyncFromAiAsync(aiRules);

            added.Should().Be(4);
            updated.Should().Be(0);

            addedRules.Should().Contain(r => r.TargetGroup == "PARENT_RETIRED" && r.DocType == "PENSION_STATEMENT");
            addedRules.Should().Contain(r => r.TargetGroup == "PARENT_RETIRED" && r.DocType == "CITIZEN_ID");
            addedRules.Should().Contain(r => r.TargetGroup == "PARENT_DISABLED" && r.DocType == "DISABILITY_CERTIFICATE");
            addedRules.Should().Contain(r => r.TargetGroup == "SPOUSE_RETIRED" && r.DocType == "CITIZEN_ID");
        }

        [Theory]
        [InlineData("CT07", "RESIDENCE_CT07")]
        [InlineData("RESIDENCE_CT07", "RESIDENCE_CT07")]
        [InlineData("RESIDENCE", "RESIDENCE_CT07")]
        [InlineData("GIAY_XAC_NHAN_CU_TRU", "RESIDENCE_CT07")]
        [InlineData("ho_khau", "RESIDENCE_CT07")]
        [InlineData("HOUSEHOLD_CERTIFICATE", "RESIDENCE_CT07")]
        [InlineData("KHAI_SINH", "BIRTH_CERTIFICATE")]
        [InlineData("CCCD", "CITIZEN_ID")]
        [InlineData("CMND", "CITIZEN_ID")]
        [InlineData("KET_HON", "MARRIAGE_CERTIFICATE")]
        [InlineData("THE_SINH_VIEN", "STUDENT_CARD")]
        [InlineData("DISABILITY", "DISABILITY_CERTIFICATE")]
        [InlineData("CAM_KET", "SUPPORT_COMMITMENT_FORM")]
        [InlineData("HUU_TRI", "PENSION_STATEMENT")]
        [InlineData("BIRTH_CERTIFICATE", "BIRTH_CERTIFICATE")]
        [InlineData("RELATIONSHIP_CERTIFICATE", "RELATIONSHIP_CERTIFICATE")]
        [InlineData("CUSTOM_DOC", "CUSTOM_DOC")]
        public void NormalizeDocType_MapsAiAliasesCorrectly(string input, string expected)
        {
            var result = DependentRuleService.NormalizeDocType(input);
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("RESIDENCE_CT07", "CT07", true)]
        [InlineData("CT07", "RELATIONSHIP_CERTIFICATE", false)]
        [InlineData("RELATIONSHIP_CERTIFICATE", "RESIDENCE_CT07", false)]
        [InlineData("RELATIONSHIP_CERTIFICATE", "RELATIONSHIP_CERTIFICATE", true)]
        public void IsDocTypeMatching_KeepsResidenceAndRelationshipTypesDistinct(
            string expected,
            string detected,
            bool matches)
        {
            StringComparisonHelper.IsDocTypeMatching(expected, detected).Should().Be(matches);
        }

        #endregion
    }
}
