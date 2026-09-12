using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Rules;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    public class DependentRuleService : IDependentRuleService
    {
        private readonly IUnitOfWork _unitOfWork;

        public DependentRuleService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
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

            return MapToDto(rule);
        }

        public async Task DeleteAsync(Guid ruleId)
        {
            var repo = _unitOfWork.Repository<DependentDocumentRule>();
            var rule = await repo.GetByIdAsync(ruleId);
            if (rule == null)
                throw new NotFoundException($"Không tìm thấy quy tắc giấy tờ với mã '{ruleId}'.");

            repo.Remove(rule);
            await _unitOfWork.SaveChangesAsync();
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
