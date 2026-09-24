using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TaxKeepVN.Application.DTOs.Law;
using TaxKeepVN.Application.DTOs.Law.Contract;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Law.Changeset;
using TaxKeepVN.Application.Law.Common;
using TaxKeepVN.Application.Law.Merge;
using TaxKeepVN.Application.Law.Normalizer;
using TaxKeepVN.Application.Law.Query;
using TaxKeepVN.Domain.Constants;
using TaxKeepVN.Domain.Entities.Law;
using TaxKeepVN.Infrastructure.Contexts;

namespace TaxKeepVN.Infrastructure.Law
{
    public class LawChangesetService : ILawChangesetService
    {
        private readonly TaxKeepDbContext _dbContext;
        private readonly ISystemLawQueryService _queryService;
        private readonly ILawMergeExecutor _mergeExecutor;
        private readonly ILogger<LawChangesetService> _logger;

        public LawChangesetService(
            TaxKeepDbContext dbContext,
            ISystemLawQueryService queryService,
            ILawMergeExecutor mergeExecutor,
            ILogger<LawChangesetService> logger)
        {
            _dbContext = dbContext;
            _queryService = queryService;
            _mergeExecutor = mergeExecutor;
            _logger = logger;
        }

        public async Task<List<ChangesetListItemDto>> GetChangesetsAsync(string? status, int page = 1, int size = 20, CancellationToken ct = default)
        {
            if (page < 1) page = 1;
            if (size < 1) size = 20;

            var query = _dbContext.LawChangesets
                .AsNoTracking()
                .Include(c => c.Document)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(c => c.Status.ToUpper() == status.ToUpper());
            }

            var list = await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync(ct);

            return list.Select(c => new ChangesetListItemDto
            {
                Id = c.Id,
                Status = c.Status,
                Origin = c.Origin,
                Reason = c.Reason,
                BaseRevisionNo = c.BaseRevisionNo,
                MergedRevisionNo = c.MergedRevisionNo,
                DocumentId = c.DocumentId,
                DocumentNumber = c.Document?.DocumentNumber,
                CreatedAt = c.CreatedAt,
                CreatedBy = c.CreatedBy?.ToString() ?? string.Empty
            }).ToList();
        }

        public async Task<ChangesetDetailDto?> GetOpenChangesetAsync(CancellationToken ct = default)
        {
            var open = await _dbContext.LawChangesets
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Status == LawConstants.ChangesetStatus.EXTRACTING
                                       || c.Status == LawConstants.ChangesetStatus.READY
                                       || c.Status == LawConstants.ChangesetStatus.STALE, ct);

            if (open == null)
                return null;

            return await GetChangesetDetailAsync(open.Id, ct);
        }

        public async Task<ChangesetDetailDto> GetChangesetDetailAsync(Guid id, CancellationToken ct = default)
        {
            var changeset = await _dbContext.LawChangesets
                .AsNoTracking()
                .Include(c => c.Document)
                .Include(c => c.Ops)
                .Include(c => c.Relations)
                .Include(c => c.OrphanResolutions)
                .FirstOrDefaultAsync(c => c.Id == id, ct);

            if (changeset == null)
            {
                throw new NotFoundException($"Không tìm thấy bản đề xuất {id}");
            }

            int head = await _queryService.GetHeadRevisionAsync(ct);

            // Active versions at base revision
            var activeVersions = await _dbContext.LawRuleVersions
                .AsNoTracking()
                .Include(v => v.Document)
                .Where(v => v.CreatedInRevision <= changeset.BaseRevisionNo && (!v.SupersededInRevision.HasValue || v.SupersededInRevision.Value > changeset.BaseRevisionNo))
                .ToListAsync(ct);

            var catalog = await _dbContext.LawRuleDefinitions.AsNoTracking().ToListAsync(ct);
            var catalogMap = catalog.ToDictionary(c => c.RuleCode, StringComparer.OrdinalIgnoreCase);

            var documents = await _dbContext.LegalDocuments
                .AsNoTracking()
                .Include(d => d.SourceRelations)
                .ToListAsync(ct);

            var relations = await _dbContext.LegalDocumentRelations
                .AsNoTracking()
                .ToListAsync(ct);

            // Trial planner execution to produce check results and orphan detection
            var planner = new LawMergePlanner();
            var input = new MergeInput
            {
                HeadRevisionNo = head,
                ActiveVersions = activeVersions,
                AcceptedOps = changeset.Ops.Where(o => o.Decision == LawConstants.Decision.ACCEPTED).ToList(),
                AcceptedRelations = changeset.Relations.Where(r => r.Decision == LawConstants.Decision.ACCEPTED).ToList(),
                AllOps = changeset.Ops.ToList(),
                AllRelations = changeset.Relations.ToList(),
                OrphanResolutions = changeset.OrphanResolutions.ToList(),
                Catalog = catalog,
                Documents = documents,
                MergedDocumentRelations = relations,
                Changeset = changeset,
                Today = DateOnly.FromDateTime(DateTime.UtcNow)
            };

            var plan = planner.Plan(input);

            var detail = new ChangesetDetailDto
            {
                Id = changeset.Id,
                Status = changeset.Status,
                Origin = changeset.Origin,
                Reason = changeset.Reason,
                CreatedAt = changeset.CreatedAt,
                CreatedBy = changeset.CreatedBy?.ToString() ?? string.Empty,
                BaseRevisionNo = changeset.BaseRevisionNo,
                HeadRevisionNo = head,
                IsStale = changeset.BaseRevisionNo != head,
                AiErrorCode = changeset.AiErrorCode,
                AiErrorMessage = changeset.AiErrorMessage,
                Coverage = new CoverageDto
                {
                    PagesRead = changeset.PagesRead,
                    TotalPages = changeset.TotalPages
                }
            };

            // Parse AI warnings
            if (!string.IsNullOrWhiteSpace(changeset.AiWarnings))
            {
                try
                {
                    detail.AiWarnings = JsonSerializer.Deserialize<List<string>>(changeset.AiWarnings) ?? new();
                }
                catch
                {
                    detail.AiWarnings = new() { changeset.AiWarnings };
                }
            }

            // Map document
            if (changeset.Document != null)
            {
                var d = changeset.Document;
                detail.Document = new LegalDocumentDto
                {
                    Id = d.Id,
                    DocumentNumber = d.DocumentNumber ?? string.Empty,
                    DocumentType = d.DocumentType,
                    Title = d.Title ?? string.Empty,
                    Issuer = d.Issuer,
                    IssuedDate = d.IssuedDate,
                    EffectiveDate = d.EffectiveDate,
                    SourceUrl = d.SourceUrl,
                    FileUrl = d.FileUrl,
                    LegalStatus = d.LegalStatus,
                    IsPlaceholder = d.IsPlaceholder
                };
            }

            // Map Ops
            foreach (var op in changeset.Ops.OrderBy(o => o.Seq))
            {
                catalogMap.TryGetValue(op.RuleCode, out var def);
                var flags = ParseFlags(op.Flags);

                detail.Ops.Add(new OpDto
                {
                    Id = op.Id,
                    Seq = op.Seq,
                    OpType = op.OpType,
                    RuleCode = op.RuleCode,
                    RuleDisplayName = def?.DisplayName ?? op.RuleCode,
                    NewCode = op.NewCode,
                    ProposedDefinition = ParseJson(op.ProposedDefinition),
                    Before = ParseJson(op.Before),
                    After = ParseJson(op.After),
                    ApplyFrom = op.ApplyFrom,
                    ApplyTo = op.ApplyTo,
                    ApplyBasis = ParseCitation(op.ApplyBasis),
                    Citation = new CitationDto
                    {
                        Article = op.Article,
                        Clause = op.Clause,
                        Point = op.Point,
                        Page = op.Page
                    },
                    Evidence = op.EvidenceText,
                    Confidence = (double?)op.Confidence,
                    Rationale = op.Rationale,
                    Origin = op.Origin,
                    Decision = op.Decision,
                    EditedByAdmin = op.EditedByAdmin,
                    AdminNote = op.AdminNote,
                    ConflictState = op.ConflictState,
                    Flags = flags
                });
            }

            // Map Relations
            foreach (var rel in changeset.Relations)
            {
                detail.Relations.Add(new RelationItemDto
                {
                    Id = rel.Id,
                    RelationType = rel.RelationType,
                    TargetDocumentNumber = rel.TargetDocumentNumber,
                    TargetArticle = rel.TargetArticle,
                    TargetClause = rel.TargetClause,
                    TargetPoint = rel.TargetPoint,
                    EffectiveDate = rel.EffectiveDate,
                    Evidence = rel.EvidenceText,
                    Note = rel.Note,
                    Origin = rel.Origin,
                    Decision = rel.Decision
                });
            }

            // Map Orphans
            var orphanMap = changeset.OrphanResolutions.ToDictionary(r => r.VersionId);
            foreach (var orph in plan.Orphans)
            {
                orphanMap.TryGetValue(orph.VersionId, out var res);
                catalogMap.TryGetValue(orph.RuleCode, out var def);

                var v = activeVersions.FirstOrDefault(ver => ver.Id == orph.VersionId);
                string? valSummary = null;
                if (v != null)
                {
                    var snap = RuleValueSnapshot.FromVersion(v);
                    valSummary = snap.ValueNumber?.ToString() ?? snap.ValueText;
                }

                detail.Orphans.Add(new OrphanDto
                {
                    VersionId = orph.VersionId,
                    RuleCode = orph.RuleCode,
                    RuleDisplayName = def?.DisplayName ?? orph.RuleCode,
                    ValueSummary = valSummary,
                    Citation = new CitationDto
                    {
                        DocumentNumber = orph.DocumentNumber,
                        Article = orph.Article,
                        Clause = orph.Clause,
                        Point = orph.Point
                    },
                    ApplyFrom = orph.ApplyFrom,
                    ApplyTo = orph.ApplyTo,
                    RelationType = LawConstants.RelationType.REPLACES,
                    TargetDocumentNumber = orph.DocumentNumber ?? string.Empty,
                    SuggestedEffectiveDate = orph.ApplyTo,
                    Resolution = res?.Action
                });
            }

            // Map Check
            detail.Check = new CheckResultDto
            {
                CanMerge = plan.CanMerge,
                HeadRevisionNo = head,
                BaseRevisionNo = changeset.BaseRevisionNo,
                Checks = plan.Checks.Select(c => new CheckItemDto
                {
                    Code = c.Code,
                    Level = c.Level.ToString(),
                    Passed = c.Passed,
                    Message = c.Message,
                    Items = c.Items
                }).ToList(),
                Preview = new CheckPreviewDto
                {
                    Added = plan.Summary.AddOpsCount,
                    Updated = plan.Summary.UpdateOpsCount,
                    Ended = plan.Summary.EndOpsCount,
                    Recited = plan.Summary.ReciteOpsCount,
                    Relations = plan.Summary.RelationsCount,
                    NewVersionIds = plan.NewVersions.Select(nv => nv.Id).ToList(),
                    SupersededVersionIds = plan.SupersededVersionIds
                }
            };

            // Counts
            detail.Counts = new ChangesetCountsDto
            {
                Pending = changeset.Ops.Count(o => o.Decision == LawConstants.Decision.PENDING)
                        + changeset.Relations.Count(r => r.Decision == LawConstants.Decision.PENDING),
                Accepted = changeset.Ops.Count(o => o.Decision == LawConstants.Decision.ACCEPTED)
                         + changeset.Relations.Count(r => r.Decision == LawConstants.Decision.ACCEPTED),
                Rejected = changeset.Ops.Count(o => o.Decision == LawConstants.Decision.REJECTED)
                         + changeset.Relations.Count(r => r.Decision == LawConstants.Decision.REJECTED)
            };

            return detail;
        }

        public async Task<ChangesetDetailDto> CreateManualChangesetAsync(ManualChangesetInput input, Guid adminId, CancellationToken ct = default)
        {
            await AssertNoOpenChangesetAsync(ct);

            int head = await _queryService.GetHeadRevisionAsync(ct);

            var changeset = new LawChangeset
            {
                Id = Guid.NewGuid(),
                DocumentId = input.DocumentId,
                BaseRevisionNo = head,
                Status = LawConstants.ChangesetStatus.READY,
                Origin = LawConstants.ChangesetOrigin.MANUAL,
                Reason = input.Reason,
                CreatedBy = adminId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            int seq = 1;
            foreach (var opInput in input.Operations)
            {
                changeset.Ops.Add(new LawChangeOp
                {
                    Id = Guid.NewGuid(),
                    ChangesetId = changeset.Id,
                    Seq = seq++,
                    OpType = opInput.Op.ToUpperInvariant(),
                    RuleCode = opInput.RuleCode.ToUpperInvariant(),
                    After = opInput.After?.GetRawText(),
                    ApplyFrom = opInput.ApplyFrom,
                    ApplyTo = opInput.ApplyTo,
                    ApplyBasis = opInput.ApplyBasis == null ? null : JsonSerializer.Serialize(opInput.ApplyBasis),
                    Article = opInput.Article,
                    Clause = opInput.Clause,
                    Point = opInput.Point,
                    Page = opInput.Page,
                    EvidenceText = opInput.Evidence,
                    AdminNote = opInput.AdminNote,
                    Origin = LawConstants.OpOrigin.ADMIN,
                    Decision = LawConstants.Decision.PENDING,
                    EditedByAdmin = true
                });
            }

            foreach (var relInput in input.Relations)
            {
                changeset.Relations.Add(new LawChangesetRelation
                {
                    Id = Guid.NewGuid(),
                    ChangesetId = changeset.Id,
                    RelationType = relInput.RelationType.ToUpperInvariant(),
                    TargetDocumentNumber = relInput.TargetDocumentNumber,
                    TargetArticle = relInput.TargetArticle,
                    TargetClause = relInput.TargetClause,
                    TargetPoint = relInput.TargetPoint,
                    EffectiveDate = relInput.EffectiveDate,
                    EvidenceText = relInput.Evidence,
                    Note = relInput.Note,
                    Origin = LawConstants.OpOrigin.ADMIN,
                    Decision = LawConstants.Decision.PENDING
                });
            }

            _dbContext.LawChangesets.Add(changeset);
            await _dbContext.SaveChangesAsync(ct);

            // Run normalizer
            await NormalizeChangesetAsync(changeset, ct);
            await _dbContext.SaveChangesAsync(ct);

            return await GetChangesetDetailAsync(changeset.Id, ct);
        }

        public async Task<ChangesetDetailDto> ImportChangesetAsync(IFormFile file, string reason, Guid? documentId, Guid adminId, CancellationToken ct = default)
        {
            await AssertNoOpenChangesetAsync(ct);

            if (file == null || file.Length == 0)
            {
                throw new BadRequestException("E-LAW_INVALID_FILE", "Vui lòng chọn file JSON để import.");
            }

            LawChangesetExtractResult? result = null;
            try
            {
                using var stream = file.OpenReadStream();
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                var root = doc.RootElement;

                // Support both direct result object and full response object
                var target = root.TryGetProperty("result", out var rProp) ? rProp : root;
                result = JsonSerializer.Deserialize<LawChangesetExtractResult>(target.GetRawText(), new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize import JSON file.");
                throw new BadRequestException("E-LAW_INVALID_FILE", "File JSON import không đúng cấu trúc.");
            }

            if (result == null)
            {
                throw new BadRequestException("E-LAW_INVALID_FILE", "File JSON import không có dữ liệu kết quả.");
            }

            LegalDocument? legalDoc = null;
            var warnings = result.Warnings ?? new List<string>();

            if (documentId.HasValue)
            {
                legalDoc = await _dbContext.LegalDocuments.FirstOrDefaultAsync(d => d.Id == documentId.Value, ct);
            }
            else if (result.Document != null && !string.IsNullOrWhiteSpace(result.Document.DocumentNumber))
            {
                string rawNum = result.Document.DocumentNumber;
                string normNum = LegalDocumentNumber.Normalize(rawNum);

                // 1. Look for existing placeholder
                var placeholder = await _dbContext.LegalDocuments.FirstOrDefaultAsync(d => d.NumberNormalized == normNum && d.IsPlaceholder, ct);
                if (placeholder != null)
                {
                    placeholder.Title = result.Document.Title ?? placeholder.Title;
                    placeholder.Issuer = result.Document.Issuer ?? placeholder.Issuer;
                    placeholder.DocumentType = result.Document.DocumentType ?? LegalDocumentNumber.InferType(rawNum);
                    placeholder.IssuedDate = result.Document.IssuedDate ?? placeholder.IssuedDate;
                    placeholder.EffectiveDate = result.Document.EffectiveDate ?? placeholder.EffectiveDate;
                    placeholder.IsPlaceholder = false;
                    legalDoc = placeholder;
                }
                else
                {
                    // 2. Check if a real document already exists
                    bool realDocExists = await _dbContext.LegalDocuments.AnyAsync(d => d.NumberNormalized == normNum && !d.IsPlaceholder, ct);
                    if (realDocExists)
                    {
                        legalDoc = new LegalDocument
                        {
                            Id = Guid.NewGuid(),
                            DocumentNumber = rawNum,
                            NumberNormalized = null, // Trigger DUPLICATE_DOCUMENT check
                            DocumentType = result.Document.DocumentType ?? LegalDocumentNumber.InferType(rawNum),
                            Title = result.Document.Title ?? string.Empty,
                            Issuer = result.Document.Issuer,
                            IssuedDate = result.Document.IssuedDate,
                            EffectiveDate = result.Document.EffectiveDate,
                            IsPlaceholder = false
                        };
                        warnings.Add("DUPLICATE_DOCUMENT");
                        _dbContext.LegalDocuments.Add(legalDoc);
                    }
                    else
                    {
                        // 3. New document
                        legalDoc = new LegalDocument
                        {
                            Id = Guid.NewGuid(),
                            DocumentNumber = rawNum,
                            NumberNormalized = normNum,
                            DocumentType = result.Document.DocumentType ?? LegalDocumentNumber.InferType(rawNum),
                            Title = result.Document.Title ?? string.Empty,
                            Issuer = result.Document.Issuer,
                            IssuedDate = result.Document.IssuedDate,
                            EffectiveDate = result.Document.EffectiveDate,
                            LegalStatus = LawConstants.LegalStatus.CHUA_RO,
                            IsPlaceholder = false
                        };
                        _dbContext.LegalDocuments.Add(legalDoc);
                    }
                }
            }

            int head = await _queryService.GetHeadRevisionAsync(ct);

            var changeset = new LawChangeset
            {
                Id = Guid.NewGuid(),
                DocumentId = legalDoc?.Id,
                BaseRevisionNo = head,
                Status = LawConstants.ChangesetStatus.READY,
                Origin = LawConstants.ChangesetOrigin.IMPORT,
                Reason = reason,
                PagesRead = result.Coverage?.PagesRead,
                TotalPages = result.Coverage?.TotalPages,
                AiWarnings = JsonSerializer.Serialize(warnings),
                CreatedBy = adminId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            int seq = 1;
            foreach (var op in result.Operations)
            {
                changeset.Ops.Add(new LawChangeOp
                {
                    Id = Guid.NewGuid(),
                    ChangesetId = changeset.Id,
                    Seq = seq++,
                    OpKey = op.OpKey,
                    OpType = op.Op.ToUpperInvariant(),
                    RuleCode = op.RuleCode.ToUpperInvariant(),
                    NewCode = op.NewCode,
                    ProposedDefinition = op.ProposedDefinition?.GetRawText(),
                    After = op.After?.GetRawText(),
                    ApplyFrom = op.ApplyFrom,
                    ApplyTo = op.ApplyTo,
                    ApplyBasis = op.ApplyBasis == null ? null : JsonSerializer.Serialize(op.ApplyBasis),
                    Article = op.Citation?.Article,
                    Clause = op.Citation?.Clause,
                    Point = op.Citation?.Point,
                    Page = op.Citation?.Page,
                    EvidenceText = op.Evidence,
                    Confidence = op.Confidence.HasValue ? (decimal)op.Confidence.Value : null,
                    Rationale = op.Rationale,
                    Origin = LawConstants.OpOrigin.ADMIN,
                    Decision = LawConstants.Decision.PENDING
                });
            }

            foreach (var rel in result.Relations)
            {
                changeset.Relations.Add(new LawChangesetRelation
                {
                    Id = Guid.NewGuid(),
                    ChangesetId = changeset.Id,
                    RelationKey = rel.RelationKey,
                    RelationType = rel.Type.ToUpperInvariant(),
                    TargetDocumentNumber = rel.TargetDocumentNumber,
                    TargetArticle = rel.TargetScope?.TryGetProperty("article", out var a) == true ? a.GetString() : null,
                    TargetClause = rel.TargetScope?.TryGetProperty("clause", out var cl) == true ? cl.GetString() : null,
                    TargetPoint = rel.TargetScope?.TryGetProperty("point", out var p) == true ? p.GetString() : null,
                    SourceArticle = rel.Citation?.Article,
                    SourceClause = rel.Citation?.Clause,
                    SourcePoint = rel.Citation?.Point,
                    SourcePage = rel.Citation?.Page,
                    EffectiveDate = rel.EffectiveDate,
                    EvidenceText = rel.Evidence,
                    Note = rel.Note,
                    Origin = LawConstants.OpOrigin.ADMIN,
                    Decision = LawConstants.Decision.PENDING
                });
            }

            _dbContext.LawChangesets.Add(changeset);
            await _dbContext.SaveChangesAsync(ct);

            // Run normalizer
            await NormalizeChangesetAsync(changeset, ct);
            await _dbContext.SaveChangesAsync(ct);

            return await GetChangesetDetailAsync(changeset.Id, ct);
        }

        public async Task<OpDto> AddOpAsync(Guid changesetId, OperationInput input, Guid adminId, CancellationToken ct = default)
        {
            var changeset = await GetEditableChangesetAsync(changesetId, ct);

            int maxSeq = changeset.Ops.Count > 0 ? changeset.Ops.Max(o => o.Seq) : 0;

            var op = new LawChangeOp
            {
                Id = Guid.NewGuid(),
                ChangesetId = changesetId,
                Seq = maxSeq + 1,
                OpType = input.Op.ToUpperInvariant(),
                RuleCode = input.RuleCode.ToUpperInvariant(),
                After = input.After?.GetRawText(),
                ApplyFrom = input.ApplyFrom,
                ApplyTo = input.ApplyTo,
                ApplyBasis = input.ApplyBasis == null ? null : JsonSerializer.Serialize(input.ApplyBasis),
                Article = input.Article,
                Clause = input.Clause,
                Point = input.Point,
                Page = input.Page,
                EvidenceText = input.Evidence,
                AdminNote = input.AdminNote,
                Origin = LawConstants.OpOrigin.ADMIN,
                Decision = LawConstants.Decision.PENDING,
                EditedByAdmin = true
            };

            changeset.Ops.Add(op);
            await _dbContext.SaveChangesAsync(ct);

            await NormalizeChangesetAsync(changeset, ct);
            await _dbContext.SaveChangesAsync(ct);

            var detail = await GetChangesetDetailAsync(changesetId, ct);
            return detail.Ops.First(o => o.Id == op.Id);
        }

        public async Task<OpDto> PatchOpAsync(Guid changesetId, Guid opId, OperationPatchInput input, Guid adminId, CancellationToken ct = default)
        {
            var changeset = await GetEditableChangesetAsync(changesetId, ct);

            var op = changeset.Ops.FirstOrDefault(o => o.Id == opId);
            if (op == null)
            {
                throw new NotFoundException($"Không tìm thấy dòng thay đổi {opId}");
            }

            if (input.After.HasValue) op.After = input.After.Value.GetRawText();
            if (input.ApplyFrom.HasValue) op.ApplyFrom = input.ApplyFrom;
            if (input.ApplyTo.HasValue) op.ApplyTo = input.ApplyTo;
            if (input.ApplyBasis != null) op.ApplyBasis = JsonSerializer.Serialize(input.ApplyBasis);
            if (input.Article != null) op.Article = input.Article;
            if (input.Clause != null) op.Clause = input.Clause;
            if (input.Point != null) op.Point = input.Point;
            if (input.Page.HasValue) op.Page = input.Page;
            if (input.AdminNote != null) op.AdminNote = input.AdminNote;
            if (input.ConflictState != null) op.ConflictState = input.ConflictState;

            op.EditedByAdmin = true;
            op.UpdatedAt = DateTime.UtcNow;

            // Re-normalize all ops
            await NormalizeChangesetAsync(changeset, ct);

            // Now handle Decision change
            if (!string.IsNullOrWhiteSpace(input.Decision))
            {
                string targetDecision = input.Decision.ToUpperInvariant();
                if (targetDecision == LawConstants.Decision.ACCEPTED)
                {
                    var flags = ParseFlags(op.Flags);
                    if (flags.Contains(LawConstants.OpFlag.NO_APPLY_FROM) || flags.Contains(LawConstants.OpFlag.INVALID_VALUE))
                    {
                        throw new BadRequestException("E-LAW_INVALID_OP", "Không thể chấp nhận dòng đang có lỗi thiếu ngày áp dụng hoặc giá trị sai kiểu.");
                    }
                }

                op.Decision = targetDecision;
                op.DecidedBy = adminId;
                op.DecidedAt = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync(ct);

            var detail = await GetChangesetDetailAsync(changesetId, ct);
            return detail.Ops.First(o => o.Id == op.Id);
        }

        public async Task<ChangesetDetailDto> AcceptAllAsync(Guid changesetId, Guid adminId, CancellationToken ct = default)
        {
            var changeset = await GetEditableChangesetAsync(changesetId, ct);

            var catalog = await _dbContext.LawRuleDefinitions.AsNoTracking().ToListAsync(ct);
            var catalogSet = new HashSet<string>(catalog.Select(c => c.RuleCode), StringComparer.OrdinalIgnoreCase);

            foreach (var op in changeset.Ops.Where(o => o.Decision == LawConstants.Decision.PENDING))
            {
                var flags = ParseFlags(op.Flags);
                bool blocked = flags.Contains(LawConstants.OpFlag.NO_APPLY_FROM)
                            || flags.Contains(LawConstants.OpFlag.INVALID_VALUE)
                            || flags.Contains(LawConstants.OpFlag.DUPLICATE_OP)
                            || (flags.Contains(LawConstants.OpFlag.NEW_CODE) && !catalogSet.Contains(op.RuleCode));

                if (!blocked)
                {
                    op.Decision = LawConstants.Decision.ACCEPTED;
                    op.DecidedBy = adminId;
                    op.DecidedAt = DateTime.UtcNow;
                }
            }

            foreach (var rel in changeset.Relations.Where(r => r.Decision == LawConstants.Decision.PENDING))
            {
                rel.Decision = LawConstants.Decision.ACCEPTED;
            }

            await _dbContext.SaveChangesAsync(ct);
            return await GetChangesetDetailAsync(changesetId, ct);
        }

        public async Task<RelationItemDto> AddRelationAsync(Guid changesetId, RelationInput input, Guid adminId, CancellationToken ct = default)
        {
            var changeset = await GetEditableChangesetAsync(changesetId, ct);

            var rel = new LawChangesetRelation
            {
                Id = Guid.NewGuid(),
                ChangesetId = changesetId,
                RelationType = input.RelationType.ToUpperInvariant(),
                TargetDocumentNumber = input.TargetDocumentNumber,
                TargetArticle = input.TargetArticle,
                TargetClause = input.TargetClause,
                TargetPoint = input.TargetPoint,
                EffectiveDate = input.EffectiveDate,
                EvidenceText = input.Evidence,
                Note = input.Note,
                Origin = LawConstants.OpOrigin.ADMIN,
                Decision = LawConstants.Decision.PENDING
            };

            changeset.Relations.Add(rel);
            await _dbContext.SaveChangesAsync(ct);

            return new RelationItemDto
            {
                Id = rel.Id,
                RelationType = rel.RelationType,
                TargetDocumentNumber = rel.TargetDocumentNumber,
                TargetArticle = rel.TargetArticle,
                TargetClause = rel.TargetClause,
                TargetPoint = rel.TargetPoint,
                EffectiveDate = rel.EffectiveDate,
                Evidence = rel.EvidenceText,
                Note = rel.Note,
                Origin = rel.Origin,
                Decision = rel.Decision
            };
        }

        public async Task<RelationItemDto> PatchRelationAsync(Guid changesetId, Guid relId, RelationPatchInput input, Guid adminId, CancellationToken ct = default)
        {
            var changeset = await GetEditableChangesetAsync(changesetId, ct);

            var rel = changeset.Relations.FirstOrDefault(r => r.Id == relId);
            if (rel == null)
            {
                throw new NotFoundException($"Không tìm thấy quan hệ {relId}");
            }

            if (!string.IsNullOrWhiteSpace(input.Decision)) rel.Decision = input.Decision.ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(input.RelationType)) rel.RelationType = input.RelationType.ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(input.TargetDocumentNumber)) rel.TargetDocumentNumber = input.TargetDocumentNumber;
            if (input.TargetArticle != null) rel.TargetArticle = input.TargetArticle;
            if (input.TargetClause != null) rel.TargetClause = input.TargetClause;
            if (input.TargetPoint != null) rel.TargetPoint = input.TargetPoint;
            if (input.EffectiveDate.HasValue) rel.EffectiveDate = input.EffectiveDate;

            await _dbContext.SaveChangesAsync(ct);

            return new RelationItemDto
            {
                Id = rel.Id,
                RelationType = rel.RelationType,
                TargetDocumentNumber = rel.TargetDocumentNumber,
                TargetArticle = rel.TargetArticle,
                TargetClause = rel.TargetClause,
                TargetPoint = rel.TargetPoint,
                EffectiveDate = rel.EffectiveDate,
                Evidence = rel.EvidenceText,
                Note = rel.Note,
                Origin = rel.Origin,
                Decision = rel.Decision
            };
        }

        public async Task<ChangesetDetailDto> ResolveOrphanAsync(Guid changesetId, OrphanResolveInput input, Guid adminId, CancellationToken ct = default)
        {
            var changeset = await GetEditableChangesetAsync(changesetId, ct);

            string action = input.Action.ToUpperInvariant();
            if (action != LawConstants.OrphanAction.RECITE && action != LawConstants.OrphanAction.END && action != LawConstants.OrphanAction.KEEP)
            {
                throw new BadRequestException("E-LAW_INVALID_OP", "Hành động xử lý orphan phải là RECITE, END hoặc KEEP.");
            }

            if (action == LawConstants.OrphanAction.KEEP && string.IsNullOrWhiteSpace(input.Note))
            {
                throw new BadRequestException("E-LAW_INVALID_OP", "Xử lý KEEP bắt buộc phải có ghi chú (note).");
            }

            var activeVersions = await _dbContext.LawRuleVersions
                .AsNoTracking()
                .Include(v => v.Document)
                .Where(v => v.CreatedInRevision <= changeset.BaseRevisionNo && (!v.SupersededInRevision.HasValue || v.SupersededInRevision.Value > changeset.BaseRevisionNo))
                .ToListAsync(ct);

            var targetVersion = activeVersions.FirstOrDefault(v => v.Id == input.VersionId);
            if (targetVersion == null)
            {
                throw new NotFoundException($"Không tìm thấy phiên bản quy tắc {input.VersionId}");
            }

            // Find relation to use default citation & effective date
            var rel = changeset.Relations.FirstOrDefault(r => r.RelationType == LawConstants.RelationType.REPLACES || r.RelationType == LawConstants.RelationType.REPEALS);
            DateOnly? effDate = input.EffectiveDate ?? rel?.EffectiveDate ?? changeset.Document?.EffectiveDate;

            if (action == LawConstants.OrphanAction.RECITE)
            {
                int maxSeq = changeset.Ops.Count > 0 ? changeset.Ops.Max(o => o.Seq) : 0;
                var op = new LawChangeOp
                {
                    Id = Guid.NewGuid(),
                    ChangesetId = changesetId,
                    Seq = maxSeq + 1,
                    OpType = LawConstants.OpType.RECITE,
                    RuleCode = targetVersion.RuleCode,
                    After = null, // Recite keeps value
                    ApplyFrom = effDate,
                    Article = input.Article ?? rel?.SourceArticle,
                    Clause = input.Clause ?? rel?.SourceClause,
                    Point = input.Point ?? rel?.SourcePoint,
                    Origin = LawConstants.OpOrigin.ADMIN,
                    Decision = LawConstants.Decision.ACCEPTED,
                    DecidedBy = adminId,
                    DecidedAt = DateTime.UtcNow,
                    EditedByAdmin = true
                };
                changeset.Ops.Add(op);
            }
            else if (action == LawConstants.OrphanAction.END)
            {
                int maxSeq = changeset.Ops.Count > 0 ? changeset.Ops.Max(o => o.Seq) : 0;
                var op = new LawChangeOp
                {
                    Id = Guid.NewGuid(),
                    ChangesetId = changesetId,
                    Seq = maxSeq + 1,
                    OpType = LawConstants.OpType.END,
                    RuleCode = targetVersion.RuleCode,
                    ApplyTo = effDate,
                    Article = input.Article ?? rel?.SourceArticle,
                    Clause = input.Clause ?? rel?.SourceClause,
                    Point = input.Point ?? rel?.SourcePoint,
                    Origin = LawConstants.OpOrigin.ADMIN,
                    Decision = LawConstants.Decision.ACCEPTED,
                    DecidedBy = adminId,
                    DecidedAt = DateTime.UtcNow,
                    EditedByAdmin = true
                };
                changeset.Ops.Add(op);
            }

            // Record or update OrphanResolution
            var existingResolution = await _dbContext.LawOrphanResolutions
                .FirstOrDefaultAsync(r => r.ChangesetId == changesetId && r.VersionId == input.VersionId, ct);

            if (existingResolution != null)
            {
                existingResolution.Action = action;
                existingResolution.Note = input.Note;
                existingResolution.ResolvedBy = adminId;
                existingResolution.ResolvedAt = DateTime.UtcNow;
            }
            else
            {
                _dbContext.LawOrphanResolutions.Add(new LawOrphanResolution
                {
                    Id = Guid.NewGuid(),
                    ChangesetId = changesetId,
                    VersionId = input.VersionId,
                    Action = action,
                    Note = input.Note,
                    ResolvedBy = adminId,
                    ResolvedAt = DateTime.UtcNow
                });
            }

            await _dbContext.SaveChangesAsync(ct);

            await NormalizeChangesetAsync(changeset, ct);
            await _dbContext.SaveChangesAsync(ct);

            return await GetChangesetDetailAsync(changesetId, ct);
        }

        public async Task<ChangesetDetailDto> RebaseAsync(Guid changesetId, Guid adminId, CancellationToken ct = default)
        {
            var changeset = await GetEditableChangesetAsync(changesetId, ct);
            int head = await _queryService.GetHeadRevisionAsync(ct);

            if (changeset.BaseRevisionNo == head)
            {
                changeset.Status = LawConstants.ChangesetStatus.READY;
                await _dbContext.SaveChangesAsync(ct);
                return await GetChangesetDetailAsync(changesetId, ct);
            }

            // Find rule codes modified between old base and HEAD
            var modifiedCodes = await _dbContext.LawRuleVersions
                .Where(v => (v.CreatedInRevision > changeset.BaseRevisionNo && v.CreatedInRevision <= head)
                         || (v.SupersededInRevision.HasValue && v.SupersededInRevision.Value > changeset.BaseRevisionNo && v.SupersededInRevision.Value <= head))
                .Select(v => v.RuleCode)
                .Distinct()
                .ToListAsync(ct);

            var modifiedSet = new HashSet<string>(modifiedCodes, StringComparer.OrdinalIgnoreCase);

            foreach (var op in changeset.Ops)
            {
                if (modifiedSet.Contains(op.RuleCode))
                {
                    op.ConflictState = LawConstants.ConflictState.CONFLICT;
                    op.Decision = LawConstants.Decision.PENDING;
                }
            }

            changeset.BaseRevisionNo = head;
            changeset.Status = LawConstants.ChangesetStatus.READY;

            await NormalizeChangesetAsync(changeset, ct);
            await _dbContext.SaveChangesAsync(ct);

            return await GetChangesetDetailAsync(changesetId, ct);
        }

        public async Task<CheckResultDto> CheckAsync(Guid changesetId, CancellationToken ct = default)
        {
            var detail = await GetChangesetDetailAsync(changesetId, ct);
            return detail.Check;
        }

        public async Task<ChangesetDetailDto> RejectAsync(Guid changesetId, string reason, Guid adminId, CancellationToken ct = default)
        {
            var changeset = await _dbContext.LawChangesets.FirstOrDefaultAsync(c => c.Id == changesetId, ct);
            if (changeset == null)
            {
                throw new NotFoundException($"Không tìm thấy bản đề xuất {changesetId}");
            }

            if (changeset.Status == LawConstants.ChangesetStatus.MERGED)
            {
                throw new ConflictException("E-LAW_NOT_EDITABLE", "Không thể từ chối bản đề xuất đã MERGED.");
            }

            changeset.Status = LawConstants.ChangesetStatus.REJECTED;
            changeset.RejectedReason = reason;
            changeset.RejectedBy = adminId;
            changeset.RejectedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(ct);
            return await GetChangesetDetailAsync(changesetId, ct);
        }

        public async Task<int> MergeAsync(Guid changesetId, Guid adminId, CancellationToken ct = default)
        {
            return await _mergeExecutor.ExecuteAsync(changesetId, adminId, ct);
        }

        public async Task RetryAsync(Guid changesetId, Guid adminId, CancellationToken ct = default)
        {
            var changeset = await _dbContext.LawChangesets.FirstOrDefaultAsync(c => c.Id == changesetId, ct);
            if (changeset == null)
            {
                throw new NotFoundException($"Không tìm thấy bản đề xuất {changesetId}");
            }

            if (changeset.Status != LawConstants.ChangesetStatus.FAILED)
            {
                throw new ConflictException("E-LAW_NOT_EDITABLE", "Chỉ có thể retry bản đề xuất ở trạng thái FAILED.");
            }

            // Check if another open changeset exists
            bool hasOtherOpen = await _dbContext.LawChangesets.AnyAsync(c => c.Id != changesetId &&
                (c.Status == LawConstants.ChangesetStatus.EXTRACTING || c.Status == LawConstants.ChangesetStatus.READY || c.Status == LawConstants.ChangesetStatus.STALE), ct);

            if (hasOtherOpen)
            {
                throw new ConflictException("E-LAW_CHANGESET_OPEN_EXISTS", "Đang có một bản đề xuất mở khác chưa hoàn tất.");
            }

            int head = await _queryService.GetHeadRevisionAsync(ct);
            changeset.BaseRevisionNo = head;
            changeset.AiTaskId = Guid.NewGuid();
            changeset.Status = LawConstants.ChangesetStatus.EXTRACTING;
            changeset.AiErrorCode = null;
            changeset.AiErrorMessage = null;
            changeset.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(ct);

            // Publish message to AI queue via producer in Step 5
        }

        private async Task<LawChangeset> GetEditableChangesetAsync(Guid changesetId, CancellationToken ct)
        {
            var changeset = await _dbContext.LawChangesets
                .Include(c => c.Document)
                .Include(c => c.Ops)
                .Include(c => c.Relations)
                .FirstOrDefaultAsync(c => c.Id == changesetId, ct);

            if (changeset == null)
            {
                throw new NotFoundException($"Không tìm thấy bản đề xuất {changesetId}");
            }

            if (changeset.Status != LawConstants.ChangesetStatus.READY && changeset.Status != LawConstants.ChangesetStatus.STALE)
            {
                throw new ConflictException("E-LAW_NOT_EDITABLE", $"Không thể sửa bản đề xuất ở trạng thái {changeset.Status}.");
            }

            return changeset;
        }

        private async Task AssertNoOpenChangesetAsync(CancellationToken ct)
        {
            bool hasOpen = await _dbContext.LawChangesets.AnyAsync(c =>
                c.Status == LawConstants.ChangesetStatus.EXTRACTING ||
                c.Status == LawConstants.ChangesetStatus.READY ||
                c.Status == LawConstants.ChangesetStatus.STALE, ct);

            if (hasOpen)
            {
                throw new ConflictException("E-LAW_CHANGESET_OPEN_EXISTS", "Đang có một bản đề xuất mở khác chưa hoàn tất.");
            }
        }

        private async Task NormalizeChangesetAsync(LawChangeset changeset, CancellationToken ct)
        {
            var activeVersions = await _dbContext.LawRuleVersions
                .AsNoTracking()
                .Include(v => v.Document)
                .Where(v => v.CreatedInRevision <= changeset.BaseRevisionNo && (!v.SupersededInRevision.HasValue || v.SupersededInRevision.Value > changeset.BaseRevisionNo))
                .ToListAsync(ct);

            var catalog = await _dbContext.LawRuleDefinitions.AsNoTracking().ToListAsync(ct);

            var normalizer = new LawOpNormalizer();
            normalizer.Normalize(changeset, activeVersions, catalog, changeset.Document);
        }

        private static List<string> ParseFlags(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new();
            try { return JsonSerializer.Deserialize<List<string>>(json) ?? new(); } catch { return new(); }
        }

        private static JsonElement? ParseJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try { using var doc = JsonDocument.Parse(json); return doc.RootElement.Clone(); } catch { return null; }
        }

        private static CitationDto? ParseCitation(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try { return JsonSerializer.Deserialize<CitationDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); } catch { return null; }
        }
    }
}
