using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaxKeepVN.Application.Constants;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Rules;
using TaxKeepVN.Application.DTOs.TaxAI;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    public class DependentRuleService : IDependentRuleService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _configuration;
        private readonly IRedisCacheService? _redisCache;
        private readonly ILogger<DependentRuleService>? _logger;

        // legacy AI codes
        private static readonly Dictionary<string, string> LegacyAiTypeToRelationshipMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["ADULT_CHILD"] = "CHILD",
            ["OTHER"] = "OTHER_DEPENDENT"
        };

        public DependentRuleService(
            IUnitOfWork unitOfWork,
            IMemoryCache cache,
            IConfiguration configuration,
            IRedisCacheService? redisCache = null,
            ILogger<DependentRuleService>? logger = null)
        {
            _unitOfWork = unitOfWork;
            _cache = cache;
            _configuration = configuration;
            _redisCache = redisCache;
            _logger = logger;
        }

        #region Public Methods

        public async Task<PagedResult<DependentRuleResponseDto>> GetAllAsync(DependentRuleQueryParameters query)
        {
            var repo = _unitOfWork.Repository<DependentDocumentRule>();
            var rules = await repo.GetAllAsync();

            // Filter by TargetGroup if specified
            if (!string.IsNullOrWhiteSpace(query.TargetGroup))
            {
                var groupFilter = query.TargetGroup.Trim().ToUpper();
                rules = rules.Where(r => r.TargetGroup.Equals(groupFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Filter by IsActive if specified
            if (query.IsActive.HasValue)
            {
                rules = rules.Where(r => r.IsActive == query.IsActive.Value);
            }

            // Searching by TargetGroup, DocType, or Description
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var kw = query.Search.Trim().ToLower();
                rules = rules.Where(r =>
                    r.TargetGroup.ToLower().Contains(kw) ||
                    r.DocType.ToLower().Contains(kw) ||
                    (!string.IsNullOrEmpty(r.Description) && r.Description.ToLower().Contains(kw)) ||
                    (!string.IsNullOrEmpty(r.AlternativeGroup) && r.AlternativeGroup.ToLower().Contains(kw)));
            }

            // Sorting
            rules = ApplySort(rules, query.Sort);

            var totalItems = rules.Count();
            var items = rules
                .Skip((query.Page - 1) * query.Size)
                .Take(query.Size)
                .Select(MapToDto)
                .ToList();

            return new PagedResult<DependentRuleResponseDto>
            {
                Items = items,
                Pagination = new PaginationMeta
                {
                    Page = query.Page,
                    PageSize = query.Size,
                    TotalItems = totalItems,
                    TotalPages = (int)Math.Ceiling(totalItems / (double)query.Size)
                }
            };
        }

        public async Task<DependentRuleResponseDto> GetByIdAsync(Guid ruleId)
        {
            var rule = await _unitOfWork.Repository<DependentDocumentRule>().GetByIdAsync(ruleId);
            if (rule == null)
                throw new NotFoundException($"Không tìm thấy quy tắc giấy tờ với mã '{ruleId}'.");

            return MapToDto(rule);
        }

        public async Task<DependentRuleResponseDto> CreateAsync(CreateDependentRuleDto dto)
        {
            var targetGroup = dto.TargetGroup.Trim().ToUpper();
            var docType = dto.DocType.Trim().ToUpper();
            var alternativeGroup = NormalizeAlternativeGroup(dto.AlternativeGroup);

            var repo = _unitOfWork.Repository<DependentDocumentRule>();

            var existing = await repo.FindAsync(r =>
                r.TargetGroup.ToUpper() == targetGroup &&
                r.DocType.ToUpper() == docType);

            if (existing.Any(r => NormalizeAlternativeGroup(r.AlternativeGroup) == alternativeGroup))
            {
                throw new ConflictException("DUPLICATE_RULE",
                    $"Quy tắc cho nhóm đối tượng '{targetGroup}', loại giấy tờ '{docType}' và nhóm thay thế '{alternativeGroup ?? "(không có)"}' đã tồn tại trong hệ thống.");
            }

            var rule = new DependentDocumentRule
            {
                RuleId = Guid.NewGuid(),
                TargetGroup = targetGroup,
                DocType = docType,
                DocName = dto.DocName?.Trim(),
                IsMandatory = dto.IsMandatory,
                Description = dto.Description?.Trim(),
                AlternativeGroup = alternativeGroup,
                Source = string.IsNullOrWhiteSpace(dto.Source) ? "ADMIN" : dto.Source.Trim().ToUpper(),
                IsActive = dto.IsActive,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await repo.AddAsync(rule);
            await _unitOfWork.SaveChangesAsync();

            // Invalidate cache for this group
            await InvalidateGroupCacheAsync(targetGroup);

            return MapToDto(rule);
        }

        public async Task<DependentRuleResponseDto> UpdateAsync(Guid ruleId, UpdateDependentRuleDto dto)
        {
            var repo = _unitOfWork.Repository<DependentDocumentRule>();
            var rule = await repo.GetByIdAsync(ruleId);
            if (rule == null)
                throw new NotFoundException($"Không tìm thấy quy tắc giấy tờ với mã '{ruleId}'.");

            if (dto.AlternativeGroup != null)
            {
                var alternativeGroup = NormalizeAlternativeGroup(dto.AlternativeGroup);
                var matchingDocumentRules = await repo.FindAsync(r =>
                    r.RuleId != ruleId &&
                    r.TargetGroup.ToUpper() == rule.TargetGroup.ToUpper() &&
                    r.DocType.ToUpper() == rule.DocType.ToUpper());

                if (matchingDocumentRules.Any(r => NormalizeAlternativeGroup(r.AlternativeGroup) == alternativeGroup))
                {
                    throw new ConflictException("DUPLICATE_RULE",
                        $"Quy tắc cho nhóm đối tượng '{rule.TargetGroup}', loại giấy tờ '{rule.DocType}' và nhóm thay thế '{alternativeGroup ?? "(không có)"}' đã tồn tại trong hệ thống.");
                }

                rule.AlternativeGroup = alternativeGroup;
            }

            if (dto.DocName != null)
            {
                rule.DocName = dto.DocName.Trim();
            }
            if (dto.Source != null)
            {
                rule.Source = dto.Source.Trim().ToUpper();
            }
            rule.IsMandatory = dto.IsMandatory;
            rule.Description = dto.Description?.Trim();
            rule.IsActive = dto.IsActive;
            rule.UpdatedAt = DateTime.UtcNow;

            repo.Update(rule);
            await _unitOfWork.SaveChangesAsync();

            // Invalidate cache for this group
            await InvalidateGroupCacheAsync(rule.TargetGroup);

            return MapToDto(rule);
        }

        public async Task DeleteAsync(Guid ruleId)
        {
            var repo = _unitOfWork.Repository<DependentDocumentRule>();
            var rule = await repo.GetByIdAsync(ruleId);
            if (rule == null)
                throw new NotFoundException($"Không tìm thấy quy tắc giấy tờ với mã '{ruleId}'.");

            var targetGroup = rule.TargetGroup;
            repo.Remove(rule);
            await _unitOfWork.SaveChangesAsync();

            // Invalidate cache for this group
            await InvalidateGroupCacheAsync(targetGroup);
        }

        /// <summary>
        /// Lấy danh sách quy tắc tài liệu đang kích hoạt (active) cho một nhóm đối tượng.
        /// Sử dụng cache 2 lớp (MemoryCache và RedisCache).
        /// </summary>
        public async Task<List<DependentDocumentRule>> GetActiveRulesForGroupAsync(string targetGroup)
        {
            if (string.IsNullOrWhiteSpace(targetGroup))
                return new List<DependentDocumentRule>();

            var normGroup = targetGroup.Trim().ToUpper();
            var cacheKey = $"DependentRules_Active_{normGroup}";
            var redisKey = $"taxkeep:dependent_rules:active:{normGroup}";

            if (!_cache.TryGetValue(cacheKey, out List<DependentDocumentRule>? activeRules) || activeRules == null)
            {
                if (_redisCache != null)
                {
                    try
                    {
                        activeRules = await _redisCache.GetAsync<List<DependentDocumentRule>>(redisKey);
                    }
                    catch { }
                }

                if (activeRules == null)
                {
                    var repo = _unitOfWork?.Repository<DependentDocumentRule>();
                    var rules = repo != null
                        ? await repo.FindAsync(r => r.TargetGroup == normGroup && r.IsActive)
                        : Enumerable.Empty<DependentDocumentRule>();
                    activeRules = (rules ?? Enumerable.Empty<DependentDocumentRule>()).OrderBy(r => r.CreatedAt).ToList();

                    _cache.Set(cacheKey, activeRules,
                        new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromHours(1)));

                    if (_redisCache != null)
                    {
                        try
                        {
                            await _redisCache.SetAsync(redisKey, activeRules, TimeSpan.FromHours(24));
                        }
                        catch { }
                    }
                }
                else
                {
                    _cache.Set(cacheKey, activeRules,
                        new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromHours(1)));
                }
            }

            return activeRules ?? new List<DependentDocumentRule>();
        }

        /// <summary>
        /// Tính toán danh sách giấy tờ còn thiếu của người phụ thuộc dựa trên quy tắc trong cơ sở dữ liệu.
        /// Hỗ trợ kiểm tra các nhóm giấy tờ có thể thay thế nhau (AlternativeGroup).
        /// Trả về (IsComplete = true/false, Danh sách mã giấy tờ còn thiếu).
        /// Nếu nhóm chưa có quy tắc nào active, trả về (false, [ErrorCodes.Dependent.RulesNotConfigured]).
        /// </summary>
        public async Task<(bool IsComplete, List<string> MissingDocuments)> ComputeMissingDocumentsAsync(string targetGroup, IEnumerable<string> uploadedDocTypes)
        {
            var activeRules = await GetActiveRulesForGroupAsync(targetGroup);
            if (activeRules == null || activeRules.Count == 0)
            {
                return (false, new List<string> { ErrorCodes.Dependent.RulesNotConfigured });
            }

            var uploadedNames = (uploadedDocTypes ?? Enumerable.Empty<string>())
                .Select(NormalizeDocType)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var missing = new List<string>();
            var mandatoryRules = activeRules.Where(r => r.IsMandatory).ToList();

            // 1. Nhóm theo alternative_group (nộp 1 trong số các giấy tờ của alternative_group là đủ)
            var alternativeGroups = mandatoryRules
                .Where(r => !string.IsNullOrWhiteSpace(r.AlternativeGroup))
                .GroupBy(r => r.AlternativeGroup!.Trim(), StringComparer.OrdinalIgnoreCase);

            foreach (var altGroup in alternativeGroups)
            {
                var groupRules = altGroup.OrderBy(r => r.CreatedAt).ToList();
                bool hasUploaded = groupRules.Any(r => uploadedNames.Contains(NormalizeDocType(r.DocType)));
                if (!hasUploaded)
                {
                    // Nếu thiếu, trả ra mã của rule đầu tiên trong nhóm thay thế
                    missing.Add(groupRules[0].DocType);
                }
            }

            // 2. Các rule bắt buộc không thuộc alternative_group nào (độc lập, bắt buộc có đủ)
            var standaloneMandatory = mandatoryRules
                .Where(r => string.IsNullOrWhiteSpace(r.AlternativeGroup));

            foreach (var rule in standaloneMandatory)
            {
                if (!uploadedNames.Contains(NormalizeDocType(rule.DocType)))
                {
                    missing.Add(rule.DocType);
                }
            }

            var distinctMissing = missing.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            return (distinctMissing.Count == 0, distinctMissing);
        }

        #endregion

        #region AI Sync Methods

        // ── Đồng bộ giấy tờ từ TaxAIService sau khi Admin Approve bộ luật ────────
        public async Task<(int added, int updated, int deactivated)> SyncFromAiAsync(IEnumerable<DependentRuleFromAiDto> dependentRules)
        {
            var ruleRepo = _unitOfWork.Repository<DependentDocumentRule>();
            var groupRepo = _unitOfWork.Repository<DependentGroupCatalog>();
            var relRepo = _unitOfWork.Repository<DependentRelationshipCatalog>();

            var allExistingRules = (await ruleRepo.GetAllAsync()).ToList();

            // Map existing rules by their persisted identity.
            var existingMap = new Dictionary<(string TargetGroup, string DocType, string? AlternativeGroup), DependentDocumentRule>();
            foreach (var r in allExistingRules)
            {
                if (!string.IsNullOrWhiteSpace(r.TargetGroup) && !string.IsNullOrWhiteSpace(r.DocType))
                {
                    var k = (r.TargetGroup.Trim().ToUpperInvariant(), NormalizeDocType(r.DocType), NormalizeAlternativeGroup(r.AlternativeGroup));
                    if (!existingMap.ContainsKey(k))
                    {
                        existingMap[k] = r;
                    }
                }
            }

            // Map all existing groups in DB
            var existingGroups = groupRepo != null ? (await groupRepo.GetAllAsync()).ToList() : new List<DependentGroupCatalog>();
            var existingGroupCodes = existingGroups.Select(g => g.Code.Trim().ToUpper()).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var existingRels = relRepo != null ? (await relRepo.GetAllAsync()).ToList() : new List<DependentRelationshipCatalog>();
            var existingRelCodes = existingRels.Select(r => r.Code.Trim().ToUpper()).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Bước 1: Gộp danh sách incoming từ AI
            var incomingMap = new Dictionary<(string TargetGroup, string DocType, string? AlternativeGroup), (bool IsMandatory, string? Description, string? DocName, string? AlternativeGroup, bool HasAlternativeGroup)>();
            var affectedGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ambiguousIncomingDocuments = new HashSet<(string TargetGroup, string DocType)>();
            var incomingDocuments = new List<(string TargetGroup, string DocType, RequiredDocumentFromAiDto Document)>();

            foreach (var depRule in dependentRules)
            {
                if (depRule.RequiredDocuments == null || depRule.RequiredDocuments.Count == 0)
                    continue;

                var rawType = depRule.DependentType?.Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(rawType) || rawType == "DEFAULT")
                {
                    _logger?.LogWarning("Bỏ qua quy tắc người phụ thuộc từ AI có mã loại rỗng hoặc 'DEFAULT': {@Rule}", depRule);
                    continue;
                }

                List<string> targetGroups;

                // 1. Kiểm tra xem rawType có trong bảng dependent_groups chưa
                if (existingGroupCodes.Contains(rawType))
                {
                    targetGroups = new List<string> { rawType };
                }
                // 2. Kiểm tra xem rawType có phải mã cũ của AI (trùng quan hệ hoặc bí danh legacy)
                else if (existingRelCodes.Contains(rawType) || LegacyAiTypeToRelationshipMap.ContainsKey(rawType))
                {
                    var relCode = existingRelCodes.Contains(rawType)
                        ? rawType
                        : LegacyAiTypeToRelationshipMap[rawType];

                    // Lấy các nhóm active có relationship_code = quan hệ đó
                    var relGroups = existingGroups
                        .Where(g => g.IsActive && string.Equals(g.RelationshipCode, relCode, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    // Riêng ADULT_CHILD: loại bỏ nhóm có max_age (nhóm dưới 18 tuổi)
                    if (string.Equals(rawType, "ADULT_CHILD", StringComparison.OrdinalIgnoreCase))
                    {
                        relGroups = relGroups.Where(g => !g.MaxAge.HasValue).ToList();
                    }

                    // Lọc theo cờ của rule AI
                    if (depRule.IsDisabled)
                    {
                        targetGroups = relGroups.Where(g => g.RequireDisabled).Select(g => g.Code).ToList();
                    }
                    else if (depRule.IsStudying)
                    {
                        targetGroups = relGroups.Where(g => g.RequireStudying).Select(g => g.Code).ToList();
                    }
                    else if (depRule.IsRetired)
                    {
                        targetGroups = relGroups.Where(g => !g.RequireDisabled && !g.RequireStudying).Select(g => g.Code).ToList();
                    }
                    else if (depRule.MaxAge.HasValue)
                    {
                        targetGroups = relGroups.Where(g => g.MaxAge == depRule.MaxAge.Value).Select(g => g.Code).ToList();
                    }
                    else
                    {
                        // Không có cờ nào: tất cả nhóm của quan hệ đó
                        targetGroups = relGroups.Select(g => g.Code).ToList();
                    }

                    if (targetGroups.Count == 0)
                    {
                        targetGroups = relGroups.Select(g => g.Code).ToList();
                    }
                }
                // 3. Thực sự là nhóm mới từ AI chưa có trong catalog -> Tạo mới với is_active=false, source="AI"
                else
                {
                    string? matchedRelCode = null;
                    if (!string.IsNullOrWhiteSpace(depRule.Relationship))
                    {
                        var relUpper = depRule.Relationship.Trim().ToUpper();
                        if (existingRelCodes.Contains(relUpper))
                        {
                            matchedRelCode = relUpper;
                        }
                    }

                    if (groupRepo != null && !existingGroupCodes.Contains(rawType))
                    {
                        var newGroup = new DependentGroupCatalog
                        {
                            Code = rawType,
                            Name = !string.IsNullOrWhiteSpace(depRule.Name) ? depRule.Name.Trim() : rawType,
                            RelationshipCode = matchedRelCode,
                            RequireDisabled = depRule.IsDisabled,
                            RequireStudying = depRule.IsStudying,
                            Level = 0,
                            DisplayOrder = 99,
                            IsActive = false, // Chờ Admin kiểm tra và bật
                            Source = "AI",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        await groupRepo.AddAsync(newGroup);
                        existingGroups.Add(newGroup);
                        existingGroupCodes.Add(rawType);
                    }

                    targetGroups = new List<string> { rawType };
                }

                foreach (var targetGroup in targetGroups)
                {
                    if (string.IsNullOrWhiteSpace(targetGroup)) continue;
                    var normGroup = targetGroup.Trim().ToUpper();
                    affectedGroups.Add(normGroup);

                    foreach (var doc in depRule.RequiredDocuments)
                    {
                        if (string.IsNullOrWhiteSpace(doc.DocType)) continue;

                        incomingDocuments.Add((normGroup, NormalizeDocType(doc.DocType), doc));
                    }
                }
            }

            var incomingVariantCounts = incomingDocuments
                .GroupBy(d => (d.TargetGroup, d.DocType))
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .Select(d => d.Document.HasAlternativeGroup
                            ? NormalizeAlternativeGroup(d.Document.AlternativeGroup)
                            : null)
                        .Distinct()
                        .Count());

            foreach (var (targetGroup, docType, doc) in incomingDocuments)
            {
                var existingVariants = existingMap.Keys
                    .Where(k => k.TargetGroup == targetGroup && k.DocType == docType)
                    .ToList();
                var alternativeGroup = doc.HasAlternativeGroup
                    ? NormalizeAlternativeGroup(doc.AlternativeGroup)
                    : null;

                if (!doc.HasAlternativeGroup)
                {
                    if (existingMap.ContainsKey((targetGroup, docType, null)))
                    {
                        alternativeGroup = null;
                    }
                    else if (existingVariants.Count == 1)
                    {
                        alternativeGroup = existingVariants[0].AlternativeGroup;
                    }
                    else if (existingVariants.Count > 1)
                    {
                        _logger?.LogWarning(
                            "Bỏ qua giấy tờ AI {TargetGroup}/{DocType}: dữ liệu không có alternative_group trong khi DB đã có nhiều biến thể.",
                            targetGroup,
                            docType);
                        ambiguousIncomingDocuments.Add((targetGroup, docType));
                        continue;
                    }
                }

                var key = (targetGroup, docType, alternativeGroup);

                if (incomingMap.TryGetValue(key, out var existingIncoming))
                {
                    _logger?.LogWarning(
                        "Cặp ({TargetGroup}, {DocType}, {AlternativeGroup}) xuất hiện nhiều lần trong dữ liệu đồng bộ AI. Lấy giá trị xuất hiện cuối cùng.",
                        targetGroup,
                        docType,
                        alternativeGroup);
                    incomingMap[key] = (
                        IsMandatory: doc.IsMandatory,
                        Description: !string.IsNullOrWhiteSpace(doc.Description) ? doc.Description.Trim() : existingIncoming.Description,
                        DocName: !string.IsNullOrWhiteSpace(doc.Name) ? doc.Name.Trim() : existingIncoming.DocName,
                        AlternativeGroup: doc.HasAlternativeGroup ? doc.AlternativeGroup?.Trim() : existingIncoming.AlternativeGroup,
                        HasAlternativeGroup: doc.HasAlternativeGroup || existingIncoming.HasAlternativeGroup
                    );
                }
                else
                {
                    incomingMap[key] = (
                        IsMandatory: doc.IsMandatory,
                        Description: doc.Description?.Trim(),
                        DocName: doc.Name?.Trim(),
                        AlternativeGroup: doc.AlternativeGroup?.Trim(),
                        HasAlternativeGroup: doc.HasAlternativeGroup
                    );
                }
            }

            int added = 0, updated = 0, deactivated = 0;

            // Bước 2: Upsert các rule từ AI
            foreach (var (key, val) in incomingMap)
            {
                if (!existingMap.TryGetValue(key, out var existingRule))
                {
                    var existingVariants = existingMap
                        .Where(pair => pair.Key.TargetGroup == key.TargetGroup && pair.Key.DocType == key.DocType)
                        .ToList();

                    if (existingVariants.Count == 1 &&
                        incomingVariantCounts.TryGetValue((key.TargetGroup, key.DocType), out var variantCount) &&
                        variantCount == 1)
                    {
                        existingRule = existingVariants[0].Value;
                        existingMap.Remove(existingVariants[0].Key);
                        existingMap[key] = existingRule;
                    }
                }

                if (existingRule != null)
                {
                    existingRule.IsMandatory = val.IsMandatory;
                    if (!string.IsNullOrWhiteSpace(val.Description))
                    {
                        existingRule.Description = val.Description;
                    }
                    if (!string.IsNullOrWhiteSpace(val.DocName))
                    {
                        existingRule.DocName = val.DocName;
                    }
                    if (val.HasAlternativeGroup)
                    {
                        existingRule.AlternativeGroup = NormalizeAlternativeGroup(val.AlternativeGroup);
                    }
                    existingRule.IsActive = true;
                    existingRule.UpdatedAt = DateTime.UtcNow;
                    ruleRepo.Update(existingRule);
                    updated++;
                }
                else
                {
                    var newRule = new DependentDocumentRule
                    {
                        RuleId = Guid.NewGuid(),
                        TargetGroup = key.TargetGroup,
                        DocType = key.DocType,
                        DocName = val.DocName,
                        AlternativeGroup = val.HasAlternativeGroup
                            ? NormalizeAlternativeGroup(val.AlternativeGroup)
                            : null,
                        Source = "AI",
                        IsMandatory = val.IsMandatory,
                        Description = val.Description,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    await ruleRepo.AddAsync(newRule);
                    existingMap[key] = newRule;
                    allExistingRules.Add(newRule);
                    added++;
                }
            }

            // Bước 3: Tắt rule cũ (với mỗi nhóm có trong lần sync, rule source='AI' không còn trong dữ liệu mới thì đặt IsActive = false)
            foreach (var grp in affectedGroups)
            {
                var incomingRuleKeysForGroup = incomingMap.Keys
                    .Where(k => k.TargetGroup.Equals(grp, StringComparison.OrdinalIgnoreCase))
                    .Select(k => (k.DocType, k.AlternativeGroup))
                    .ToHashSet();

                var staleAiRules = allExistingRules.Where(r =>
                    r.TargetGroup.Equals(grp, StringComparison.OrdinalIgnoreCase) &&
                    r.Source == "AI" &&
                    r.IsActive &&
                    !incomingRuleKeysForGroup.Contains((NormalizeDocType(r.DocType), NormalizeAlternativeGroup(r.AlternativeGroup))) &&
                    !ambiguousIncomingDocuments.Contains((grp, NormalizeDocType(r.DocType)))).ToList();

                foreach (var staleRule in staleAiRules)
                {
                    staleRule.IsActive = false;
                    staleRule.UpdatedAt = DateTime.UtcNow;
                    ruleRepo.Update(staleRule);
                    deactivated++;
                }
            }

            if (added > 0 || updated > 0 || deactivated > 0 || (groupRepo != null))
            {
                await _unitOfWork.SaveChangesAsync();

                foreach (var grp in affectedGroups)
                {
                    await InvalidateGroupCacheAsync(grp);
                }
            }

            return (added, updated, deactivated);
        }

        // ── Lấy danh sách TargetGroup đang active từ DB cho endpoint GET /groups ─
        public async Task<IEnumerable<string>> GetActiveGroupsAsync(string? relationshipFilter = null)
        {
            var normRel = !string.IsNullOrWhiteSpace(relationshipFilter) ? relationshipFilter.Trim().ToUpper() : null;
            var cacheKey = normRel == null ? "ActiveDependentGroups" : $"ActiveDependentGroups_{normRel}";
            var redisKey = normRel == null ? "taxkeep:dependent_rules:groups" : $"taxkeep:dependent_rules:groups:{normRel}";

            IEnumerable<string>? groups = null;
            if (_redisCache != null)
            {
                try
                {
                    groups = await _redisCache.GetAsync<List<string>>(redisKey);
                }
                catch { }
            }

            if (groups == null && (!_cache.TryGetValue(cacheKey, out groups) || groups == null))
            {
                var groupRepo = _unitOfWork.Repository<DependentGroupCatalog>();
                var ruleRepo = _unitOfWork.Repository<DependentDocumentRule>();

                List<string> activeGroupCodes;

                if (groupRepo != null)
                {
                    // Lấy các group đang active từ bảng dependent_groups
                    var dbGroups = (await groupRepo.FindAsync(g => g.IsActive)).ToList();
                    if (!string.IsNullOrWhiteSpace(normRel))
                    {
                        dbGroups = dbGroups.Where(g => string.Equals(g.RelationshipCode, normRel, StringComparison.OrdinalIgnoreCase)).ToList();
                    }

                    // Và phải có ít nhất 1 rule active trong dependent_document_rules
                    var activeRules = (await ruleRepo.FindAsync(r => r.IsActive)).ToList();
                    var activeRuleTargetGroups = activeRules
                        .Select(r => r.TargetGroup.Trim().ToUpper())
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    activeGroupCodes = dbGroups
                        .Where(g => activeRuleTargetGroups.Contains(g.Code.Trim().ToUpper()))
                        .OrderBy(g => g.DisplayOrder)
                        .ThenBy(g => g.Code)
                        .Select(g => g.Code)
                        .ToList();
                }
                else
                {
                    // Fallback nếu groupRepo chưa được mock hoặc không tồn tại (cho test cũ)
                    var allActiveRules = await ruleRepo.FindAsync(r => r.IsActive);
                    activeGroupCodes = allActiveRules
                        .Select(r => r.TargetGroup)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(g => g)
                        .ToList();

                    if (!string.IsNullOrWhiteSpace(normRel))
                    {
                        activeGroupCodes = activeGroupCodes.Where(g =>
                            g.StartsWith(normRel + "_", StringComparison.OrdinalIgnoreCase)
                        ).ToList();
                    }
                }

                groups = activeGroupCodes;
                _cache.Set(cacheKey, groups, new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromHours(1)));

                if (_redisCache != null)
                {
                    try
                    {
                        await _redisCache.SetAsync(redisKey, groups, TimeSpan.FromHours(24));
                    }
                    catch { }
                }
            }

            return groups;
        }

        #endregion

        #region Private Helpers

        private static DependentRuleResponseDto MapToDto(DependentDocumentRule r) => new DependentRuleResponseDto
        {
            RuleId = r.RuleId,
            TargetGroup = r.TargetGroup,
            DocType = r.DocType,
            DocName = r.DocName,
            IsMandatory = r.IsMandatory,
            Description = r.Description,
            AlternativeGroup = r.AlternativeGroup,
            Source = r.Source,
            IsActive = r.IsActive,
            CreatedAt = r.CreatedAt,
            UpdatedAt = r.UpdatedAt
        };

        private static IEnumerable<DependentDocumentRule> ApplySort(IEnumerable<DependentDocumentRule> rules, string? sort)
        {
            if (string.IsNullOrWhiteSpace(sort))
                return rules
                    .OrderBy(r => r.TargetGroup)
                    .ThenBy(r => r.DocType)
                    .ThenBy(r => r.AlternativeGroup)
                    .ThenBy(r => r.RuleId);

            bool desc = sort.StartsWith("-");
            string field = sort.TrimStart('-').ToLower();

            IOrderedEnumerable<DependentDocumentRule> ordered = field switch
            {
                "targetgroup" => desc ? rules.OrderByDescending(r => r.TargetGroup) : rules.OrderBy(r => r.TargetGroup),
                "doctype" => desc ? rules.OrderByDescending(r => r.DocType) : rules.OrderBy(r => r.DocType),
                "ismandatory" => desc ? rules.OrderByDescending(r => r.IsMandatory) : rules.OrderBy(r => r.IsMandatory),
                "isactive" => desc ? rules.OrderByDescending(r => r.IsActive) : rules.OrderBy(r => r.IsActive),
                "createdat" => desc ? rules.OrderByDescending(r => r.CreatedAt) : rules.OrderBy(r => r.CreatedAt),
                "updatedat" => desc ? rules.OrderByDescending(r => r.UpdatedAt) : rules.OrderBy(r => r.UpdatedAt),
                _ => desc ? rules.OrderByDescending(r => r.CreatedAt) : rules.OrderBy(r => r.CreatedAt)
            };

            return ordered
                .ThenBy(r => r.TargetGroup)
                .ThenBy(r => r.DocType)
                .ThenBy(r => r.AlternativeGroup)
                .ThenBy(r => r.RuleId);
        }

        private async Task InvalidateGroupCacheAsync(string targetGroup)
        {
            if (string.IsNullOrWhiteSpace(targetGroup)) return;
            var normGroup = targetGroup.Trim().ToUpper();
            _cache.Remove($"DependentRules_{normGroup}");
            _cache.Remove($"DependentRules_Required_{normGroup}");
            _cache.Remove($"DependentRules_Active_{normGroup}");
            _cache.Remove("ActiveDependentGroups");
            _cache.Remove("ActiveDependentGroups_All");
            _cache.Remove("ActiveDependentRelationships");
            _cache.Remove("DependentCatalog");

            if (_redisCache != null)
            {
                await _redisCache.RemoveAsync($"taxkeep:dependent_rules:{normGroup}");
                await _redisCache.RemoveAsync($"taxkeep:dependent_rules:required:{normGroup}");
                await _redisCache.RemoveAsync($"taxkeep:dependent_rules:active:{normGroup}");
                await _redisCache.RemoveAsync("taxkeep:dependent_rules:groups");
                await _redisCache.RemoveAsync("taxkeep:dependents:catalog");
            }

            var relRepo = _unitOfWork?.Repository<DependentRelationshipCatalog>();
            if (relRepo != null)
            {
                try
                {
                    var allRels = await relRepo.GetAllAsync();
                    foreach (var r in allRels)
                    {
                        if (!string.IsNullOrWhiteSpace(r.Code))
                        {
                            var normRel = r.Code.Trim().ToUpper();
                            _cache.Remove($"ActiveDependentGroups_{normRel}");
                            if (_redisCache != null)
                            {
                                await _redisCache.RemoveAsync($"taxkeep:dependent_rules:groups:{normRel}");
                            }
                        }
                    }
                }
                catch { }
            }
        }

        public static string NormalizeDocType(string rawDocType)
        {
            if (string.IsNullOrWhiteSpace(rawDocType))
                return "OTHER";

            var trimmed = rawDocType.Trim().ToUpperInvariant();

            return trimmed switch
            {
                "CT07" or "RESIDENCE_CT07" or "RESIDENCE" or "HO_KHAU" or "HOUSEHOLD" or "HOUSEHOLD_CERTIFICATE" or "XAC_NHAN_CU_TRU" or "GIAY_XAC_NHAN_CU_TRU" => "RESIDENCE_CT07",
                "RELATIONSHIP_CERTIFICATE" => "RELATIONSHIP_CERTIFICATE",
                "CHILD_BIRTH" or "CHILD_BIRTH_CERTIFICATE" => "CHILD_BIRTH_CERTIFICATE",
                "TAXPAYER_BIRTH" or "TAXPAYER_BIRTH_CERTIFICATE" => "TAXPAYER_BIRTH_CERTIFICATE",
                "KHAI_SINH" or "GIAY_KHAI_SINH" or "BIRTH" or "BIRTH_CERTIFICATE" => "BIRTH_CERTIFICATE",
                "CCCD" or "CMND" or "CAN_CUOC" or "CAN_CUOC_CONG_DAN" or "CITIZEN_IDENTIFICATION" or "CITIZEN_ID" => "CITIZEN_ID",
                "TAXPAYER_MARRIAGE" or "TAXPAYER_MARRIAGE_CERTIFICATE" => "TAXPAYER_MARRIAGE_CERTIFICATE",
                "DANG_KY_KET_HON" or "KET_HON" or "GIAY_KET_HON" or "MARRIAGE" or "MARRIAGE_CERTIFICATE" => "MARRIAGE_CERTIFICATE",
                "THE_SINH_VIEN" or "SINH_VIEN" or "STUDENT" or "STUDENT_CARD" => "STUDENT_CARD",
                "KHUYET_TAT" or "GIAY_KHUYET_TAT" or "DISABILITY" or "DISABILITY_CERTIFICATE" => "DISABILITY_CERTIFICATE",
                "CAM_KET" or "GIAY_CAM_KET" or "SUPPORT_COMMITMENT" or "SUPPORT_COMMITMENT_FORM" => "SUPPORT_COMMITMENT_FORM",
                "ADOPTION" or "ADOPTION_CERTIFICATE" or "CON_NUOI" or "NUOI_CON" or "GIAY_CON_NUOI" or "QUYET_DINH_NUOI_CON_NUOI" => "ADOPTION_CERTIFICATE",
                "HUU_TRI" or "SO_HUU_TRI" or "PENSION" or "PENSION_STATEMENT" => "PENSION_STATEMENT",
                "XAC_NHAN_UBND" or "COMMUNE" or "COMMUNE_CERTIFICATION" => "COMMUNE_CERTIFICATION",
                _ => trimmed
            };
        }

        private static string? NormalizeAlternativeGroup(string? alternativeGroup)
        {
            return string.IsNullOrWhiteSpace(alternativeGroup)
                ? null
                : alternativeGroup.Trim().ToUpperInvariant();
        }

        #endregion
    }
}
