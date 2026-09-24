using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TaxKeepVN.Application.Law.Common;
using TaxKeepVN.Application.Law.Selector;
using TaxKeepVN.Domain.Constants;
using TaxKeepVN.Domain.Entities.Law;

namespace TaxKeepVN.Application.Law.Merge
{
    public class LawMergePlanner
    {
        public MergePlan Plan(MergeInput input)
        {
            var plan = new MergePlan();
            var opInfo = new Dictionary<Guid, List<string>>();
            bool hadApplyError = false;

            // Working set W initially cloned from ActiveVersions
            var w = new List<LawRuleVersion>(input.ActiveVersions);

            // Sort accepted ops by (RuleCode, Date, Seq)
            var sortedOps = input.AcceptedOps
                .OrderBy(o => o.RuleCode, StringComparer.OrdinalIgnoreCase)
                .ThenBy(o => o.OpType == LawConstants.OpType.END ? o.ApplyTo : o.ApplyFrom)
                .ThenBy(o => o.Seq)
                .ToList();

            // 10.1 Apply each op to W
            foreach (var op in sortedOps)
            {
                var infoList = new List<string>();

                DateOnly? d = op.OpType == LawConstants.OpType.END
                    ? op.ApplyTo?.AddDays(-1)
                    : op.ApplyFrom;

                if (!d.HasValue)
                {
                    hadApplyError = true;
                    continue;
                }

                var c = w.FirstOrDefault(v =>
                    string.Equals(v.RuleCode, op.RuleCode, StringComparison.OrdinalIgnoreCase)
                    && v.ApplyFrom <= d.Value
                    && (!v.ApplyTo.HasValue || v.ApplyTo.Value > d.Value));

                if (op.OpType == LawConstants.OpType.ADD)
                {
                    if (c != null)
                    {
                        hadApplyError = true;
                        continue;
                    }

                    var n = w.Where(v =>
                        string.Equals(v.RuleCode, op.RuleCode, StringComparison.OrdinalIgnoreCase)
                        && v.ApplyFrom > d.Value)
                        .OrderBy(v => v.ApplyFrom)
                        .FirstOrDefault();

                    DateOnly? endLimit = op.ApplyTo;
                    if (n != null && (!endLimit.HasValue || endLimit.Value > n.ApplyFrom))
                    {
                        endLimit = n.ApplyFrom;
                        infoList.Add("AUTO_BOUNDED");
                    }

                    var vNew = new LawRuleVersion
                    {
                        Id = Guid.NewGuid(),
                        RuleCode = op.RuleCode,
                        ApplyFrom = d.Value,
                        ApplyTo = endLimit,
                        RuleValue = op.After ?? "{}",
                        DocumentId = input.Changeset.DocumentId,
                        Article = op.Article,
                        Clause = op.Clause,
                        Point = op.Point,
                        Page = op.Page,
                        EvidenceText = op.EvidenceText,
                        SourceOpId = op.Id,
                        DerivedFromVersionId = null
                    };

                    w.Add(vNew);
                }
                else if (op.OpType == LawConstants.OpType.UPDATE || op.OpType == LawConstants.OpType.RECITE)
                {
                    if (c == null)
                    {
                        hadApplyError = true;
                        continue;
                    }

                    w.Remove(c);

                    Guid? derivedId = input.ActiveVersions.Any(a => a.Id == c.Id)
                        ? c.Id
                        : c.DerivedFromVersionId;

                    // If C.ApplyFrom < d, clone left part of C
                    if (c.ApplyFrom < d.Value)
                    {
                        var vLeft = new LawRuleVersion
                        {
                            Id = Guid.NewGuid(),
                            RuleCode = c.RuleCode,
                            ApplyFrom = c.ApplyFrom,
                            ApplyTo = d.Value,
                            RuleValue = c.RuleValue,
                            DocumentId = c.DocumentId,
                            Article = c.Article,
                            Clause = c.Clause,
                            Point = c.Point,
                            Page = c.Page,
                            EvidenceText = c.EvidenceText,
                            SourceOpId = c.SourceOpId,
                            DerivedFromVersionId = derivedId
                        };
                        w.Add(vLeft);
                    }

                    // Middle part with new value (or C's value if RECITE)
                    DateOnly? e = op.ApplyTo ?? c.ApplyTo;
                    string val = op.OpType == LawConstants.OpType.RECITE
                        ? c.RuleValue
                        : (op.After ?? "{}");

                    var vMid = new LawRuleVersion
                    {
                        Id = Guid.NewGuid(),
                        RuleCode = op.RuleCode,
                        ApplyFrom = d.Value,
                        ApplyTo = e,
                        RuleValue = val,
                        DocumentId = input.Changeset.DocumentId,
                        Article = op.Article,
                        Clause = op.Clause,
                        Point = op.Point,
                        Page = op.Page,
                        EvidenceText = op.EvidenceText,
                        SourceOpId = op.Id,
                        DerivedFromVersionId = null
                    };
                    w.Add(vMid);

                    // If op.ApplyTo < C.ApplyTo, clone right part of C
                    if (op.ApplyTo.HasValue && (!c.ApplyTo.HasValue || op.ApplyTo.Value < c.ApplyTo.Value))
                    {
                        var vRight = new LawRuleVersion
                        {
                            Id = Guid.NewGuid(),
                            RuleCode = c.RuleCode,
                            ApplyFrom = op.ApplyTo.Value,
                            ApplyTo = c.ApplyTo,
                            RuleValue = c.RuleValue,
                            DocumentId = c.DocumentId,
                            Article = c.Article,
                            Clause = c.Clause,
                            Point = c.Point,
                            Page = c.Page,
                            EvidenceText = c.EvidenceText,
                            SourceOpId = c.SourceOpId,
                            DerivedFromVersionId = derivedId
                        };
                        w.Add(vRight);
                    }
                }
                else if (op.OpType == LawConstants.OpType.END)
                {
                    if (c == null)
                    {
                        hadApplyError = true;
                        continue;
                    }

                    w.Remove(c);

                    Guid? derivedId = input.ActiveVersions.Any(a => a.Id == c.Id)
                        ? c.Id
                        : c.DerivedFromVersionId;

                    if (op.ApplyTo.HasValue && c.ApplyFrom < op.ApplyTo.Value)
                    {
                        var vLeft = new LawRuleVersion
                        {
                            Id = Guid.NewGuid(),
                            RuleCode = c.RuleCode,
                            ApplyFrom = c.ApplyFrom,
                            ApplyTo = op.ApplyTo.Value,
                            RuleValue = c.RuleValue,
                            DocumentId = c.DocumentId,
                            Article = c.Article,
                            Clause = c.Clause,
                            Point = c.Point,
                            Page = c.Page,
                            EvidenceText = c.EvidenceText,
                            SourceOpId = c.SourceOpId,
                            DerivedFromVersionId = derivedId
                        };
                        w.Add(vLeft);
                    }
                }

                if (infoList.Count > 0)
                {
                    opInfo[op.Id] = infoList;
                }
            }

            plan.OpInfo = opInfo;

            // Compute newVersions and supersededVersionIds
            var activeSet = new HashSet<Guid>(input.ActiveVersions.Select(v => v.Id));
            var wSet = new HashSet<Guid>(w.Select(v => v.Id));

            plan.NewVersions = w.Where(v => !activeSet.Contains(v.Id)).ToList();
            plan.SupersededVersionIds = input.ActiveVersions.Where(v => !wSet.Contains(v.Id)).Select(v => v.Id).ToList();

            // 10.2 Document Relations & Placeholders
            var placeholderDict = new Dictionary<string, LegalDocument>(StringComparer.OrdinalIgnoreCase);

            foreach (var rel in input.AcceptedRelations)
            {
                string norm = LegalDocumentNumber.Normalize(rel.TargetDocumentNumber);
                var targetDoc = input.Documents.FirstOrDefault(d => string.Equals(d.NumberNormalized, norm, StringComparison.OrdinalIgnoreCase));

                LegalDocument? ph = null;
                if (targetDoc == null)
                {
                    if (!placeholderDict.TryGetValue(norm, out ph))
                    {
                        ph = new LegalDocument
                        {
                            Id = Guid.NewGuid(),
                            DocumentNumber = rel.TargetDocumentNumber,
                            NumberNormalized = norm,
                            DocumentType = LegalDocumentNumber.InferType(rel.TargetDocumentNumber),
                            LegalStatus = LawConstants.LegalStatus.CHUA_RO,
                            IsPlaceholder = true,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        placeholderDict[norm] = ph;
                    }
                }

                var relToCreate = new LegalDocumentRelation
                {
                    Id = Guid.NewGuid(),
                    SourceDocumentId = input.Changeset.DocumentId ?? Guid.Empty,
                    TargetDocumentId = targetDoc?.Id ?? ph!.Id,
                    RelationType = rel.RelationType,
                    TargetArticle = rel.TargetArticle,
                    TargetClause = rel.TargetClause,
                    TargetPoint = rel.TargetPoint,
                    SourceArticle = rel.SourceArticle,
                    SourceClause = rel.SourceClause,
                    SourcePoint = rel.SourcePoint,
                    SourcePage = rel.SourcePage,
                    EffectiveDate = rel.EffectiveDate,
                    EvidenceText = rel.EvidenceText,
                    Note = rel.Note
                };
                plan.RelationsToCreate.Add(relToCreate);

                // Legal status updates
                Guid targetDocId = targetDoc?.Id ?? ph!.Id;
                if (rel.RelationType == LawConstants.RelationType.REPLACES ||
                    (rel.RelationType == LawConstants.RelationType.REPEALS && string.IsNullOrWhiteSpace(rel.TargetArticle)))
                {
                    plan.LegalStatusUpdates.Add(new LegalStatusUpdate
                    {
                        DocumentId = targetDocId,
                        TargetStatus = LawConstants.LegalStatus.HET_HIEU_LUC,
                        Note = $"Bị thay thế/bãi bỏ bởi {input.Changeset.Document?.DocumentNumber} từ {rel.EffectiveDate ?? input.Changeset.Document?.EffectiveDate}"
                    });
                }
                else if (rel.RelationType == LawConstants.RelationType.REPEALS)
                {
                    plan.LegalStatusUpdates.Add(new LegalStatusUpdate
                    {
                        DocumentId = targetDocId,
                        TargetStatus = LawConstants.LegalStatus.HET_HIEU_LUC_MOT_PHAN,
                        Note = $"Bị bãi bỏ một phần bởi {input.Changeset.Document?.DocumentNumber}"
                    });
                }
                else if (rel.RelationType == LawConstants.RelationType.AMENDS)
                {
                    plan.LegalStatusUpdates.Add(new LegalStatusUpdate
                    {
                        DocumentId = targetDocId,
                        TargetStatus = LawConstants.LegalStatus.CON_HIEU_LUC,
                        Note = $"Được sửa đổi bởi {input.Changeset.Document?.DocumentNumber}"
                    });
                }
            }

            plan.PlaceholdersToCreate = placeholderDict.Values.ToList();

            // Self-status update for current document if CHUA_RO
            if (input.Changeset.Document != null && input.Changeset.Document.LegalStatus == LawConstants.LegalStatus.CHUA_RO)
            {
                plan.LegalStatusUpdates.Add(new LegalStatusUpdate
                {
                    DocumentId = input.Changeset.Document.Id,
                    TargetStatus = LawConstants.LegalStatus.CON_HIEU_LUC
                });
            }

            // 10.2 Point 5: VBHN takes status of base document (CONSOLIDATES with Note = "BASE")
            var allKnownRelations = (input.MergedDocumentRelations ?? Enumerable.Empty<LegalDocumentRelation>())
                .Concat(plan.RelationsToCreate)
                .Concat(input.Documents.SelectMany(d => d.SourceRelations ?? Enumerable.Empty<LegalDocumentRelation>()))
                .Where(r => r.RelationType == LawConstants.RelationType.CONSOLIDATES
                         && !string.IsNullOrWhiteSpace(r.Note)
                         && r.Note.Trim().ToUpperInvariant() == "BASE")
                .ToList();

            foreach (var relBase in allKnownRelations)
            {
                var targetUpdate = plan.LegalStatusUpdates.FirstOrDefault(u => u.DocumentId == relBase.TargetDocumentId);
                var targetDoc = input.Documents.FirstOrDefault(d => d.Id == relBase.TargetDocumentId)
                    ?? plan.PlaceholdersToCreate.FirstOrDefault(p => p.Id == relBase.TargetDocumentId);

                string? baseStatus = targetUpdate?.TargetStatus ?? targetDoc?.LegalStatus;
                if (!string.IsNullOrEmpty(baseStatus) && baseStatus != LawConstants.LegalStatus.CHUA_RO)
                {
                    if (!plan.LegalStatusUpdates.Any(u => u.DocumentId == relBase.SourceDocumentId))
                    {
                        plan.LegalStatusUpdates.Add(new LegalStatusUpdate
                        {
                            DocumentId = relBase.SourceDocumentId,
                            TargetStatus = baseStatus,
                            Note = $"Theo văn bản gốc ({baseStatus})"
                        });
                    }
                }
            }

            // 10.3 Detect Orphans
            var acceptedRuleCodes = new HashSet<string>(input.AcceptedOps.Select(o => o.RuleCode), StringComparer.OrdinalIgnoreCase);
            var resolvedVersionIds = new HashSet<Guid>(input.OrphanResolutions.Select(r => r.VersionId));

            foreach (var rel in input.AcceptedRelations)
            {
                if (rel.RelationType != LawConstants.RelationType.REPLACES && rel.RelationType != LawConstants.RelationType.REPEALS)
                    continue;

                string norm = LegalDocumentNumber.Normalize(rel.TargetDocumentNumber);
                var targetDoc = input.Documents.FirstOrDefault(d => string.Equals(d.NumberNormalized, norm, StringComparison.OrdinalIgnoreCase));
                if (targetDoc == null)
                    continue;

                var affectedDocIds = new HashSet<Guid> { targetDoc.Id };

                // Find VBHN documents that consolidate this target document
                var consolidatingRelations = (input.MergedDocumentRelations ?? Enumerable.Empty<LegalDocumentRelation>())
                    .Where(r => r.TargetDocumentId == targetDoc.Id && r.RelationType == LawConstants.RelationType.CONSOLIDATES)
                    .Select(r => r.SourceDocumentId);

                foreach (var vbhnId in consolidatingRelations)
                {
                    affectedDocIds.Add(vbhnId);
                }

                DateOnly? eff = rel.EffectiveDate ?? input.Changeset.Document?.EffectiveDate;

                // Find versions in W that belong to affected docs
                var potentialOrphans = w.Where(v =>
                    v.DocumentId.HasValue
                    && affectedDocIds.Contains(v.DocumentId.Value)
                    && (!eff.HasValue || !v.ApplyTo.HasValue || v.ApplyTo.Value > eff.Value)
                    && !acceptedRuleCodes.Contains(v.RuleCode)
                    && !resolvedVersionIds.Contains(v.Id));

                // If scope is specified, filter by scope
                if (!string.IsNullOrWhiteSpace(rel.TargetArticle))
                {
                    potentialOrphans = potentialOrphans.Where(v => string.Equals(v.Article, rel.TargetArticle, StringComparison.OrdinalIgnoreCase));
                }

                foreach (var orphan in potentialOrphans)
                {
                    if (!plan.Orphans.Any(o => o.VersionId == orphan.Id))
                    {
                        var doc = input.Documents.FirstOrDefault(d => d.Id == orphan.DocumentId);
                        plan.Orphans.Add(new OrphanRuleInfo
                        {
                            VersionId = orphan.Id,
                            RuleCode = orphan.RuleCode,
                            DocumentId = orphan.DocumentId ?? Guid.Empty,
                            DocumentNumber = doc?.DocumentNumber,
                            ApplyFrom = orphan.ApplyFrom,
                            ApplyTo = orphan.ApplyTo,
                            Article = orphan.Article,
                            Clause = orphan.Clause,
                            Point = orphan.Point
                        });
                    }
                }
            }

            // 10.5 Run Checks (CHK-01 to CHK-17)
            RunChecks(input, plan, w, hadApplyError);

            // Summary
            plan.Summary = new MergeSummary
            {
                TotalOpsApplied = sortedOps.Count,
                AddOpsCount = sortedOps.Count(o => o.OpType == LawConstants.OpType.ADD),
                UpdateOpsCount = sortedOps.Count(o => o.OpType == LawConstants.OpType.UPDATE),
                ReciteOpsCount = sortedOps.Count(o => o.OpType == LawConstants.OpType.RECITE),
                EndOpsCount = sortedOps.Count(o => o.OpType == LawConstants.OpType.END),
                RelationsCount = plan.RelationsToCreate.Count,
                NewVersionsCount = plan.NewVersions.Count,
                SupersededVersionsCount = plan.SupersededVersionIds.Count,
                PlaceholdersCount = plan.PlaceholdersToCreate.Count
            };

            return plan;
        }

        private void RunChecks(MergeInput input, MergePlan plan, List<LawRuleVersion> w, bool hadApplyError)
        {
            var catalogDict = input.Catalog.ToDictionary(c => c.RuleCode, StringComparer.OrdinalIgnoreCase);

            // CHK-01: Mọi dòng ACCEPTED có Article. Dòng AI có Page. Bản MANUAL/IMPORT có Reason.
            bool chk01 = true;
            foreach (var op in input.AcceptedOps)
            {
                if (string.IsNullOrWhiteSpace(op.Article))
                    chk01 = false;
                if (op.Origin == LawConstants.OpOrigin.AI && !op.Page.HasValue)
                    chk01 = false;
            }
            if (input.Changeset.Origin == LawConstants.ChangesetOrigin.MANUAL || input.Changeset.Origin == LawConstants.ChangesetOrigin.IMPORT)
            {
                if (string.IsNullOrWhiteSpace(input.Changeset.Reason))
                    chk01 = false;
            }
            plan.Checks.Add(new MergeCheckResult
            {
                Code = "CHK-01",
                Message = "Mọi dòng ACCEPTED phải có căn cứ (Điều/Trang) và bản đề xuất phải có lý do",
                Level = CheckLevel.BLOCK,
                Passed = chk01
            });

            // CHK-02: Giá trị hợp lệ theo kiểu
            bool chk02 = true;
            foreach (var op in input.AcceptedOps)
            {
                if (op.OpType != LawConstants.OpType.END && !string.IsNullOrWhiteSpace(op.After))
                {
                    catalogDict.TryGetValue(op.RuleCode, out var def);
                    string kind = def?.ValueKind ?? LawConstants.ValueKind.TEXT;
                    if (!LawValueComparer.IsValidValue(op.After, kind))
                        chk02 = false;
                }
            }
            plan.Checks.Add(new MergeCheckResult
            {
                Code = "CHK-02",
                Message = "Mọi giá trị dòng ACCEPTED phải hợp lệ theo định dạng ValueKind",
                Level = CheckLevel.BLOCK,
                Passed = chk02
            });

            // CHK-03: W không có hai phiên bản cùng mã chồng khoảng, không có dòng nào áp lỗi
            bool chk03 = !hadApplyError;
            var groupedW = w.GroupBy(v => v.RuleCode, StringComparer.OrdinalIgnoreCase);
            foreach (var grp in groupedW)
            {
                var list = grp.OrderBy(v => v.ApplyFrom).ToList();
                for (int i = 0; i < list.Count - 1; i++)
                {
                    var cur = list[i];
                    var next = list[i + 1];
                    if (!cur.ApplyTo.HasValue || cur.ApplyTo.Value > next.ApplyFrom)
                    {
                        chk03 = false;
                        break;
                    }
                }
                if (!chk03) break;
            }
            plan.Checks.Add(new MergeCheckResult
            {
                Code = "CHK-03",
                Message = "Tập phiên bản sau khi áp không bị chồng khoảng và không có dòng áp lỗi",
                Level = CheckLevel.BLOCK,
                Passed = chk03
            });

            // CHK-04: Mọi RuleCode có trong catalog và IsActive
            bool chk04 = input.AcceptedOps.All(o => catalogDict.TryGetValue(o.RuleCode, out var def) && def.IsActive);
            plan.Checks.Add(new MergeCheckResult
            {
                Code = "CHK-04",
                Message = "Mọi mã luật của dòng ACCEPTED phải có trong danh mục và đang hoạt động",
                Level = CheckLevel.BLOCK,
                Passed = chk04
            });

            // CHK-05: Không còn rule bị bỏ lại (orphans)
            bool chk05 = plan.Orphans.Count == 0;
            plan.Checks.Add(new MergeCheckResult
            {
                Code = "CHK-05",
                Message = $"Không còn quy tắc luật bị bỏ lại chưa được xử lý (còn {plan.Orphans.Count} rule)",
                Level = CheckLevel.BLOCK,
                Passed = chk05
            });

            // CHK-06: Không còn dòng CONFLICT
            bool chk06 = !input.AllOps.Any(o => o.ConflictState == LawConstants.ConflictState.CONFLICT);
            plan.Checks.Add(new MergeCheckResult
            {
                Code = "CHK-06",
                Message = "Không còn dòng thay đổi ở trạng thái xung đột (CONFLICT)",
                Level = CheckLevel.BLOCK,
                Passed = chk06
            });

            // CHK-07: Apply dates valid
            bool chk07 = true;
            foreach (var op in input.AcceptedOps)
            {
                if (op.OpType != LawConstants.OpType.END && !op.ApplyFrom.HasValue)
                    chk07 = false;
                if (op.OpType == LawConstants.OpType.END && !op.ApplyTo.HasValue)
                    chk07 = false;
                if (op.ApplyFrom.HasValue && op.ApplyTo.HasValue && op.ApplyTo.Value <= op.ApplyFrom.Value)
                    chk07 = false;
            }
            plan.Checks.Add(new MergeCheckResult
            {
                Code = "CHK-07",
                Message = "Ngày áp dụng phải hợp lệ (ApplyTo > ApplyFrom)",
                Level = CheckLevel.BLOCK,
                Passed = chk07
            });

            // CHK-08 (WARN): missingRequired for relevant tax years
            var selector = new EffectiveLawSelector();
            var yearsToCheck = new HashSet<int> { input.Today.Year, input.Today.Year - 1 };
            foreach (var op in input.AcceptedOps)
            {
                if (op.ApplyFrom.HasValue) yearsToCheck.Add(op.ApplyFrom.Value.Year);
                if (op.ApplyTo.HasValue) yearsToCheck.Add(op.ApplyTo.Value.Year);
            }
            bool chk08 = true;
            foreach (int year in yearsToCheck)
            {
                var sel = selector.SelectForTaxYear(year, w, input.Catalog);
                if (sel.MissingRequiredRules.Count > 0)
                {
                    chk08 = false;
                    break;
                }
            }
            plan.Checks.Add(new MergeCheckResult
            {
                Code = "CHK-08",
                Message = "Kiểm tra các quy tắc bắt buộc cho luồng tính thuế",
                Level = CheckLevel.WARN,
                Passed = chk08
            });

            // CHK-09 (WARN): PagesRead < TotalPages
            bool chk09 = !(input.Changeset.PagesRead.HasValue && input.Changeset.TotalPages.HasValue
                           && input.Changeset.PagesRead.Value < input.Changeset.TotalPages.Value);
            plan.Checks.Add(new MergeCheckResult
            {
                Code = "CHK-09",
                Message = "Số trang đã đọc khớp tổng số trang văn bản",
                Level = CheckLevel.WARN,
                Passed = chk09
            });

            // CHK-10 (WARN): END leaves versions after ApplyTo
            bool chk10 = true;
            foreach (var op in input.AcceptedOps.Where(o => o.OpType == LawConstants.OpType.END && o.ApplyTo.HasValue))
            {
                if (w.Any(v => string.Equals(v.RuleCode, op.RuleCode, StringComparison.OrdinalIgnoreCase) && v.ApplyFrom >= op.ApplyTo!.Value))
                {
                    chk10 = false;
                    break;
                }
            }
            plan.Checks.Add(new MergeCheckResult
            {
                Code = "CHK-10",
                Message = "Dòng END không để lại phiên bản thừa phía sau ngày kết thúc",
                Level = CheckLevel.WARN,
                Passed = chk10
            });

            // CHK-11: Duplicate accepted op
            bool chk11 = !input.AcceptedOps
                .GroupBy(o => new { o.RuleCode, Date = o.OpType == LawConstants.OpType.END ? o.ApplyTo : o.ApplyFrom })
                .Any(g => g.Count() > 1);
            plan.Checks.Add(new MergeCheckResult
            {
                Code = "CHK-11",
                Message = "Không có hai dòng ACCEPTED trùng mã và trùng ngày hiệu lực",
                Level = CheckLevel.BLOCK,
                Passed = chk11
            });

            // CHK-12: REPLACES/REPEALS relation missing effectiveDate when rules affected
            bool chk12 = true;
            foreach (var rel in input.AcceptedRelations)
            {
                if (rel.RelationType == LawConstants.RelationType.REPLACES || rel.RelationType == LawConstants.RelationType.REPEALS)
                {
                    DateOnly? eff = rel.EffectiveDate ?? input.Changeset.Document?.EffectiveDate;
                    if (!eff.HasValue && plan.Orphans.Count > 0)
                    {
                        chk12 = false;
                    }
                }
            }
            plan.Checks.Add(new MergeCheckResult
            {
                Code = "CHK-12",
                Message = "Quan hệ REPLACES/REPEALS có ảnh hưởng quy tắc phải có ngày hiệu lực",
                Level = CheckLevel.BLOCK,
                Passed = chk12
            });

            // CHK-13: DocumentNumber or DocumentType missing on AI or IMPORT
            bool chk13 = true;
            if (input.Changeset.Origin == LawConstants.ChangesetOrigin.AI || input.Changeset.Origin == LawConstants.ChangesetOrigin.IMPORT)
            {
                if (input.Changeset.Document == null
                    || string.IsNullOrWhiteSpace(input.Changeset.Document.DocumentNumber)
                    || string.IsNullOrWhiteSpace(input.Changeset.Document.DocumentType))
                {
                    chk13 = false;
                }
            }
            plan.Checks.Add(new MergeCheckResult
            {
                Code = "CHK-13",
                Message = "Văn bản phải có đầy đủ số hiệu và loại văn bản",
                Level = CheckLevel.BLOCK,
                Passed = chk13
            });

            // CHK-14 (INFO): Empty changeset
            bool chk14 = input.AcceptedOps.Count > 0 || input.AcceptedRelations.Count > 0;
            plan.Checks.Add(new MergeCheckResult
            {
                Code = "CHK-14",
                Message = chk14 ? "Bản đề xuất có nội dung được chấp nhận" : "Bản đề xuất rỗng (không có dòng nào được chấp nhận)",
                Level = CheckLevel.INFO,
                Passed = chk14
            });

            // CHK-15: BaseRevisionNo matches HeadRevisionNo
            bool chk15 = input.Changeset.BaseRevisionNo == input.HeadRevisionNo;
            plan.Checks.Add(new MergeCheckResult
            {
                Code = "CHK-15",
                Message = $"Base revision ({input.Changeset.BaseRevisionNo}) phải khớp HEAD ({input.HeadRevisionNo})",
                Level = CheckLevel.BLOCK,
                Passed = chk15
            });

            // CHK-16: Still pending ops or relations
            bool chk16 = !input.AllOps.Any(o => o.Decision == LawConstants.Decision.PENDING)
                         && !input.AllRelations.Any(r => r.Decision == LawConstants.Decision.PENDING);
            plan.Checks.Add(new MergeCheckResult
            {
                Code = "CHK-16",
                Message = "Tất cả các dòng và quan hệ phải được xử lý xong (không còn PENDING)",
                Level = CheckLevel.BLOCK,
                Passed = chk16
            });

            // CHK-17: DUPLICATE_DOCUMENT warning
            bool chk17 = true;
            if (!string.IsNullOrWhiteSpace(input.Changeset.AiWarnings))
            {
                try
                {
                    var warns = JsonSerializer.Deserialize<List<string>>(input.Changeset.AiWarnings);
                    if (warns != null && warns.Contains("DUPLICATE_DOCUMENT"))
                        chk17 = false;
                }
                catch { }
            }
            plan.Checks.Add(new MergeCheckResult
            {
                Code = "CHK-17",
                Message = "Văn bản không được trùng số hiệu với văn bản thật đã tồn tại",
                Level = CheckLevel.BLOCK,
                Passed = chk17
            });
        }
    }
}
