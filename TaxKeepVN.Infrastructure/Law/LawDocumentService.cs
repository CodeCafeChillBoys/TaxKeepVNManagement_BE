using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TaxKeepVN.Application.DTOs.Law;
using TaxKeepVN.Application.DTOs.Law.Contract;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Law.Common;
using TaxKeepVN.Application.Law.Document;
using TaxKeepVN.Application.Law.Messaging;
using TaxKeepVN.Application.Law.Query;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Constants;
using TaxKeepVN.Domain.Entities.Law;
using TaxKeepVN.Infrastructure.Contexts;

namespace TaxKeepVN.Infrastructure.Law
{
    public class LawDocumentService : ILawDocumentService
    {
        private readonly TaxKeepDbContext _dbContext;
        private readonly ISystemLawQueryService _queryService;
        private readonly ILawChangesetProducer _producer;
        private readonly IFileStorageService _fileStorageService;
        private readonly ILogger<LawDocumentService> _logger;

        public LawDocumentService(
            TaxKeepDbContext dbContext,
            ISystemLawQueryService queryService,
            ILawChangesetProducer producer,
            IFileStorageService fileStorageService,
            ILogger<LawDocumentService> logger)
        {
            _dbContext = dbContext;
            _queryService = queryService;
            _producer = producer;
            _fileStorageService = fileStorageService;
            _logger = logger;
        }

        public async Task<UploadDocumentResponseDto> UploadDocumentAsync(
            IFormFile file,
            string? sourceUrl,
            string? documentNumberHint,
            Guid adminId,
            string baseUrl,
            CancellationToken ct = default)
        {
            if (file == null || file.Length == 0)
            {
                throw new BadRequestException("E-LAW_INVALID_FILE", "Vui lòng chọn file PDF luật thuế.");
            }

            if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) && file.ContentType != "application/pdf")
            {
                throw new BadRequestException("E-LAW_INVALID_FILE", "File phải có định dạng PDF.");
            }

            if (file.Length > 20 * 1024 * 1024)
            {
                throw new BadRequestException("E-LAW_INVALID_FILE", "Dung lượng file không được vượt quá 20MB.");
            }

            // Assert no open changeset exists
            bool hasOpen = await _dbContext.LawChangesets.AnyAsync(c =>
                c.Status == LawConstants.ChangesetStatus.EXTRACTING ||
                c.Status == LawConstants.ChangesetStatus.READY ||
                c.Status == LawConstants.ChangesetStatus.STALE, ct);

            if (hasOpen)
            {
                throw new ConflictException("E-LAW_CHANGESET_OPEN_EXISTS", "Đang có một bản đề xuất mở khác chưa hoàn tất.");
            }

            // Save file
            string relativeUrl = await _fileStorageService.SaveFileAsync(file, "law-documents");
            string fullFileUrl = relativeUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? relativeUrl
                : $"{baseUrl.TrimEnd('/')}/{relativeUrl.TrimStart('/')}";

            int head = await _queryService.GetHeadRevisionAsync(ct);

            string? normNum = !string.IsNullOrWhiteSpace(documentNumberHint) ? LegalDocumentNumber.Normalize(documentNumberHint) : null;
            string? docType = !string.IsNullOrWhiteSpace(documentNumberHint) ? LegalDocumentNumber.InferType(documentNumberHint) : null;

            var doc = new LegalDocument
            {
                Id = Guid.NewGuid(),
                DocumentNumber = documentNumberHint,
                NumberNormalized = normNum,
                DocumentType = docType,
                Title = file.FileName,
                FileUrl = fullFileUrl,
                OriginalFilename = file.FileName,
                SourceUrl = sourceUrl,
                LegalStatus = LawConstants.LegalStatus.CHUA_RO,
                IsPlaceholder = false,
                CreatedBy = adminId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _dbContext.LegalDocuments.Add(doc);

            var taskId = Guid.NewGuid();
            var changeset = new LawChangeset
            {
                Id = Guid.NewGuid(),
                DocumentId = doc.Id,
                BaseRevisionNo = head,
                Status = LawConstants.ChangesetStatus.EXTRACTING,
                Origin = LawConstants.ChangesetOrigin.AI,
                AiTaskId = taskId,
                CreatedBy = adminId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _dbContext.LawChangesets.Add(changeset);
            await _dbContext.SaveChangesAsync(ct);

            // Build request to publish to RabbitMQ
            var activeVersions = await _dbContext.LawRuleVersions
                .AsNoTracking()
                .Include(v => v.Document)
                .Where(v => v.CreatedInRevision <= head && (!v.SupersededInRevision.HasValue || v.SupersededInRevision.Value > head))
                .OrderBy(v => v.RuleCode)
                .ThenBy(v => v.ApplyFrom)
                .ToListAsync(ct);

            var catalog = await _dbContext.LawRuleDefinitions.AsNoTracking().Where(d => d.IsActive).ToListAsync(ct);
            var catalogMap = catalog.ToDictionary(c => c.RuleCode, StringComparer.OrdinalIgnoreCase);

            var currentRules = activeVersions.Select(v =>
            {
                var snap = RuleValueSnapshot.FromVersion(v);
                catalogMap.TryGetValue(v.RuleCode, out var def);
                return new CurrentRuleDto
                {
                    VersionId = v.Id,
                    RuleCode = v.RuleCode,
                    ValueKind = def?.ValueKind ?? LawConstants.ValueKind.AMOUNT,
                    ValueNumber = snap.ValueNumber,
                    ValueJson = snap.ValueJson,
                    ValueText = snap.ValueText,
                    Unit = snap.Unit,
                    Condition = snap.Condition,
                    ConditionText = snap.ConditionText,
                    ApplyFrom = v.ApplyFrom,
                    ApplyTo = v.ApplyTo,
                    Citation = new CitationDto
                    {
                        DocumentNumber = v.Document?.DocumentNumber,
                        Article = v.Article,
                        Clause = v.Clause,
                        Point = v.Point,
                        Page = v.Page
                    }
                };
            }).ToList();

            var ruleCatalog = catalog.Select(d => new RuleCatalogItemDto
            {
                RuleCode = d.RuleCode,
                RuleGroup = d.RuleGroup,
                ValueKind = d.ValueKind,
                DefaultUnit = d.DefaultUnit,
                DisplayName = d.DisplayName,
                Description = d.Description
            }).ToList();

            var knownDocuments = (await _dbContext.LegalDocuments.AsNoTracking()
                .Where(d => !string.IsNullOrWhiteSpace(d.DocumentNumber))
                .ToListAsync(ct))
                .Select(d => new KnownDocumentDto
                {
                    DocumentNumber = d.DocumentNumber!,
                    DocumentType = d.DocumentType,
                    Title = d.Title,
                    LegalStatus = d.LegalStatus
                }).ToList();

            var extractRequest = new LawChangesetExtractRequest
            {
                SchemaVersion = 1,
                TaskId = taskId,
                ChangesetId = changeset.Id,
                FileUrl = fullFileUrl,
                FileName = file.FileName,
                SourceUrl = sourceUrl,
                DocumentNumberHint = documentNumberHint,
                BaseRevision = head,
                CurrentRules = currentRules,
                RuleCatalog = ruleCatalog,
                KnownDocuments = knownDocuments
            };

            await _producer.PublishExtractRequestAsync(extractRequest);

            return new UploadDocumentResponseDto
            {
                DocumentId = doc.Id,
                ChangesetId = changeset.Id,
                TaskId = taskId
            };
        }

        public async Task<List<LegalDocumentDto>> GetDocumentsAsync(string? status, string? q, int page = 1, int size = 20, CancellationToken ct = default)
        {
            if (page < 1) page = 1;
            if (size < 1) size = 20;

            var query = _dbContext.LegalDocuments.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(d => d.LegalStatus.ToUpper() == status.ToUpper());
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                string lowerQ = q.ToLower();
                query = query.Where(d => (d.DocumentNumber != null && d.DocumentNumber.ToLower().Contains(lowerQ))
                                      || (d.Title != null && d.Title.ToLower().Contains(lowerQ)));
            }

            var list = await query
                .OrderByDescending(d => d.CreatedAt)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync(ct);

            return list.Select(d => new LegalDocumentDto
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
            }).ToList();
        }

        public async Task<LegalDocumentDetailDto> GetDocumentDetailAsync(Guid id, CancellationToken ct = default)
        {
            var doc = await _dbContext.LegalDocuments
                .AsNoTracking()
                .Include(d => d.SourceRelations).ThenInclude(r => r.TargetDocument)
                .Include(d => d.TargetRelations).ThenInclude(r => r.SourceDocument)
                .Include(d => d.Changesets)
                .FirstOrDefaultAsync(d => d.Id == id, ct);

            if (doc == null)
            {
                throw new NotFoundException($"Không tìm thấy văn bản pháp luật {id}");
            }

            var dto = new LegalDocumentDetailDto
            {
                Id = doc.Id,
                DocumentNumber = doc.DocumentNumber ?? string.Empty,
                DocumentType = doc.DocumentType,
                Title = doc.Title ?? string.Empty,
                Issuer = doc.Issuer,
                IssuedDate = doc.IssuedDate,
                EffectiveDate = doc.EffectiveDate,
                SourceUrl = doc.SourceUrl,
                FileUrl = doc.FileUrl,
                LegalStatus = doc.LegalStatus,
                IsPlaceholder = doc.IsPlaceholder,
                OutgoingRelations = doc.SourceRelations.Select(r => new RelationItemDto
                {
                    Id = r.Id,
                    RelationType = r.RelationType,
                    TargetDocumentNumber = r.TargetDocument?.DocumentNumber ?? string.Empty,
                    TargetArticle = r.TargetArticle,
                    TargetClause = r.TargetClause,
                    TargetPoint = r.TargetPoint,
                    EffectiveDate = r.EffectiveDate,
                    Evidence = r.EvidenceText,
                    Note = r.Note
                }).ToList(),
                IncomingRelations = doc.TargetRelations.Select(r => new RelationItemDto
                {
                    Id = r.Id,
                    RelationType = r.RelationType,
                    TargetDocumentNumber = doc.DocumentNumber ?? string.Empty,
                    TargetArticle = r.TargetArticle,
                    TargetClause = r.TargetClause,
                    TargetPoint = r.TargetPoint,
                    EffectiveDate = r.EffectiveDate,
                    Evidence = r.EvidenceText,
                    Note = r.Note
                }).ToList(),
                Changesets = doc.Changesets.Select(c => new ChangesetListItemDto
                {
                    Id = c.Id,
                    Status = c.Status,
                    Origin = c.Origin,
                    Reason = c.Reason,
                    BaseRevisionNo = c.BaseRevisionNo,
                    MergedRevisionNo = c.MergedRevisionNo,
                    DocumentId = c.DocumentId,
                    DocumentNumber = doc.DocumentNumber,
                    CreatedAt = c.CreatedAt,
                    CreatedBy = c.CreatedBy?.ToString() ?? string.Empty
                }).ToList()
            };

            return dto;
        }

        public async Task<LegalDocumentDto> UpdateDocumentAsync(Guid id, UpdateLegalDocumentInput input, CancellationToken ct = default)
        {
            var doc = await _dbContext.LegalDocuments.FirstOrDefaultAsync(d => d.Id == id, ct);
            if (doc == null)
            {
                throw new NotFoundException($"Không tìm thấy văn bản {id}");
            }

            bool hasMerged = await _dbContext.LawChangesets.AnyAsync(c => c.DocumentId == id && c.Status == LawConstants.ChangesetStatus.MERGED, ct);

            if (hasMerged)
            {
                bool triedModifyCore = input.DocumentNumber != null || input.DocumentType != null
                                    || input.Issuer != null || input.IssuedDate != null || input.EffectiveDate != null;

                if (triedModifyCore)
                {
                    throw new ConflictException("E-LAW_NOT_EDITABLE", "Văn bản đã có bản đề xuất MERGED thì chỉ sửa được tiêu đề (Title) và nguồn (SourceUrl).");
                }

                if (input.Title != null) doc.Title = input.Title;
                if (input.SourceUrl != null) doc.SourceUrl = input.SourceUrl;
            }
            else
            {
                if (input.DocumentNumber != null)
                {
                    doc.DocumentNumber = input.DocumentNumber;
                    doc.NumberNormalized = LegalDocumentNumber.Normalize(input.DocumentNumber);
                    doc.DocumentType = input.DocumentType ?? LegalDocumentNumber.InferType(input.DocumentNumber);
                }
                else if (input.DocumentType != null)
                {
                    doc.DocumentType = input.DocumentType;
                }

                if (input.Title != null) doc.Title = input.Title;
                if (input.Issuer != null) doc.Issuer = input.Issuer;
                if (input.IssuedDate.HasValue) doc.IssuedDate = input.IssuedDate;
                if (input.EffectiveDate.HasValue) doc.EffectiveDate = input.EffectiveDate;
                if (input.SourceUrl != null) doc.SourceUrl = input.SourceUrl;
            }

            doc.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
            _queryService.IncrementMetaVersion();

            return new LegalDocumentDto
            {
                Id = doc.Id,
                DocumentNumber = doc.DocumentNumber ?? string.Empty,
                DocumentType = doc.DocumentType,
                Title = doc.Title ?? string.Empty,
                Issuer = doc.Issuer,
                IssuedDate = doc.IssuedDate,
                EffectiveDate = doc.EffectiveDate,
                SourceUrl = doc.SourceUrl,
                FileUrl = doc.FileUrl,
                LegalStatus = doc.LegalStatus,
                IsPlaceholder = doc.IsPlaceholder
            };
        }
    }
}
