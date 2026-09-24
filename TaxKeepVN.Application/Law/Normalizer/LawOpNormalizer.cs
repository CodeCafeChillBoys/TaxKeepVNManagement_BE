using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TaxKeepVN.Application.Law.Common;
using TaxKeepVN.Domain.Constants;
using TaxKeepVN.Domain.Entities.Law;

namespace TaxKeepVN.Application.Law.Normalizer
{
    public class LawOpNormalizer
    {
        public void Normalize(
            LawChangeset changeset,
            IReadOnlyList<LawRuleVersion> activeVersionsAtBase,
            IReadOnlyList<LawRuleDefinition> catalog,
            LegalDocument? currentDocument = null)
        {
            if (changeset.Ops == null || changeset.Ops.Count == 0)
                return;

            var catalogDict = catalog.ToDictionary(c => c.RuleCode, StringComparer.OrdinalIgnoreCase);

            // Step 1 - 4: Process each op individually
            foreach (var op in changeset.Ops)
            {
                NormalizeSingleOp(op, activeVersionsAtBase, catalogDict, currentDocument);
            }

            // Step 5: Duplicate check across all ops
            ApplyDuplicateCheck(changeset.Ops);
        }

        private void NormalizeSingleOp(
            LawChangeOp op,
            IReadOnlyList<LawRuleVersion> activeVersions,
            Dictionary<string, LawRuleDefinition> catalogDict,
            LegalDocument? currentDocument)
        {
            // 1. Find C
            DateOnly? d = op.OpType == LawConstants.OpType.END
                ? op.ApplyTo?.AddDays(-1)
                : op.ApplyFrom;

            LawRuleVersion? c = null;
            if (d.HasValue)
            {
                c = activeVersions.FirstOrDefault(v =>
                    string.Equals(v.RuleCode, op.RuleCode, StringComparison.OrdinalIgnoreCase)
                    && v.ApplyFrom <= d.Value
                    && (v.ApplyTo == null || v.ApplyTo > d.Value));
            }

            // Populate Before snapshot
            if (c != null)
            {
                var beforeObj = new
                {
                    versionId = c.Id,
                    ruleValue = c.RuleValue,
                    applyFrom = c.ApplyFrom.ToString("yyyy-MM-dd"),
                    applyTo = c.ApplyTo?.ToString("yyyy-MM-dd"),
                    documentId = c.DocumentId,
                    documentNumber = c.Document?.DocumentNumber,
                    article = c.Article,
                    clause = c.Clause,
                    point = c.Point,
                    page = c.Page,
                    evidenceText = c.EvidenceText
                };
                op.Before = JsonSerializer.Serialize(beforeObj);
            }
            else
            {
                op.Before = null;
            }

            var flags = new HashSet<string>();
            catalogDict.TryGetValue(op.RuleCode, out var def);

            // Check new code
            if (def == null)
            {
                flags.Add(LawConstants.OpFlag.NEW_CODE);
                op.NewCode = true;
            }
            else
            {
                op.NewCode = false;
            }

            // Check apply_from
            if (op.OpType != LawConstants.OpType.END && op.ApplyFrom == null)
            {
                flags.Add(LawConstants.OpFlag.NO_APPLY_FROM);
            }

            // Value check
            string? valueKind = def?.ValueKind;
            if (op.OpType != LawConstants.OpType.END && !string.IsNullOrWhiteSpace(op.After))
            {
                if (valueKind != null && !LawValueComparer.IsValidValue(op.After, valueKind))
                {
                    flags.Add(LawConstants.OpFlag.INVALID_VALUE);
                }
            }

            // Evidence check
            if (string.IsNullOrWhiteSpace(op.Article) ||
                (op.Origin == LawConstants.OpOrigin.AI && string.IsNullOrWhiteSpace(op.EvidenceText)))
            {
                flags.Add(LawConstants.OpFlag.NO_EVIDENCE);
            }

            // Type transformation based on C
            string currentOpType = op.OpType;

            if (currentOpType == LawConstants.OpType.ADD)
            {
                if (c != null)
                {
                    bool sameValue = LawValueComparer.AreValuesEqual(op.After, c.RuleValue, valueKind);
                    if (sameValue)
                    {
                        op.OpType = LawConstants.OpType.RECITE;
                        flags.Add(LawConstants.OpFlag.ADD_TO_RECITE);
                    }
                    else
                    {
                        op.OpType = LawConstants.OpType.UPDATE;
                        flags.Add(LawConstants.OpFlag.ADD_TO_UPDATE);
                    }
                }
            }
            else if (currentOpType == LawConstants.OpType.UPDATE)
            {
                if (c == null)
                {
                    op.OpType = LawConstants.OpType.ADD;
                    flags.Add(LawConstants.OpFlag.UPDATE_TO_ADD);
                }
                else
                {
                    bool sameValue = LawValueComparer.AreValuesEqual(op.After, c.RuleValue, valueKind);
                    if (sameValue)
                    {
                        bool isSameDoc = currentDocument?.Id != null && c.DocumentId.HasValue && c.DocumentId.Value == currentDocument.Id;
                        if (isSameDoc)
                        {
                            flags.Add(LawConstants.OpFlag.SAME_AS_CURRENT);
                            if (op.Decision == LawConstants.Decision.PENDING && !op.EditedByAdmin)
                            {
                                op.Decision = LawConstants.Decision.REJECTED;
                            }
                        }
                        else
                        {
                            op.OpType = LawConstants.OpType.RECITE;
                            flags.Add(LawConstants.OpFlag.UPDATE_TO_RECITE);
                        }
                    }
                }
            }
            else if (currentOpType == LawConstants.OpType.RECITE)
            {
                if (c == null)
                {
                    op.OpType = LawConstants.OpType.ADD;
                    flags.Add(LawConstants.OpFlag.RECITE_TO_ADD);
                }
                else
                {
                    bool sameValue = string.IsNullOrWhiteSpace(op.After) || LawValueComparer.AreValuesEqual(op.After, c.RuleValue, valueKind);
                    if (!sameValue)
                    {
                        op.OpType = LawConstants.OpType.UPDATE;
                        flags.Add(LawConstants.OpFlag.RECITE_TO_UPDATE);
                    }
                }
            }
            else if (currentOpType == LawConstants.OpType.END)
            {
                if (c == null)
                {
                    flags.Add(LawConstants.OpFlag.NOTHING_TO_END);
                    if (op.Decision == LawConstants.Decision.PENDING && !op.EditedByAdmin)
                    {
                        op.Decision = LawConstants.Decision.REJECTED;
                    }
                }
            }

            // If final type is RECITE: check lower level recite
            if (op.OpType == LawConstants.OpType.RECITE && c != null && currentDocument != null)
            {
                int currentLevel = LegalDocumentNumber.Level(currentDocument.DocumentType, currentDocument.DocumentNumber);
                int cLevel = LegalDocumentNumber.Level(c.Document?.DocumentType, c.Document?.DocumentNumber);
                if (currentLevel > cLevel)
                {
                    flags.Add(LawConstants.OpFlag.LOWER_LEVEL_RECITE);
                }
            }

            // If blocking flags, revert decision to PENDING
            if (flags.Contains(LawConstants.OpFlag.NO_APPLY_FROM) || flags.Contains(LawConstants.OpFlag.INVALID_VALUE))
            {
                op.Decision = LawConstants.Decision.PENDING;
            }

            op.Flags = JsonSerializer.Serialize(flags.OrderBy(f => f));
        }

        private void ApplyDuplicateCheck(ICollection<LawChangeOp> ops)
        {
            var groups = ops
                .GroupBy(o => new
                {
                    RuleCode = o.RuleCode.ToUpperInvariant(),
                    EffectiveDate = o.OpType == LawConstants.OpType.END ? o.ApplyTo : o.ApplyFrom
                })
                .Where(g => g.Key.EffectiveDate.HasValue && g.Count() > 1);

            foreach (var group in groups)
            {
                foreach (var op in group)
                {
                    var flags = DeserializeFlags(op.Flags);
                    if (flags.Add(LawConstants.OpFlag.DUPLICATE_OP))
                    {
                        op.Flags = JsonSerializer.Serialize(flags.OrderBy(f => f));
                    }
                }
            }
        }

        private HashSet<string> DeserializeFlags(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new HashSet<string>();

            try
            {
                return JsonSerializer.Deserialize<HashSet<string>>(json) ?? new HashSet<string>();
            }
            catch
            {
                return new HashSet<string>();
            }
        }
    }
}
