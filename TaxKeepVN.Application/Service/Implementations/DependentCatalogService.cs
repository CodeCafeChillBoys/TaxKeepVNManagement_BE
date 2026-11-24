using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Catalog;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    public class DependentCatalogService : IDependentCatalogService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMemoryCache _cache;
        private readonly IRedisCacheService? _redisCache;

        private const string CatalogCacheKey = "DependentCatalog";
        private const string CatalogRedisKey = "taxkeep:dependents:catalog";

        public DependentCatalogService(
            IUnitOfWork unitOfWork,
            IMemoryCache cache,
            IRedisCacheService? redisCache = null)
        {
            _unitOfWork = unitOfWork;
            _cache = cache;
            _redisCache = redisCache;
        }

        public async Task<DependentCatalogResponseDto> GetActiveCatalogAsync()
        {
            if (_redisCache != null)
            {
                try
                {
                    var cachedRedis = await _redisCache.GetAsync<DependentCatalogResponseDto>(CatalogRedisKey);
                    if (cachedRedis != null)
                    {
                        return cachedRedis;
                    }
                }
                catch { }
            }

            if (_cache.TryGetValue(CatalogCacheKey, out DependentCatalogResponseDto? cachedMemory) && cachedMemory != null)
            {
                return cachedMemory;
            }

            var relRepo = _unitOfWork.Repository<DependentRelationshipCatalog>();
            var groupRepo = _unitOfWork.Repository<DependentGroupCatalog>();

            var activeRels = relRepo != null
                ? (await relRepo.FindAsync(r => r.IsActive)).OrderBy(r => r.DisplayOrder).ToList()
                : new List<DependentRelationshipCatalog>();

            var activeGroups = groupRepo != null
                ? (await groupRepo.FindAsync(g => g.IsActive)).OrderBy(g => g.DisplayOrder).ToList()
                : new List<DependentGroupCatalog>();

            var response = new DependentCatalogResponseDto
            {
                Relationships = activeRels.Select(r => new DependentRelationshipItemDto
                {
                    Code = r.Code,
                    Name = r.Name,
                    DisplayOrder = r.DisplayOrder
                }).ToList(),
                Groups = activeGroups.Select(g => new DependentGroupItemDto
                {
                    Code = g.Code,
                    Name = g.Name,
                    RelationshipCode = g.RelationshipCode,
                    MinAge = g.MinAge,
                    MaxAge = g.MaxAge,
                    RequireDisabled = g.RequireDisabled,
                    RequireStudying = g.RequireStudying,
                    Level = g.Level,
                    DisplayOrder = g.DisplayOrder
                }).ToList()
            };

            _cache.Set(CatalogCacheKey, response, new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromHours(1)));
            if (_redisCache != null)
            {
                try
                {
                    await _redisCache.SetAsync(CatalogRedisKey, response, TimeSpan.FromHours(24));
                }
                catch { }
            }

            return response;
        }

        #region Admin Groups CRUD

        public async Task<List<AdminDependentGroupDto>> GetAllGroupsAsync(bool? isActive = null)
        {
            var repo = _unitOfWork.Repository<DependentGroupCatalog>();
            if (repo == null) return new List<AdminDependentGroupDto>();

            var groups = await repo.GetAllAsync();
            if (isActive.HasValue)
            {
                groups = groups.Where(g => g.IsActive == isActive.Value);
            }

            return groups
                .OrderBy(g => g.DisplayOrder)
                .ThenBy(g => g.Code)
                .Select(MapGroupToAdminDto)
                .ToList();
        }

        public async Task<AdminDependentGroupDto> GetGroupByCodeAsync(string code)
        {
            var repo = _unitOfWork.Repository<DependentGroupCatalog>();
            var group = (await repo.FindAsync(g => g.Code == code.Trim().ToUpper())).FirstOrDefault();
            if (group == null)
                throw new NotFoundException($"Không tìm thấy nhóm người phụ thuộc với mã '{code}'.");

            return MapGroupToAdminDto(group);
        }

        public async Task<AdminDependentGroupDto> CreateGroupAsync(CreateDependentGroupRequest request)
        {
            var normCode = request.Code.Trim().ToUpper();
            var repo = _unitOfWork.Repository<DependentGroupCatalog>();
            var existing = (await repo.FindAsync(g => g.Code == normCode)).FirstOrDefault();
            if (existing != null)
                throw new ConflictException("DUPLICATE_GROUP", $"Nhóm người phụ thuộc với mã '{normCode}' đã tồn tại.");

            string? relCode = null;
            if (!string.IsNullOrWhiteSpace(request.RelationshipCode))
            {
                relCode = request.RelationshipCode.Trim().ToUpper();
                var relRepo = _unitOfWork.Repository<DependentRelationshipCatalog>();
                var relExists = (await relRepo.FindAsync(r => r.Code == relCode)).Any();
                if (!relExists)
                    throw new BadRequestException("INVALID_RELATIONSHIP", $"Mã quan hệ '{relCode}' không tồn tại trong hệ thống.");
            }

            if (request.IsActive && string.IsNullOrWhiteSpace(relCode))
            {
                throw new BadRequestException("RELATIONSHIP_REQUIRED", "Không thể tạo nhóm người phụ thuộc ở trạng thái kích hoạt khi chưa gán nhóm quan hệ (relationship_code).");
            }

            var newGroup = new DependentGroupCatalog
            {
                Code = normCode,
                Name = request.Name.Trim(),
                RelationshipCode = relCode,
                MinAge = request.MinAge,
                MaxAge = request.MaxAge,
                RequireDisabled = request.RequireDisabled,
                RequireStudying = request.RequireStudying,
                Level = request.Level,
                DisplayOrder = request.DisplayOrder,
                IsActive = request.IsActive,
                Source = "ADMIN",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await repo.AddAsync(newGroup);
            await _unitOfWork.SaveChangesAsync();

            InvalidateCatalogCache();
            return MapGroupToAdminDto(newGroup);
        }

        public async Task<AdminDependentGroupDto> UpdateGroupAsync(string code, UpdateDependentGroupRequest request)
        {
            var repo = _unitOfWork.Repository<DependentGroupCatalog>();
            var group = (await repo.FindAsync(g => g.Code == code.Trim().ToUpper())).FirstOrDefault();
            if (group == null)
                throw new NotFoundException($"Không tìm thấy nhóm người phụ thuộc với mã '{code}'.");

            if (request.Name != null) group.Name = request.Name.Trim();
            if (request.RelationshipCode != null)
            {
                var relCode = request.RelationshipCode.Trim().ToUpper();
                if (!string.IsNullOrWhiteSpace(relCode))
                {
                    var relRepo = _unitOfWork.Repository<DependentRelationshipCatalog>();
                    var relExists = (await relRepo.FindAsync(r => r.Code == relCode)).Any();
                    if (!relExists)
                        throw new BadRequestException("INVALID_RELATIONSHIP", $"Mã quan hệ '{relCode}' không tồn tại.");
                    group.RelationshipCode = relCode;
                }
                else
                {
                    group.RelationshipCode = null;
                }
            }

            if (request.MinAge.HasValue) group.MinAge = request.MinAge.Value;
            if (request.MaxAge.HasValue) group.MaxAge = request.MaxAge.Value;
            if (request.RequireDisabled.HasValue) group.RequireDisabled = request.RequireDisabled.Value;
            if (request.RequireStudying.HasValue) group.RequireStudying = request.RequireStudying.Value;
            if (request.Level.HasValue) group.Level = request.Level.Value;
            if (request.DisplayOrder.HasValue) group.DisplayOrder = request.DisplayOrder.Value;
            if (request.IsActive.HasValue) group.IsActive = request.IsActive.Value;

            if (group.IsActive && string.IsNullOrWhiteSpace(group.RelationshipCode))
            {
                throw new BadRequestException("RELATIONSHIP_REQUIRED", "Không thể kích hoạt nhóm người phụ thuộc khi chưa gán nhóm quan hệ (relationship_code).");
            }

            group.UpdatedAt = DateTime.UtcNow;
            repo.Update(group);
            await _unitOfWork.SaveChangesAsync();

            InvalidateCatalogCache();
            return MapGroupToAdminDto(group);
        }

        public async Task DeleteGroupAsync(string code)
        {
            var repo = _unitOfWork.Repository<DependentGroupCatalog>();
            var group = (await repo.FindAsync(g => g.Code == code.Trim().ToUpper())).FirstOrDefault();
            if (group == null)
                throw new NotFoundException($"Không tìm thấy nhóm người phụ thuộc với mã '{code}'.");

            // Kiểm tra xem nhóm có đang được dùng trong quy tắc hoặc người phụ thuộc không
            var ruleRepo = _unitOfWork.Repository<DependentDocumentRule>();
            var hasRules = (await ruleRepo.FindAsync(r => r.TargetGroup == group.Code && r.IsActive)).Any();
            if (hasRules)
                throw new BadRequestException("GROUP_HAS_ACTIVE_RULES", $"Không thể xóa nhóm '{group.Code}' vì vẫn còn quy tắc giấy tờ đang kích hoạt liên kết với nhóm này.");

            repo.Remove(group);
            await _unitOfWork.SaveChangesAsync();

            InvalidateCatalogCache();
        }

        public async Task<AdminDependentGroupDto> ActivateAiGroupAsync(string code, ActivateAiGroupRequest request)
        {
            var repo = _unitOfWork.Repository<DependentGroupCatalog>();
            var group = (await repo.FindAsync(g => g.Code == code.Trim().ToUpper())).FirstOrDefault();
            if (group == null)
                throw new NotFoundException($"Không tìm thấy nhóm người phụ thuộc với mã '{code}'.");

            var relCode = request.RelationshipCode.Trim().ToUpper();
            var relRepo = _unitOfWork.Repository<DependentRelationshipCatalog>();
            var relExists = (await relRepo.FindAsync(r => r.Code == relCode)).Any();
            if (!relExists)
                throw new BadRequestException("INVALID_RELATIONSHIP", $"Mã quan hệ '{relCode}' không hợp lệ hoặc không tồn tại.");

            group.RelationshipCode = relCode;
            if (!string.IsNullOrWhiteSpace(request.Name)) group.Name = request.Name.Trim();
            if (request.MinAge.HasValue) group.MinAge = request.MinAge.Value;
            if (request.MaxAge.HasValue) group.MaxAge = request.MaxAge.Value;
            if (request.RequireDisabled.HasValue) group.RequireDisabled = request.RequireDisabled.Value;
            if (request.RequireStudying.HasValue) group.RequireStudying = request.RequireStudying.Value;
            if (request.Level.HasValue) group.Level = request.Level.Value;
            if (request.DisplayOrder.HasValue) group.DisplayOrder = request.DisplayOrder.Value;

            group.IsActive = true;
            group.UpdatedAt = DateTime.UtcNow;

            repo.Update(group);
            await _unitOfWork.SaveChangesAsync();

            InvalidateCatalogCache();
            return MapGroupToAdminDto(group);
        }

        #endregion

        #region Admin Relationships CRUD

        public async Task<List<AdminDependentRelationshipDto>> GetAllRelationshipsAsync(bool? isActive = null)
        {
            var repo = _unitOfWork.Repository<DependentRelationshipCatalog>();
            if (repo == null) return new List<AdminDependentRelationshipDto>();

            var rels = await repo.GetAllAsync();
            if (isActive.HasValue)
            {
                rels = rels.Where(r => r.IsActive == isActive.Value);
            }

            return rels
                .OrderBy(r => r.DisplayOrder)
                .ThenBy(r => r.Code)
                .Select(r => new AdminDependentRelationshipDto
                {
                    Code = r.Code,
                    Name = r.Name,
                    DisplayOrder = r.DisplayOrder,
                    IsActive = r.IsActive
                })
                .ToList();
        }

        public async Task<AdminDependentRelationshipDto> GetRelationshipByCodeAsync(string code)
        {
            var repo = _unitOfWork.Repository<DependentRelationshipCatalog>();
            var rel = (await repo.FindAsync(r => r.Code == code.Trim().ToUpper())).FirstOrDefault();
            if (rel == null)
                throw new NotFoundException($"Không tìm thấy quan hệ người phụ thuộc với mã '{code}'.");

            return new AdminDependentRelationshipDto
            {
                Code = rel.Code,
                Name = rel.Name,
                DisplayOrder = rel.DisplayOrder,
                IsActive = rel.IsActive
            };
        }

        public async Task<AdminDependentRelationshipDto> CreateRelationshipAsync(CreateDependentRelationshipRequest request)
        {
            var normCode = request.Code.Trim().ToUpper();
            var repo = _unitOfWork.Repository<DependentRelationshipCatalog>();
            var existing = (await repo.FindAsync(r => r.Code == normCode)).FirstOrDefault();
            if (existing != null)
                throw new ConflictException("DUPLICATE_RELATIONSHIP", $"Quan hệ người phụ thuộc với mã '{normCode}' đã tồn tại.");

            var newRel = new DependentRelationshipCatalog
            {
                Code = normCode,
                Name = request.Name.Trim(),
                DisplayOrder = request.DisplayOrder,
                IsActive = request.IsActive
            };

            await repo.AddAsync(newRel);
            await _unitOfWork.SaveChangesAsync();

            InvalidateCatalogCache();
            return new AdminDependentRelationshipDto
            {
                Code = newRel.Code,
                Name = newRel.Name,
                DisplayOrder = newRel.DisplayOrder,
                IsActive = newRel.IsActive
            };
        }

        public async Task<AdminDependentRelationshipDto> UpdateRelationshipAsync(string code, UpdateDependentRelationshipRequest request)
        {
            var repo = _unitOfWork.Repository<DependentRelationshipCatalog>();
            var rel = (await repo.FindAsync(r => r.Code == code.Trim().ToUpper())).FirstOrDefault();
            if (rel == null)
                throw new NotFoundException($"Không tìm thấy quan hệ người phụ thuộc với mã '{code}'.");

            if (request.Name != null) rel.Name = request.Name.Trim();
            if (request.DisplayOrder.HasValue) rel.DisplayOrder = request.DisplayOrder.Value;
            if (request.IsActive.HasValue) rel.IsActive = request.IsActive.Value;

            repo.Update(rel);
            await _unitOfWork.SaveChangesAsync();

            InvalidateCatalogCache();
            return new AdminDependentRelationshipDto
            {
                Code = rel.Code,
                Name = rel.Name,
                DisplayOrder = rel.DisplayOrder,
                IsActive = rel.IsActive
            };
        }

        public async Task DeleteRelationshipAsync(string code)
        {
            var repo = _unitOfWork.Repository<DependentRelationshipCatalog>();
            var rel = (await repo.FindAsync(r => r.Code == code.Trim().ToUpper())).FirstOrDefault();
            if (rel == null)
                throw new NotFoundException($"Không tìm thấy quan hệ người phụ thuộc với mã '{code}'.");

            var groupRepo = _unitOfWork.Repository<DependentGroupCatalog>();
            var hasGroups = (await groupRepo.FindAsync(g => g.RelationshipCode == rel.Code)).Any();
            if (hasGroups)
                throw new BadRequestException("RELATIONSHIP_IN_USE", $"Không thể xóa quan hệ '{rel.Code}' vì đang có các nhóm người phụ thuộc trực thuộc quan hệ này.");

            repo.Remove(rel);
            await _unitOfWork.SaveChangesAsync();

            InvalidateCatalogCache();
        }

        #endregion

        #region Private Helpers

        private void InvalidateCatalogCache()
        {
            _cache.Remove(CatalogCacheKey);
            _cache.Remove("ActiveDependentGroups");
            _cache.Remove("ActiveDependentGroups_All");
            _cache.Remove("ActiveDependentRelationships");
            _redisCache?.RemoveAsync(CatalogRedisKey);
            _redisCache?.RemoveAsync("taxkeep:dependent_rules:groups");

            var knownRels = new HashSet<string>(new[] { "CHILD", "SPOUSE", "PARENT", "OTHER_DEPENDENT" }, StringComparer.OrdinalIgnoreCase);
            var relRepo = _unitOfWork.Repository<DependentRelationshipCatalog>();
            if (relRepo != null)
            {
                try
                {
                    var allRels = relRepo.GetAllAsync().GetAwaiter().GetResult();
                    foreach (var r in allRels)
                    {
                        if (!string.IsNullOrWhiteSpace(r.Code))
                            knownRels.Add(r.Code.Trim().ToUpper());
                    }
                }
                catch { }
            }

            foreach (var rel in knownRels)
            {
                _cache.Remove($"ActiveDependentGroups_{rel}");
                _redisCache?.RemoveAsync($"taxkeep:dependent_rules:groups:{rel}");
            }
        }

        private static AdminDependentGroupDto MapGroupToAdminDto(DependentGroupCatalog g) => new()
        {
            Code = g.Code,
            Name = g.Name,
            RelationshipCode = g.RelationshipCode,
            MinAge = g.MinAge,
            MaxAge = g.MaxAge,
            RequireDisabled = g.RequireDisabled,
            RequireStudying = g.RequireStudying,
            Level = g.Level,
            DisplayOrder = g.DisplayOrder,
            IsActive = g.IsActive,
            Source = g.Source,
            CreatedAt = g.CreatedAt,
            UpdatedAt = g.UpdatedAt
        };

        #endregion
    }
}
