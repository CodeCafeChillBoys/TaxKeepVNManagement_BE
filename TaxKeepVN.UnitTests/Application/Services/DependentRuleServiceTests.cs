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
        private readonly IMemoryCache _cache;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly DependentRuleService _service;

        public DependentRuleServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockRepo = new Mock<IGenericRepository<DependentDocumentRule>>();
            _cache = new MemoryCache(new MemoryCacheOptions());
            _mockConfiguration = new Mock<IConfiguration>();

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

        #endregion

        #region GetActiveGroupsAsync

        [Fact]
        public async Task UTCID10_GetActiveGroupsAsync_ReturnsDistinctGroups()
        {
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
                TargetGroup = "OTHER_HELPLESS",
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

            var (added, updated) = await _service.SyncFromAiAsync(aiRules);

            added.Should().Be(1);
            updated.Should().Be(1);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
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

            var (added, updated) = await service.SyncFromAiAsync(aiRules);
            updated.Should().Be(1); // CITIZEN_ID was existing
            added.Should().Be(4);   // DISABILITY_CERTIFICATE, STUDENT_CARD, BIRTH_CERTIFICATE, OTHER_DOC
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.AtLeastOnce());
        }

        #endregion
    }
}
