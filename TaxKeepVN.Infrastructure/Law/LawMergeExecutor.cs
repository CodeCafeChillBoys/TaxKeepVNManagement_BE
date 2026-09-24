using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Law.Merge;
using TaxKeepVN.Application.Law.Notification;
using TaxKeepVN.Application.Law.Query;
using TaxKeepVN.Domain.Constants;
using TaxKeepVN.Domain.Entities.Law;
using TaxKeepVN.Infrastructure.Contexts;

namespace TaxKeepVN.Infrastructure.Law
{
    public class LawMergeExecutor : ILawMergeExecutor
    {
        private readonly TaxKeepDbContext _dbContext;
        private readonly ISystemLawQueryService _queryService;
        private readonly ILawNotificationService _notificationService;
        private readonly ILogger<LawMergeExecutor> _logger;

        public LawMergeExecutor(
            TaxKeepDbContext dbContext,
            ISystemLawQueryService queryService,
            ILawNotificationService notificationService,
            ILogger<LawMergeExecutor> logger)
        {
            _dbContext = dbContext;
            _queryService = queryService;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task<int> ExecuteAsync(Guid changesetId, Guid adminId, CancellationToken ct = default)
        {
            await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);

            // Acquire Postgres advisory lock to ensure only one merge executes at a time
            await _dbContext.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(74260001);", ct);

            int head = await _dbContext.LawRevisions.MaxAsync(r => (int?)r.RevisionNo, ct) ?? 0;

            var changeset = await _dbContext.LawChangesets
                .Include(c => c.Ops)
                .Include(c => c.Relations)
                .FirstOrDefaultAsync(c => c.Id == changesetId, ct);

            if (changeset == null)
            {
                throw new NotFoundException($"Không tìm thấy bản đề xuất {changesetId}");
            }

            if (changeset.Status != LawConstants.ChangesetStatus.READY)
            {
                throw new ConflictException("E-LAW_NOT_READY", "Bản đề xuất chưa ở trạng thái READY để merge.");
            }

            if (changeset.BaseRevisionNo != head)
            {
                throw new ConflictException("E-LAW_STALE_BASE", $"BaseRevisionNo ({changeset.BaseRevisionNo}) khác HEAD ({head}). Vui lòng rebase trước khi merge.");
            }

            // Load active versions at HEAD
            var activeVersions = await _dbContext.LawRuleVersions
                .Include(v => v.Document)
                .Where(v => v.CreatedInRevision <= head && (!v.SupersededInRevision.HasValue || v.SupersededInRevision.Value > head))
                .ToListAsync(ct);

            var acceptedOps = changeset.Ops.Where(o => o.Decision == LawConstants.Decision.ACCEPTED).ToList();
            var acceptedRelations = changeset.Relations.Where(r => r.Decision == LawConstants.Decision.ACCEPTED).ToList();
            var orphanResolutions = await _dbContext.LawOrphanResolutions.Where(o => o.ChangesetId == changesetId).ToListAsync(ct);
            var catalog = await _dbContext.LawRuleDefinitions.ToListAsync(ct);
            var documents = await _dbContext.LegalDocuments.Include(d => d.SourceRelations).ToListAsync(ct);
            var relations = await _dbContext.LegalDocumentRelations.AsNoTracking().ToListAsync(ct);

            var input = new MergeInput
            {
                HeadRevisionNo = head,
                ActiveVersions = activeVersions,
                AcceptedOps = acceptedOps,
                AcceptedRelations = acceptedRelations,
                AllOps = changeset.Ops.ToList(),
                AllRelations = changeset.Relations.ToList(),
                OrphanResolutions = orphanResolutions,
                Catalog = catalog,
                Documents = documents,
                MergedDocumentRelations = relations,
                Changeset = changeset,
                Today = DateOnly.FromDateTime(DateTime.UtcNow)
            };

            var planner = new LawMergePlanner();
            var plan = planner.Plan(input);

            if (!plan.CanMerge)
            {
                var blockMessages = plan.Checks
                    .Where(c => c.Level == CheckLevel.BLOCK && !c.Passed)
                    .Select(c => c.Message);
                string errDetail = string.Join("; ", blockMessages);
                throw new UnprocessableEntityException("E-LAW_CHECK_FAILED", $"Kế hoạch merge có kiểm tra mức BLOCK bị trượt: {errDetail}");
            }

            int N = head + 1;

            // 1. Insert LawRevision
            var revision = new LawRevision
            {
                RevisionNo = N,
                ChangesetId = changeset.Id,
                DocumentId = changeset.DocumentId,
                Description = changeset.Reason ?? $"Revision {N}",
                CommittedBy = adminId.ToString(),
                CommittedAt = DateTime.UtcNow,
                MetaVersion = 1
            };
            _dbContext.LawRevisions.Add(revision);

            // 2. Supersede old versions
            if (plan.SupersededVersionIds.Count > 0)
            {
                var superseded = activeVersions.Where(v => plan.SupersededVersionIds.Contains(v.Id)).ToList();
                foreach (var v in superseded)
                {
                    v.SupersededInRevision = N;
                    v.SupersededAt = DateTime.UtcNow;
                }
            }

            // 3. Insert new versions
            foreach (var nv in plan.NewVersions)
            {
                nv.CreatedInRevision = N;
                _dbContext.LawRuleVersions.Add(nv);
            }

            // 4. Create placeholders
            foreach (var ph in plan.PlaceholdersToCreate)
            {
                bool exists = await _dbContext.LegalDocuments.AnyAsync(d => d.NumberNormalized == ph.NumberNormalized, ct);
                if (!exists)
                {
                    _dbContext.LegalDocuments.Add(ph);
                }
            }

            // 5. Create relations
            foreach (var rel in plan.RelationsToCreate)
            {
                rel.CreatedInRevision = N;
                _dbContext.LegalDocumentRelations.Add(rel);
            }

            // 6. Update document legal status
            foreach (var upd in plan.LegalStatusUpdates)
            {
                var targetDoc = await _dbContext.LegalDocuments.FirstOrDefaultAsync(d => d.Id == upd.DocumentId, ct);
                if (targetDoc != null)
                {
                    targetDoc.LegalStatus = upd.TargetStatus;
                    if (!string.IsNullOrWhiteSpace(upd.Note))
                    {
                        targetDoc.LegalStatusNote = string.IsNullOrWhiteSpace(targetDoc.LegalStatusNote)
                            ? upd.Note
                            : $"{targetDoc.LegalStatusNote}\n{upd.Note}";
                    }
                }
            }

            if (changeset.DocumentId.HasValue)
            {
                var curDoc = await _dbContext.LegalDocuments.FirstOrDefaultAsync(d => d.Id == changeset.DocumentId.Value, ct);
                if (curDoc != null && curDoc.LegalStatus == LawConstants.LegalStatus.CHUA_RO)
                {
                    curDoc.LegalStatus = LawConstants.LegalStatus.CON_HIEU_LUC;
                }
            }

            // 7. Update changeset status
            changeset.Status = LawConstants.ChangesetStatus.MERGED;
            changeset.MergedRevisionNo = N;
            changeset.MergedBy = adminId;
            changeset.MergedAt = DateTime.UtcNow;

            // Commit transaction & catch exclusion constraint violation
            try
            {
                await _dbContext.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                _logger.LogError(ex, "Error committing law revision merge for changeset {ChangesetId}", changesetId);

                if (IsExclusionConstraintViolation(ex))
                {
                    throw new UnprocessableEntityException("E-LAW_CHECK_FAILED", "Chồng khoảng áp dụng (vi phạm ràng buộc law_rule_versions_no_overlap).");
                }
                throw;
            }

            // Post-commit: invalidate cache & notify SignalR
            _queryService.InvalidateCache();

            await _notificationService.NotifyRevisionMergedAsync(changeset.CreatedBy?.ToString() ?? string.Empty, new
            {
                revisionNo = N,
                changesetId = changeset.Id,
                documentId = changeset.DocumentId
            });

            _logger.LogInformation("Successfully merged changeset {ChangesetId} into revision {RevisionNo}", changesetId, N);
            return N;
        }

        private static bool IsExclusionConstraintViolation(Exception ex)
        {
            var current = ex;
            while (current != null)
            {
                if (current is PostgresException pgEx && pgEx.SqlState == "23P01")
                    return true;
                current = current.InnerException;
            }
            return false;
        }
    }
}
