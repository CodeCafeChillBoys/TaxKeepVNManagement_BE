using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Rules;
using TaxKeepVN.Application.DTOs.TaxAI;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    public class DependentRuleService : IDependentRuleService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _configuration;

        public DependentRuleService(
            IUnitOfWork unitOfWork,
            IMemoryCache cache,
            IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _cache = cache;
            _configuration = configuration;
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
                    (!string.IsNullOrEmpty(r.Description) && r.Description.ToLower().Contains(kw)));
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

            var repo = _unitOfWork.Repository<DependentDocumentRule>();

            // Check unique constraint (TargetGroup, DocType)
            var existing = await repo.FindAsync(r =>
                r.TargetGroup.ToUpper() == targetGroup &&
                r.DocType.ToUpper() == docType);

            if (existing.Any())
            {
                throw new ConflictException("DUPLICATE_RULE",
                    $"Quy tắc cho nhóm đối tượng '{targetGroup}' và loại giấy tờ '{docType}' đã tồn tại trong hệ thống.");
            }

            var rule = new DependentDocumentRule
            {
                RuleId = Guid.NewGuid(),
                TargetGroup = targetGroup,
                DocType = docType,
                IsMandatory = dto.IsMandatory,
                Description = dto.Description?.Trim(),
                IsActive = dto.IsActive,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await repo.AddAsync(rule);
            await _unitOfWork.SaveChangesAsync();

            // Invalidate cache for this group
            _cache.Remove($"DependentRules_{targetGroup}");

            return MapToDto(rule);
        }

        public async Task<DependentRuleResponseDto> UpdateAsync(Guid ruleId, UpdateDependentRuleDto dto)
        {
            var repo = _unitOfWork.Repository<DependentDocumentRule>();
            var rule = await repo.GetByIdAsync(ruleId);
            if (rule == null)
                throw new NotFoundException($"Không tìm thấy quy tắc giấy tờ với mã '{ruleId}'.");

            rule.IsMandatory = dto.IsMandatory;
            rule.Description = dto.Description?.Trim();
            rule.IsActive = dto.IsActive;
            rule.UpdatedAt = DateTime.UtcNow;

            repo.Update(rule);
            await _unitOfWork.SaveChangesAsync();

            // Invalidate cache for this group
            _cache.Remove($"DependentRules_{rule.TargetGroup}");

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
            _cache.Remove($"DependentRules_{targetGroup}");
        }

        #endregion

        #region AI Sync Methods

        // ── Đồng bộ giấy tờ từ TaxAIService sau khi Admin Approve bộ luật ────────
        // Logic: Với mỗi (TargetGroup, DocType) trong danh sách dependentRules AI trả về:
        //   - Nếu chưa tồn tại trong DB → thêm mới.
        //   - Nếu đã tồn tại → cập nhật IsMandatory, Description.
        // Sau khi sync xong, tất cả cache liên quan bị xóa để dữ liệu hiệu lực ngay lập tức.
        public async Task<(int added, int updated)> SyncFromAiAsync(IEnumerable<DependentRuleFromAiDto> dependentRules)
        {
            var repo = _unitOfWork.Repository<DependentDocumentRule>();
            var allExisting = (await repo.GetAllAsync()).ToList();

            int added = 0, updated = 0;
            var affectedGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var depRule in dependentRules)
            {
                if (depRule.RequiredDocuments == null || depRule.RequiredDocuments.Count == 0)
                    continue;

                // Map dependentType của AI sang TargetGroup enum của .NET
                // Ví dụ: CHILD → CHILD_UNDER_18, ADULT_CHILD → CHILD_OVER_18_STUDYING, v.v.
                var targetGroups = MapAiDependentTypeToTargetGroups(depRule);

                foreach (var targetGroup in targetGroups)
                {
                    affectedGroups.Add(targetGroup);

                    foreach (var doc in depRule.RequiredDocuments)
                    {
                        if (string.IsNullOrWhiteSpace(doc.DocType)) continue;

                        var docType = doc.DocType.Trim().ToUpper();
                        var existing = allExisting.FirstOrDefault(r =>
                            r.TargetGroup.Equals(targetGroup, StringComparison.OrdinalIgnoreCase) &&
                            r.DocType.Equals(docType, StringComparison.OrdinalIgnoreCase));

                        if (existing == null)
                        {
                            // Thêm mới
                            var newRule = new DependentDocumentRule
                            {
                                RuleId    = Guid.NewGuid(),
                                TargetGroup = targetGroup,
                                DocType     = docType,
                                IsMandatory = doc.IsMandatory,
                                Description = doc.Description?.Trim(),
                                IsActive    = true,
                                CreatedAt   = DateTime.UtcNow,
                                UpdatedAt   = DateTime.UtcNow
                            };
                            await repo.AddAsync(newRule);
                            added++;
                        }
                        else
                        {
                            // Cập nhật bản ghi hiện có
                            existing.IsMandatory = doc.IsMandatory;
                            existing.Description = doc.Description?.Trim() ?? existing.Description;
                            existing.IsActive    = true;
                            existing.UpdatedAt   = DateTime.UtcNow;
                            repo.Update(existing);
                            updated++;
                        }
                    }
                }
            }

            if (added > 0 || updated > 0)
            {
                await _unitOfWork.SaveChangesAsync();

                // Xóa toàn bộ cache liên quan
                foreach (var grp in affectedGroups)
                {
                    _cache.Remove($"DependentRules_{grp}");
                    _cache.Remove($"DependentRules_Required_{grp}");
                }
            }

            return (added, updated);
        }

        // ── Lấy danh sách TargetGroup đang active từ DB cho endpoint GET /groups ─
        public async Task<IEnumerable<string>> GetActiveGroupsAsync(string? relationshipFilter = null)
        {
            const string CacheKey = "ActiveDependentGroups";

            if (!_cache.TryGetValue(CacheKey, out IEnumerable<string>? groups) || groups == null)
            {
                var repo = _unitOfWork.Repository<DependentDocumentRule>();
                var allActive = await repo.FindAsync(r => r.IsActive);
                groups = allActive
                    .Select(r => r.TargetGroup)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(g => g)
                    .ToList();

                _cache.Set(CacheKey, groups,
                    new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromHours(1)));
            }

            if (!string.IsNullOrWhiteSpace(relationshipFilter))
            {
                var prefix = relationshipFilter.Trim().ToUpper();
                groups = groups.Where(g => g.StartsWith(prefix + "_",
                    StringComparison.OrdinalIgnoreCase));
            }

            return groups;
        }

        // ── Helper nội bộ: map dependentType của AI → TargetGroup của .NET ────────
        // Đọc hoàn toàn động từ cấu hình TaxAiSettings:TypeMapping trong appsettings.json.
        // Không hardcode loại quan hệ hay nhóm đối tượng.
        private List<string> MapAiDependentTypeToTargetGroups(DependentRuleFromAiDto dep)
        {
            var type = (dep.DependentType ?? "DEFAULT").Trim().ToUpper();
            var section = _configuration.GetSection($"TaxAiSettings:TypeMapping:{type}");
            if (!section.Exists())
            {
                section = _configuration.GetSection("TaxAiSettings:TypeMapping:DEFAULT");
            }

            if (!section.Exists())
            {
                return new List<string> { "OTHER_HELPLESS" };
            }

            List<string>? result = null;

            if (dep.IsDisabled)
            {
                result = ReadStringList(section, "IfDisabled");
            }
            else if (dep.IsStudying)
            {
                result = ReadStringList(section, "IfStudying");
            }

            result ??= ReadStringList(section, "Default");

            return result != null && result.Count > 0
                ? result
                : new List<string> { "OTHER_HELPLESS" };
        }

        private static List<string>? ReadStringList(IConfigurationSection parentSection, string key)
        {
            var sec = parentSection.GetSection(key);
            if (!sec.Exists()) return null;
            var list = sec.GetChildren()
                .Select(c => c.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!)
                .ToList();
            return list.Count > 0 ? list : null;
        }

        #endregion

        #region Private Helpers

        private static DependentRuleResponseDto MapToDto(DependentDocumentRule r) => new DependentRuleResponseDto
        {
            RuleId = r.RuleId,
            TargetGroup = r.TargetGroup,
            DocType = r.DocType,
            IsMandatory = r.IsMandatory,
            Description = r.Description,
            IsActive = r.IsActive,
            CreatedAt = r.CreatedAt,
            UpdatedAt = r.UpdatedAt
        };

        private static IEnumerable<DependentDocumentRule> ApplySort(IEnumerable<DependentDocumentRule> rules, string? sort)
        {
            if (string.IsNullOrWhiteSpace(sort))
                return rules.OrderBy(r => r.TargetGroup).ThenBy(r => r.DocType);

            bool desc = sort.StartsWith("-");
            string field = sort.TrimStart('-').ToLower();

            return field switch
            {
                "targetgroup" => desc ? rules.OrderByDescending(r => r.TargetGroup) : rules.OrderBy(r => r.TargetGroup),
                "doctype" => desc ? rules.OrderByDescending(r => r.DocType) : rules.OrderBy(r => r.DocType),
                "ismandatory" => desc ? rules.OrderByDescending(r => r.IsMandatory) : rules.OrderBy(r => r.IsMandatory),
                "isactive" => desc ? rules.OrderByDescending(r => r.IsActive) : rules.OrderBy(r => r.IsActive),
                "createdat" => desc ? rules.OrderByDescending(r => r.CreatedAt) : rules.OrderBy(r => r.CreatedAt),
                "updatedat" => desc ? rules.OrderByDescending(r => r.UpdatedAt) : rules.OrderBy(r => r.UpdatedAt),
                _ => desc ? rules.OrderByDescending(r => r.CreatedAt) : rules.OrderBy(r => r.CreatedAt)
            };
        }

        #endregion
    }
}
