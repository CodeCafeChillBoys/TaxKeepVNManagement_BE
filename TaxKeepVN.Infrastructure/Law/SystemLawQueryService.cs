using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TaxKeepVN.Domain.Constants;
using TaxKeepVN.Application.DTOs.Law;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Law.Common;
using TaxKeepVN.Application.Law.Normalizer;
using TaxKeepVN.Application.Law.Query;
using TaxKeepVN.Application.Law.Selector;
using TaxKeepVN.Domain.Entities.Law;
using TaxKeepVN.Infrastructure.Contexts;

namespace TaxKeepVN.Infrastructure.Law
{
    public class SystemLawQueryService : ISystemLawQueryService
    {
        private readonly TaxKeepDbContext _dbContext;
        private readonly IMemoryCache _cache;
        private static readonly Regex RuleCodeRegex = new(@"^PIT_[A-Z0-9_]+$", RegexOptions.Compiled);

        private static readonly HashSet<string> ValidRuleGroups = new(StringComparer.OrdinalIgnoreCase)
        {
            LawConstants.RuleGroup.SCHEDULE,
            LawConstants.RuleGroup.DEDUCTION,
            LawConstants.RuleGroup.DEPENDENT,
            LawConstants.RuleGroup.SETTLEMENT,
            LawConstants.RuleGroup.WITHHOLDING,
            LawConstants.RuleGroup.RATE,
            LawConstants.RuleGroup.EXEMPTION
        };

        private static readonly HashSet<string> ValidValueKinds = new(StringComparer.OrdinalIgnoreCase)
        {
            LawConstants.ValueKind.AMOUNT,
            LawConstants.ValueKind.RATE,
            LawConstants.ValueKind.SCHEDULE,
            LawConstants.ValueKind.JSON,
            LawConstants.ValueKind.FLAG,
            LawConstants.ValueKind.TEXT
        };

        public SystemLawQueryService(TaxKeepDbContext dbContext, IMemoryCache cache)
        {
            _dbContext = dbContext;
            _cache = cache;
        }

        private int GetMetaVersion()
        {
            return _cache.GetOrCreate("law:meta-version", entry => 1);
        }

        public void IncrementMetaVersion()
        {
            int current = GetMetaVersion();
            _cache.Set("law:meta-version", current + 1);
        }

        public void InvalidateCache()
        {
            _cache.Remove("law:head");
            IncrementMetaVersion();
        }

        public async Task<int> GetHeadRevisionAsync(CancellationToken ct = default)
        {
            if (_cache.TryGetValue("law:head", out int head))
            {
                return head;
            }

            head = await _dbContext.LawRevisions.MaxAsync(r => (int?)r.RevisionNo, ct) ?? 0;
            _cache.Set("law:head", head, TimeSpan.FromMinutes(10));
            return head;
        }

        public async Task<EffectiveLawDto> GetEffectiveAsync(int taxYear, int? asOfRevision = null, CancellationToken ct = default)
        {
            int head = await GetHeadRevisionAsync(ct);
            int revisionNo = (asOfRevision.HasValue && asOfRevision.Value >= 0) ? asOfRevision.Value : head;
            int metaVersion = GetMetaVersion();
            string cacheKey = $"law:effective:{revisionNo}:{taxYear}:{metaVersion}";

            if (_cache.TryGetValue(cacheKey, out EffectiveLawDto? cached) && cached != null)
            {
                return cached;
            }

            // Retrieve active versions at revisionNo
            var activeVersions = await _dbContext.LawRuleVersions
                .AsNoTracking()
                .Include(v => v.Document)
                .Where(v => v.CreatedInRevision <= revisionNo && (!v.SupersededInRevision.HasValue || v.SupersededInRevision.Value > revisionNo))
                .ToListAsync(ct);

            var definitions = await _dbContext.LawRuleDefinitions
                .AsNoTracking()
                .ToListAsync(ct);

            var defMap = definitions.ToDictionary(d => d.RuleCode, StringComparer.OrdinalIgnoreCase);

            var selector = new EffectiveLawSelector();
            var selection = selector.SelectForTaxYear(taxYear, activeVersions, definitions);

            var dto = new EffectiveLawDto
            {
                TaxYear = taxYear,
                RevisionNo = revisionNo,
                MissingRequired = selection.MissingRequiredRules
            };

            // Map rules
            foreach (var kvp in selection.EffectiveRules)
            {
                var v = kvp.Value;
                defMap.TryGetValue(v.RuleCode, out var def);
                var snap = RuleValueSnapshot.FromVersion(v);

                dto.Rules.Add(new EffectiveRuleItemDto
                {
                    RuleCode = v.RuleCode,
                    DisplayName = def?.DisplayName ?? v.RuleCode,
                    RuleGroup = def?.RuleGroup ?? LawConstants.RuleGroup.SCHEDULE,
                    ValueKind = def?.ValueKind ?? LawConstants.ValueKind.AMOUNT,
                    ValueNumber = snap.ValueNumber,
                    ValueJson = snap.ValueJson,
                    ValueText = snap.ValueText,
                    Unit = snap.Unit,
                    Condition = snap.Condition,
                    ConditionText = snap.ConditionText,
                    ApplyFrom = v.ApplyFrom,
                    ApplyTo = v.ApplyTo,
                    VersionId = v.Id,
                    CreatedInRevision = v.CreatedInRevision,
                    Citation = new EffectiveCitationDto
                    {
                        DocumentId = v.DocumentId ?? Guid.Empty,
                        DocumentNumber = v.Document?.DocumentNumber ?? string.Empty,
                        Article = v.Article,
                        Clause = v.Clause,
                        Point = v.Point,
                        Page = v.Page
                    }
                });
            }

            // Map distinct documents cited
            var citedDocs = selection.EffectiveRules.Values
                .Select(v => v.Document)
                .Where(d => d != null)
                .GroupBy(d => d!.Id)
                .Select(g => g.First()!)
                .ToList();

            foreach (var doc in citedDocs)
            {
                dto.Documents.Add(new EffectiveDocumentDto
                {
                    Id = doc.Id,
                    DocumentNumber = doc.DocumentNumber ?? string.Empty,
                    DocumentType = doc.DocumentType,
                    Title = doc.Title ?? string.Empty,
                    IssuedDate = doc.IssuedDate,
                    EffectiveDate = doc.EffectiveDate,
                    SourceUrl = doc.SourceUrl,
                    FileUrl = doc.FileUrl,
                    LegalStatus = doc.LegalStatus
                });
            }

            // Map warnings
            foreach (var mid in selection.MidYearChanges)
            {
                dto.Warnings.Add(new EffectiveWarningDto
                {
                    Code = "MID_YEAR_CHANGE",
                    RuleCode = mid.RuleCode,
                    Message = $"Mã {mid.RuleCode} có thay đổi áp dụng giữa năm tính thuế {taxYear}.",
                    VersionIds = mid.Segments.Select(s => s.Id).ToList()
                });
            }

            _cache.Set(cacheKey, dto, TimeSpan.FromHours(1));
            return dto;
        }

        public async Task<RuleHistoryDto> GetRuleHistoryAsync(string ruleCode, CancellationToken ct = default)
        {
            var def = await _dbContext.LawRuleDefinitions
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.RuleCode.ToLower() == ruleCode.ToLower(), ct);

            if (def == null)
            {
                throw new NotFoundException($"Không tìm thấy mã quy tắc {ruleCode}");
            }

            var versions = await _dbContext.LawRuleVersions
                .AsNoTracking()
                .Include(v => v.Document)
                .Where(v => v.RuleCode.ToLower() == ruleCode.ToLower())
                .OrderBy(v => v.ApplyFrom)
                .ThenBy(v => v.CreatedInRevision)
                .ToListAsync(ct);

            var dto = new RuleHistoryDto
            {
                RuleCode = def.RuleCode,
                DisplayName = def.DisplayName,
                ValueKind = def.ValueKind,
                Versions = versions.Select(v =>
                {
                    var snap = RuleValueSnapshot.FromVersion(v);
                    return new RuleHistoryItemDto
                    {
                        Id = v.Id,
                        ValueNumber = snap.ValueNumber,
                        ValueJson = snap.ValueJson,
                        ValueText = snap.ValueText,
                        Unit = snap.Unit,
                        Condition = snap.Condition,
                        ConditionText = snap.ConditionText,
                        ApplyFrom = v.ApplyFrom,
                        ApplyTo = v.ApplyTo,
                        CreatedInRevision = v.CreatedInRevision,
                        SupersededInRevision = v.SupersededInRevision,
                        DerivedFromVersionId = v.DerivedFromVersionId,
                        SourceOpId = v.SourceOpId,
                        Citation = new EffectiveCitationDto
                        {
                            DocumentId = v.DocumentId ?? Guid.Empty,
                            DocumentNumber = v.Document?.DocumentNumber ?? string.Empty,
                            Article = v.Article,
                            Clause = v.Clause,
                            Point = v.Point,
                            Page = v.Page
                        }
                    };
                }).ToList()
            };

            return dto;
        }

        public async Task<List<RevisionDto>> GetRevisionsAsync(int page = 1, int size = 20, CancellationToken ct = default)
        {
            if (page < 1) page = 1;
            if (size < 1) size = 20;

            var revisions = await _dbContext.LawRevisions
                .AsNoTracking()
                .Include(r => r.Document)
                .OrderByDescending(r => r.RevisionNo)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync(ct);

            return revisions.Select(r => new RevisionDto
            {
                RevisionNo = r.RevisionNo,
                ParentRevisionNo = r.RevisionNo > 1 ? r.RevisionNo - 1 : null,
                Message = r.Description,
                Document = r.Document == null ? null : new RevisionDocumentDto
                {
                    Id = r.Document.Id,
                    DocumentNumber = r.Document.DocumentNumber ?? string.Empty,
                    Title = r.Document.Title
                },
                Summary = r.Description,
                MergedBy = r.CommittedBy,
                MergedAt = r.CommittedAt
            }).ToList();
        }

        public async Task<RevisionDetailDto> GetRevisionDetailAsync(int revisionNo, CancellationToken ct = default)
        {
            var rev = await _dbContext.LawRevisions
                .AsNoTracking()
                .Include(r => r.Document)
                .FirstOrDefaultAsync(r => r.RevisionNo == revisionNo, ct);

            if (rev == null)
            {
                throw new NotFoundException($"Không tìm thấy revision số {revisionNo}");
            }

            var affectedVersions = await _dbContext.LawRuleVersions
                .AsNoTracking()
                .Include(v => v.Document)
                .Where(v => v.CreatedInRevision == revisionNo || v.SupersededInRevision == revisionNo)
                .ToListAsync(ct);

            var definitions = await _dbContext.LawRuleDefinitions
                .AsNoTracking()
                .ToListAsync(ct);
            var defMap = definitions.ToDictionary(d => d.RuleCode, StringComparer.OrdinalIgnoreCase);

            var dto = new RevisionDetailDto
            {
                RevisionNo = rev.RevisionNo,
                ParentRevisionNo = rev.RevisionNo > 1 ? rev.RevisionNo - 1 : null,
                Message = rev.Description,
                Document = rev.Document == null ? null : new RevisionDocumentDto
                {
                    Id = rev.Document.Id,
                    DocumentNumber = rev.Document.DocumentNumber ?? string.Empty,
                    Title = rev.Document.Title
                },
                Summary = rev.Description,
                MergedBy = rev.CommittedBy,
                MergedAt = rev.CommittedAt
            };

            var grouped = affectedVersions.GroupBy(v => v.RuleCode, StringComparer.OrdinalIgnoreCase);

            foreach (var g in grouped)
            {
                string ruleCode = g.Key;
                defMap.TryGetValue(ruleCode, out var def);
                var created = g.Where(v => v.CreatedInRevision == revisionNo).ToList();
                var superseded = g.Where(v => v.SupersededInRevision == revisionNo).ToList();

                string opType;
                RuleValueSnapshot? before = null;
                RuleValueSnapshot? after = null;

                if (created.Count > 0 && superseded.Count == 0)
                {
                    opType = LawConstants.OpType.ADD;
                    after = RuleValueSnapshot.FromVersion(created.First());
                }
                else if (created.Count > 0 && superseded.Count > 0)
                {
                    var c = created.First();
                    var s = superseded.First();
                    before = RuleValueSnapshot.FromVersion(s);
                    after = RuleValueSnapshot.FromVersion(c);

                    string valKind = def?.ValueKind ?? LawConstants.ValueKind.AMOUNT;
                    bool sameVal = LawValueComparer.AreValuesEqual(s.RuleValue, c.RuleValue, valKind);
                    opType = sameVal ? LawConstants.OpType.RECITE : LawConstants.OpType.UPDATE;
                }
                else
                {
                    opType = LawConstants.OpType.END;
                    before = RuleValueSnapshot.FromVersion(superseded.First());
                }

                dto.Changes.Add(new RevisionChangeDto
                {
                    RuleCode = ruleCode,
                    DisplayName = def?.DisplayName ?? ruleCode,
                    OpType = opType,
                    Before = before,
                    After = after
                });
            }

            return dto;
        }

        public async Task<List<LawRuleDefinitionDto>> GetRuleDefinitionsAsync(bool? activeOnly = null, CancellationToken ct = default)
        {
            var query = _dbContext.LawRuleDefinitions.AsNoTracking().AsQueryable();

            if (activeOnly == true)
            {
                query = query.Where(d => d.IsActive);
            }

            var definitions = await query
                .OrderBy(d => d.RuleGroup)
                .ThenBy(d => d.RuleCode)
                .ToListAsync(ct);

            var ruleCodesWithVersions = await _dbContext.LawRuleVersions
                .Select(v => v.RuleCode)
                .Distinct()
                .ToListAsync(ct);

            var usedSet = new HashSet<string>(ruleCodesWithVersions, StringComparer.OrdinalIgnoreCase);

            return definitions.Select(d => new LawRuleDefinitionDto
            {
                RuleCode = d.RuleCode,
                RuleGroup = d.RuleGroup,
                ValueKind = d.ValueKind,
                DefaultUnit = d.DefaultUnit,
                DisplayName = d.DisplayName,
                Description = d.Description,
                RequiredForFlow3 = d.RequiredForFlow3,
                RequiredFromTaxYear = d.RequiredFromTaxYear,
                IsActive = d.IsActive,
                HasVersions = usedSet.Contains(d.RuleCode)
            }).ToList();
        }

        public async Task<LawRuleDefinitionDto> CreateRuleDefinitionAsync(LawRuleDefinitionInput input, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(input.RuleCode) || !RuleCodeRegex.IsMatch(input.RuleCode))
            {
                throw new BadRequestException("E-LAW_INVALID_RULE_CODE", "RuleCode phải có tiền tố PIT_ và viết hoa dạng SNAKE_CASE (ví dụ: PIT_EXAMPLE_RULE).");
            }

            if (!ValidRuleGroups.Contains(input.RuleGroup))
            {
                throw new BadRequestException("E-LAW_INVALID_RULE_GROUP", $"RuleGroup không hợp lệ. Cho phép: {string.Join(", ", ValidRuleGroups)}");
            }

            if (!ValidValueKinds.Contains(input.ValueKind))
            {
                throw new BadRequestException("E-LAW_INVALID_VALUE_KIND", $"ValueKind không hợp lệ. Cho phép: {string.Join(", ", ValidValueKinds)}");
            }

            bool exists = await _dbContext.LawRuleDefinitions.AnyAsync(d => d.RuleCode.ToLower() == input.RuleCode.ToLower(), ct);
            if (exists)
            {
                throw new ConflictException("E-LAW_RULE_EXISTS", $"Mã quy tắc {input.RuleCode} đã tồn tại trong danh mục.");
            }

            var entity = new LawRuleDefinition
            {
                RuleCode = input.RuleCode.ToUpperInvariant(),
                RuleGroup = input.RuleGroup,
                ValueKind = input.ValueKind,
                DefaultUnit = input.DefaultUnit,
                DisplayName = input.DisplayName ?? string.Empty,
                Description = input.Description,
                RequiredForFlow3 = input.RequiredForFlow3,
                RequiredFromTaxYear = input.RequiredFromTaxYear,
                IsActive = input.IsActive
            };

            _dbContext.LawRuleDefinitions.Add(entity);
            await _dbContext.SaveChangesAsync(ct);
            IncrementMetaVersion();

            return new LawRuleDefinitionDto
            {
                RuleCode = entity.RuleCode,
                RuleGroup = entity.RuleGroup,
                ValueKind = entity.ValueKind,
                DefaultUnit = entity.DefaultUnit,
                DisplayName = entity.DisplayName,
                Description = entity.Description,
                RequiredForFlow3 = entity.RequiredForFlow3,
                RequiredFromTaxYear = entity.RequiredFromTaxYear,
                IsActive = entity.IsActive,
                HasVersions = false
            };
        }

        public async Task<LawRuleDefinitionDto> UpdateRuleDefinitionAsync(string ruleCode, LawRuleDefinitionUpdateInput input, CancellationToken ct = default)
        {
            var entity = await _dbContext.LawRuleDefinitions.FirstOrDefaultAsync(d => d.RuleCode.ToLower() == ruleCode.ToLower(), ct);
            if (entity == null)
            {
                throw new NotFoundException($"Không tìm thấy mã quy tắc {ruleCode}");
            }

            entity.DisplayName = input.DisplayName ?? string.Empty;
            entity.Description = input.Description;
            entity.DefaultUnit = input.DefaultUnit;
            entity.RequiredForFlow3 = input.RequiredForFlow3;
            entity.RequiredFromTaxYear = input.RequiredFromTaxYear;
            entity.IsActive = input.IsActive;

            await _dbContext.SaveChangesAsync(ct);
            IncrementMetaVersion();

            bool hasVersions = await _dbContext.LawRuleVersions.AnyAsync(v => v.RuleCode.ToLower() == ruleCode.ToLower(), ct);

            return new LawRuleDefinitionDto
            {
                RuleCode = entity.RuleCode,
                RuleGroup = entity.RuleGroup,
                ValueKind = entity.ValueKind,
                DefaultUnit = entity.DefaultUnit,
                DisplayName = entity.DisplayName,
                Description = entity.Description,
                RequiredForFlow3 = entity.RequiredForFlow3,
                RequiredFromTaxYear = entity.RequiredFromTaxYear,
                IsActive = entity.IsActive,
                HasVersions = hasVersions
            };
        }
    }
}
