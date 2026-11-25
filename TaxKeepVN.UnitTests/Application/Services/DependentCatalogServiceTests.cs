using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using TaxKeepVN.Application.DTOs.Catalog;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Implementations;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;
using Xunit;

namespace TaxKeepVN.UnitTests.Application.Services
{
    public class DependentCatalogServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IGenericRepository<DependentRelationshipCatalog>> _mockRelRepo;
        private readonly Mock<IGenericRepository<DependentGroupCatalog>> _mockGroupRepo;
        private readonly Mock<IGenericRepository<DependentDocumentRule>> _mockRuleRepo;
        private readonly Mock<IRedisCacheService> _mockRedis;
        private readonly IMemoryCache _cache;
        private readonly DependentCatalogService _service;

        public DependentCatalogServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockRelRepo = new Mock<IGenericRepository<DependentRelationshipCatalog>>();
            _mockGroupRepo = new Mock<IGenericRepository<DependentGroupCatalog>>();
            _mockRuleRepo = new Mock<IGenericRepository<DependentDocumentRule>>();
            _mockRedis = new Mock<IRedisCacheService>();
            _cache = new MemoryCache(new MemoryCacheOptions());

            _mockUnitOfWork.Setup(u => u.Repository<DependentRelationshipCatalog>()).Returns(_mockRelRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<DependentGroupCatalog>()).Returns(_mockGroupRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<DependentDocumentRule>()).Returns(_mockRuleRepo.Object);
            _mockUnitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

            _mockRelRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<DependentRelationshipCatalog>());

            _service = new DependentCatalogService(_mockUnitOfWork.Object, _cache, _mockRedis.Object);
        }

        #region GetActiveCatalogAsync / GetActiveRelationshipsAsync / GetActiveGroupsAsync / GetGroupAsync

        [Fact]
        public async Task GetActiveCatalogAsync_CacheMiss_LoadsFromDbAndPopulatesCaches()
        {
            var rels = new List<DependentRelationshipCatalog>
            {
                new() { Code = "PARENT", Name = "Cha/Mẹ", DisplayOrder = 2, IsActive = true },
                new() { Code = "CHILD", Name = "Con", DisplayOrder = 1, IsActive = true }
            };
            var groups = new List<DependentGroupCatalog>
            {
                new() { Code = "CHILD_OVER_18_STUDYING", Name = "Con >= 18 đang học", RelationshipCode = "CHILD", MinAge = 18, RequireStudying = true, Level = 1, DisplayOrder = 2, IsActive = true },
                new() { Code = "CHILD_UNDER_18", Name = "Con < 18", RelationshipCode = "CHILD", MaxAge = 18, Level = 0, DisplayOrder = 1, IsActive = true }
            };

            _mockRedis.Setup(r => r.GetAsync<DependentCatalogResponseDto>("taxkeep:dependents:catalog"))
                .ReturnsAsync((DependentCatalogResponseDto?)null);
            _mockRelRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentRelationshipCatalog, bool>>>()))
                .ReturnsAsync(rels);
            _mockGroupRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentGroupCatalog, bool>>>()))
                .ReturnsAsync(groups);

            var result = await _service.GetActiveCatalogAsync();

            result.Relationships.Select(r => r.Code).Should().ContainInOrder("CHILD", "PARENT");
            result.Groups.Select(g => g.Code).Should().ContainInOrder("CHILD_UNDER_18", "CHILD_OVER_18_STUDYING");

            _cache.TryGetValue("DependentCatalog", out DependentCatalogResponseDto? memCached).Should().BeTrue();
            memCached.Should().NotBeNull();
            _mockRedis.Verify(r => r.SetAsync("taxkeep:dependents:catalog", It.IsAny<DependentCatalogResponseDto>(), TimeSpan.FromHours(24)), Times.Once);
        }

        [Fact]
        public async Task GetActiveCatalogAsync_RedisHit_ReturnsCachedWithoutDbCall()
        {
            var cachedDto = new DependentCatalogResponseDto
            {
                Relationships = new List<DependentRelationshipItemDto> { new() { Code = "CHILD", Name = "Con", DisplayOrder = 1 } },
                Groups = new List<DependentGroupItemDto>()
            };
            _mockRedis.Setup(r => r.GetAsync<DependentCatalogResponseDto>("taxkeep:dependents:catalog"))
                .ReturnsAsync(cachedDto);

            var result = await _service.GetActiveCatalogAsync();

            result.Should().BeSameAs(cachedDto);
            _mockRelRepo.Verify(r => r.FindAsync(It.IsAny<Expression<Func<DependentRelationshipCatalog, bool>>>()), Times.Never);
            _mockGroupRepo.Verify(r => r.FindAsync(It.IsAny<Expression<Func<DependentGroupCatalog, bool>>>()), Times.Never);
        }

        [Fact]
        public async Task GetActiveCatalogAsync_MemoryCacheHit_ReturnsCachedWithoutDbCall()
        {
            var cachedDto = new DependentCatalogResponseDto
            {
                Relationships = new List<DependentRelationshipItemDto> { new() { Code = "SPOUSE", Name = "Vợ/Chồng", DisplayOrder = 1 } },
                Groups = new List<DependentGroupItemDto>()
            };
            _cache.Set("DependentCatalog", cachedDto);
            _mockRedis.Setup(r => r.GetAsync<DependentCatalogResponseDto>("taxkeep:dependents:catalog"))
                .ThrowsAsync(new Exception("Redis down"));

            var result = await _service.GetActiveCatalogAsync();

            result.Should().BeSameAs(cachedDto);
            _mockRelRepo.Verify(r => r.FindAsync(It.IsAny<Expression<Func<DependentRelationshipCatalog, bool>>>()), Times.Never);
        }

        [Fact]
        public async Task GetActiveRelationshipsAsync_ReturnsSortedAndCaches()
        {
            var rels = new List<DependentRelationshipCatalog>
            {
                new() { Code = "SPOUSE", Name = "Vợ/Chồng", DisplayOrder = 2, IsActive = true },
                new() { Code = "CHILD", Name = "Con", DisplayOrder = 1, IsActive = true }
            };
            _mockRelRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentRelationshipCatalog, bool>>>()))
                .ReturnsAsync(rels);

            var first = await _service.GetActiveRelationshipsAsync();
            var second = await _service.GetActiveRelationshipsAsync();

            first.Select(r => r.Code).Should().ContainInOrder("CHILD", "SPOUSE");
            second.Should().BeSameAs(first);
            _mockRelRepo.Verify(r => r.FindAsync(It.IsAny<Expression<Func<DependentRelationshipCatalog, bool>>>()), Times.Once);
        }

        [Fact]
        public async Task GetActiveGroupsAsync_WithRelationshipFilter_NormalizesAndCaches()
        {
            var spouseGroups = new List<DependentGroupCatalog>
            {
                new() { Code = "SPOUSE_RETIRED", RelationshipCode = "SPOUSE", DisplayOrder = 2, IsActive = true },
                new() { Code = "SPOUSE_DISABLED", RelationshipCode = "SPOUSE", DisplayOrder = 1, IsActive = true }
            };
            _mockGroupRepo.Setup(g => g.FindAsync(It.IsAny<Expression<Func<DependentGroupCatalog, bool>>>()))
                .ReturnsAsync(spouseGroups);

            var result = await _service.GetActiveGroupsAsync(" spouse ");

            result.Select(g => g.Code).Should().ContainInOrder("SPOUSE_DISABLED", "SPOUSE_RETIRED");
            _cache.TryGetValue("ActiveDependentGroups_SPOUSE", out List<DependentGroupCatalog>? _).Should().BeTrue();
        }

        [Fact]
        public async Task GetGroupAsync_BlankCode_ReturnsNull()
        {
            var result = await _service.GetGroupAsync("   ");
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetGroupAsync_IncludeInactiveTrue_QueriesRepoDirectly()
        {
            var inactiveGroup = new DependentGroupCatalog { Code = "OLD_GROUP", IsActive = false };
            _mockGroupRepo.Setup(g => g.FindAsync(It.IsAny<Expression<Func<DependentGroupCatalog, bool>>>()))
                .ReturnsAsync(new List<DependentGroupCatalog> { inactiveGroup });

            var result = await _service.GetGroupAsync("old_group", includeInactive: true);

            result.Should().BeSameAs(inactiveGroup);
        }

        #endregion

        #region Admin Groups CRUD

        [Fact]
        public async Task GetAllGroupsAsync_FiltersByIsActiveAndSorts()
        {
            var all = new List<DependentGroupCatalog>
            {
                new() { Code = "B_GROUP", DisplayOrder = 2, IsActive = true },
                new() { Code = "A_GROUP", DisplayOrder = 1, IsActive = true },
                new() { Code = "INACTIVE_GROUP", DisplayOrder = 0, IsActive = false }
            };
            _mockGroupRepo.Setup(g => g.GetAllAsync()).ReturnsAsync(all);

            var activeOnly = await _service.GetAllGroupsAsync(isActive: true);

            activeOnly.Select(g => g.Code).Should().ContainInOrder("A_GROUP", "B_GROUP");
        }

        [Fact]
        public async Task GetGroupByCodeAsync_NotFound_ThrowsNotFoundException()
        {
            _mockGroupRepo.Setup(g => g.FindAsync(It.IsAny<Expression<Func<DependentGroupCatalog, bool>>>()))
                .ReturnsAsync(new List<DependentGroupCatalog>());

            var act = () => _service.GetGroupByCodeAsync("MISSING");

            await act.Should().ThrowAsync<NotFoundException>();
        }

        [Fact]
        public async Task CreateGroupAsync_Valid_CreatesAndInvalidatesCache()
        {
            _mockGroupRepo.Setup(g => g.FindAsync(It.IsAny<Expression<Func<DependentGroupCatalog, bool>>>()))
                .ReturnsAsync(new List<DependentGroupCatalog>());
            _mockRelRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentRelationshipCatalog, bool>>>()))
                .ReturnsAsync(new List<DependentRelationshipCatalog> { new() { Code = "CHILD", IsActive = true } });

            _cache.Set("DependentCatalog", new DependentCatalogResponseDto());

            var req = new CreateDependentGroupRequest
            {
                Code = " child_new ",
                Name = " Nhóm mới ",
                RelationshipCode = "child",
                MinAge = 0,
                MaxAge = 18,
                Level = 0,
                DisplayOrder = 1,
                IsActive = true
            };

            var res = await _service.CreateGroupAsync(req);

            res.Code.Should().Be("CHILD_NEW");
            res.Name.Should().Be("Nhóm mới");
            res.RelationshipCode.Should().Be("CHILD");
            res.Source.Should().Be("ADMIN");
            _mockGroupRepo.Verify(g => g.AddAsync(It.Is<DependentGroupCatalog>(x => x.Code == "CHILD_NEW")), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
            _cache.TryGetValue("DependentCatalog", out _).Should().BeFalse();
        }

        [Fact]
        public async Task CreateGroupAsync_DuplicateCode_ThrowsConflictException()
        {
            _mockGroupRepo.Setup(g => g.FindAsync(It.IsAny<Expression<Func<DependentGroupCatalog, bool>>>()))
                .ReturnsAsync(new List<DependentGroupCatalog> { new() { Code = "CHILD_UNDER_18" } });

            var req = new CreateDependentGroupRequest { Code = "child_under_18", Name = "Dup", RelationshipCode = "CHILD", IsActive = true };

            var ex = await Assert.ThrowsAsync<ConflictException>(() => _service.CreateGroupAsync(req));
            ex.ErrorCode.Should().Be("DUPLICATE_GROUP");
        }

        [Fact]
        public async Task CreateGroupAsync_InvalidRelationship_ThrowsBadRequestException()
        {
            _mockGroupRepo.Setup(g => g.FindAsync(It.IsAny<Expression<Func<DependentGroupCatalog, bool>>>()))
                .ReturnsAsync(new List<DependentGroupCatalog>());
            _mockRelRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentRelationshipCatalog, bool>>>()))
                .ReturnsAsync(new List<DependentRelationshipCatalog>());

            var req = new CreateDependentGroupRequest { Code = "NEW_G", Name = "New", RelationshipCode = "UNKNOWN", IsActive = true };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() => _service.CreateGroupAsync(req));
            ex.ErrorCode.Should().Be("INVALID_RELATIONSHIP");
        }

        [Fact]
        public async Task CreateGroupAsync_ActiveWithoutRelationship_ThrowsBadRequestException()
        {
            _mockGroupRepo.Setup(g => g.FindAsync(It.IsAny<Expression<Func<DependentGroupCatalog, bool>>>()))
                .ReturnsAsync(new List<DependentGroupCatalog>());

            var req = new CreateDependentGroupRequest { Code = "NEW_G", Name = "New", RelationshipCode = null, IsActive = true };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() => _service.CreateGroupAsync(req));
            ex.ErrorCode.Should().Be("RELATIONSHIP_REQUIRED");
        }

        [Fact]
        public async Task UpdateGroupAsync_Valid_UpdatesAndInvalidatesCache()
        {
            var existing = new DependentGroupCatalog { Code = "SPOUSE_RETIRED", Name = "Old", RelationshipCode = "SPOUSE", IsActive = false };
            _mockGroupRepo.Setup(g => g.FindAsync(It.IsAny<Expression<Func<DependentGroupCatalog, bool>>>()))
                .ReturnsAsync(new List<DependentGroupCatalog> { existing });
            _mockRelRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentRelationshipCatalog, bool>>>()))
                .ReturnsAsync(new List<DependentRelationshipCatalog> { new() { Code = "SPOUSE" } });

            var res = await _service.UpdateGroupAsync("spouse_retired", new AdminUpdateDependentGroupRequest
            {
                Name = "Vợ/Chồng hết tuổi lao động",
                IsActive = true,
                MinAge = 55
            });

            res.Name.Should().Be("Vợ/Chồng hết tuổi lao động");
            res.IsActive.Should().BeTrue();
            res.MinAge.Should().Be(55);
            _mockGroupRepo.Verify(g => g.Update(existing), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task UpdateGroupAsync_ActivatingWithoutRelationship_ThrowsBadRequestException()
        {
            var existing = new DependentGroupCatalog { Code = "AI_GROUP", Name = "AI", RelationshipCode = null, IsActive = false };
            _mockGroupRepo.Setup(g => g.FindAsync(It.IsAny<Expression<Func<DependentGroupCatalog, bool>>>()))
                .ReturnsAsync(new List<DependentGroupCatalog> { existing });

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.UpdateGroupAsync("AI_GROUP", new AdminUpdateDependentGroupRequest { IsActive = true }));
            ex.ErrorCode.Should().Be("RELATIONSHIP_REQUIRED");
        }

        [Fact]
        public async Task DeleteGroupAsync_HasActiveRules_ThrowsBadRequestException()
        {
            var existing = new DependentGroupCatalog { Code = "CHILD_UNDER_18" };
            _mockGroupRepo.Setup(g => g.FindAsync(It.IsAny<Expression<Func<DependentGroupCatalog, bool>>>()))
                .ReturnsAsync(new List<DependentGroupCatalog> { existing });
            _mockRuleRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocumentRule, bool>>>()))
                .ReturnsAsync(new List<DependentDocumentRule> { new() { TargetGroup = "CHILD_UNDER_18", IsActive = true } });

            var ex = await Assert.ThrowsAsync<BadRequestException>(() => _service.DeleteGroupAsync("CHILD_UNDER_18"));
            ex.ErrorCode.Should().Be("GROUP_HAS_ACTIVE_RULES");
        }

        [Fact]
        public async Task DeleteGroupAsync_NoActiveRules_RemovesAndSaves()
        {
            var existing = new DependentGroupCatalog { Code = "UNUSED_GROUP" };
            _mockGroupRepo.Setup(g => g.FindAsync(It.IsAny<Expression<Func<DependentGroupCatalog, bool>>>()))
                .ReturnsAsync(new List<DependentGroupCatalog> { existing });
            _mockRuleRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentDocumentRule, bool>>>()))
                .ReturnsAsync(new List<DependentDocumentRule>());

            await _service.DeleteGroupAsync("unused_group");

            _mockGroupRepo.Verify(g => g.Remove(existing), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task ActivateAiGroupAsync_Valid_ActivatesAndAssignsRelationship()
        {
            var aiGroup = new DependentGroupCatalog { Code = "AI_NEW", Name = "AI Suggested", IsActive = false, Source = "AI" };
            _mockGroupRepo.Setup(g => g.FindAsync(It.IsAny<Expression<Func<DependentGroupCatalog, bool>>>()))
                .ReturnsAsync(new List<DependentGroupCatalog> { aiGroup });
            _mockRelRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentRelationshipCatalog, bool>>>()))
                .ReturnsAsync(new List<DependentRelationshipCatalog> { new() { Code = "PARENT" } });

            var res = await _service.ActivateAiGroupAsync("ai_new", new ActivateAiGroupRequest
            {
                RelationshipCode = "parent",
                Name = "Cha/Mẹ đặc biệt",
                Level = 1
            });

            res.IsActive.Should().BeTrue();
            res.RelationshipCode.Should().Be("PARENT");
            res.Name.Should().Be("Cha/Mẹ đặc biệt");
            _mockGroupRepo.Verify(g => g.Update(aiGroup), Times.Once);
        }

        #endregion

        #region Admin Relationships CRUD

        [Fact]
        public async Task GetAllRelationshipsAsync_FiltersByIsActive()
        {
            var rels = new List<DependentRelationshipCatalog>
            {
                new() { Code = "CHILD", Name = "Con", DisplayOrder = 1, IsActive = true },
                new() { Code = "OLD", Name = "Cũ", DisplayOrder = 2, IsActive = false }
            };
            _mockRelRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(rels);

            var res = await _service.GetAllRelationshipsAsync(isActive: true);

            res.Should().HaveCount(1);
            res[0].Code.Should().Be("CHILD");
        }

        [Fact]
        public async Task GetRelationshipByCodeAsync_NotFound_ThrowsNotFoundException()
        {
            _mockRelRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentRelationshipCatalog, bool>>>()))
                .ReturnsAsync(new List<DependentRelationshipCatalog>());

            var act = () => _service.GetRelationshipByCodeAsync("NOPE");
            await act.Should().ThrowAsync<NotFoundException>();
        }

        [Fact]
        public async Task CreateRelationshipAsync_Duplicate_ThrowsConflictException()
        {
            _mockRelRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentRelationshipCatalog, bool>>>()))
                .ReturnsAsync(new List<DependentRelationshipCatalog> { new() { Code = "CHILD" } });

            var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                _service.CreateRelationshipAsync(new CreateDependentRelationshipRequest { Code = "child", Name = "Con" }));
            ex.ErrorCode.Should().Be("DUPLICATE_RELATIONSHIP");
        }

        [Fact]
        public async Task CreateRelationshipAsync_Valid_CreatesAndSaves()
        {
            _mockRelRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentRelationshipCatalog, bool>>>()))
                .ReturnsAsync(new List<DependentRelationshipCatalog>());

            var res = await _service.CreateRelationshipAsync(new CreateDependentRelationshipRequest
            {
                Code = " guardian ",
                Name = " Người giám hộ ",
                DisplayOrder = 5,
                IsActive = true
            });

            res.Code.Should().Be("GUARDIAN");
            res.Name.Should().Be("Người giám hộ");
            _mockRelRepo.Verify(r => r.AddAsync(It.Is<DependentRelationshipCatalog>(x => x.Code == "GUARDIAN")), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task UpdateRelationshipAsync_Valid_UpdatesAndSaves()
        {
            var rel = new DependentRelationshipCatalog { Code = "CHILD", Name = "Old", DisplayOrder = 1, IsActive = true };
            _mockRelRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentRelationshipCatalog, bool>>>()))
                .ReturnsAsync(new List<DependentRelationshipCatalog> { rel });

            var res = await _service.UpdateRelationshipAsync("child", new UpdateDependentRelationshipRequest
            {
                Name = "Con đẻ / Con nuôi",
                DisplayOrder = 2
            });

            res.Name.Should().Be("Con đẻ / Con nuôi");
            res.DisplayOrder.Should().Be(2);
            _mockRelRepo.Verify(r => r.Update(rel), Times.Once);
        }

        [Fact]
        public async Task DeleteRelationshipAsync_InUseByGroup_ThrowsBadRequestException()
        {
            var rel = new DependentRelationshipCatalog { Code = "CHILD" };
            _mockRelRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentRelationshipCatalog, bool>>>()))
                .ReturnsAsync(new List<DependentRelationshipCatalog> { rel });
            _mockGroupRepo.Setup(g => g.FindAsync(It.IsAny<Expression<Func<DependentGroupCatalog, bool>>>()))
                .ReturnsAsync(new List<DependentGroupCatalog> { new() { Code = "CHILD_UNDER_18", RelationshipCode = "CHILD" } });

            var ex = await Assert.ThrowsAsync<BadRequestException>(() => _service.DeleteRelationshipAsync("CHILD"));
            ex.ErrorCode.Should().Be("RELATIONSHIP_IN_USE");
        }

        [Fact]
        public async Task DeleteRelationshipAsync_NotInUse_RemovesAndSaves()
        {
            var rel = new DependentRelationshipCatalog { Code = "TEMP" };
            _mockRelRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DependentRelationshipCatalog, bool>>>()))
                .ReturnsAsync(new List<DependentRelationshipCatalog> { rel });
            _mockGroupRepo.Setup(g => g.FindAsync(It.IsAny<Expression<Func<DependentGroupCatalog, bool>>>()))
                .ReturnsAsync(new List<DependentGroupCatalog>());

            await _service.DeleteRelationshipAsync("temp");

            _mockRelRepo.Verify(r => r.Remove(rel), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion
    }
}
