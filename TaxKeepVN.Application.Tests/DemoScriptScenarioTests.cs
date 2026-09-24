using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using TaxKeepVN.Application.Law.Merge;
using TaxKeepVN.Application.Law.Normalizer;
using TaxKeepVN.Application.Law.Selector;
using TaxKeepVN.Domain.Constants;
using TaxKeepVN.Domain.Entities.Law;
using Xunit;

namespace TaxKeepVN.Application.Tests
{
    public class DemoScriptScenarioTests
    {
        private readonly List<LawRuleDefinition> _catalog = new()
        {
            new LawRuleDefinition { RuleCode = LawConstants.RuleCodes.PIT_TAX_SCHEDULE, RuleGroup = LawConstants.RuleGroup.SCHEDULE, ValueKind = LawConstants.ValueKind.SCHEDULE, DisplayName = "Biểu thuế", IsActive = true, RequiredForFlow3 = true },
            new LawRuleDefinition { RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_PERSONAL, RuleGroup = LawConstants.RuleGroup.DEDUCTION, ValueKind = LawConstants.ValueKind.AMOUNT, DisplayName = "Bản thân", IsActive = true, RequiredForFlow3 = true },
            new LawRuleDefinition { RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_DEPENDENT, RuleGroup = LawConstants.RuleGroup.DEDUCTION, ValueKind = LawConstants.ValueKind.AMOUNT, DisplayName = "Người phụ thuộc", IsActive = true, RequiredForFlow3 = true },
            new LawRuleDefinition { RuleCode = LawConstants.RuleCodes.PIT_DEPENDENT_GROUPS, RuleGroup = LawConstants.RuleGroup.DEPENDENT, ValueKind = LawConstants.ValueKind.JSON, DisplayName = "Nhóm NPT", IsActive = true },
            new LawRuleDefinition { RuleCode = LawConstants.RuleCodes.PIT_DEPENDENT_MAX_MONTHLY_INCOME, RuleGroup = LawConstants.RuleGroup.DEPENDENT, ValueKind = LawConstants.ValueKind.AMOUNT, DisplayName = "Thu nhập tối đa NPT", IsActive = true },
            new LawRuleDefinition { RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_MEDICAL, RuleGroup = LawConstants.RuleGroup.DEDUCTION, ValueKind = LawConstants.ValueKind.AMOUNT, DisplayName = "Y tế", IsActive = true },
            new LawRuleDefinition { RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_EDUCATION, RuleGroup = LawConstants.RuleGroup.DEDUCTION, ValueKind = LawConstants.ValueKind.AMOUNT, DisplayName = "Giáo dục", IsActive = true },
            new LawRuleDefinition { RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_MANDATORY_INSURANCE, RuleGroup = LawConstants.RuleGroup.DEDUCTION, ValueKind = LawConstants.ValueKind.FLAG, DisplayName = "BH bắt buộc", IsActive = true },
            new LawRuleDefinition { RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_VOLUNTARY_INSURANCE_CAP, RuleGroup = LawConstants.RuleGroup.DEDUCTION, ValueKind = LawConstants.ValueKind.AMOUNT, DisplayName = "Trần hưu trí", IsActive = true },
            new LawRuleDefinition { RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_CHARITY, RuleGroup = LawConstants.RuleGroup.DEDUCTION, ValueKind = LawConstants.ValueKind.FLAG, DisplayName = "Từ thiện", IsActive = true },
            new LawRuleDefinition { RuleCode = LawConstants.RuleCodes.PIT_SETTLEMENT_SELF_REQUIRED_IF_MED_EDU, RuleGroup = LawConstants.RuleGroup.SETTLEMENT, ValueKind = LawConstants.ValueKind.FLAG, DisplayName = "Tự quyết toán", IsActive = true },
            new LawRuleDefinition { RuleCode = LawConstants.RuleCodes.PIT_WITHHOLD_CASUAL_RATE, RuleGroup = LawConstants.RuleGroup.WITHHOLDING, ValueKind = LawConstants.ValueKind.RATE, DisplayName = "Tỷ lệ vãng lai", IsActive = true },
            new LawRuleDefinition { RuleCode = LawConstants.RuleCodes.PIT_WITHHOLD_CASUAL_MIN_PAYMENT, RuleGroup = LawConstants.RuleGroup.WITHHOLDING, ValueKind = LawConstants.ValueKind.AMOUNT, DisplayName = "Ngưỡng vãng lai", IsActive = true },
            new LawRuleDefinition { RuleCode = LawConstants.RuleCodes.PIT_RATE_NON_RESIDENT_SALARY, RuleGroup = LawConstants.RuleGroup.RATE, ValueKind = LawConstants.ValueKind.RATE, DisplayName = "Không cư trú", IsActive = true },
            new LawRuleDefinition { RuleCode = LawConstants.RuleCodes.PIT_EXEMPTION_OVERTIME, RuleGroup = LawConstants.RuleGroup.EXEMPTION, ValueKind = LawConstants.ValueKind.FLAG, DisplayName = "Làm thêm giờ", IsActive = true },
            new LawRuleDefinition { RuleCode = LawConstants.RuleCodes.PIT_EXEMPTION_RETIREMENT_PENSION, RuleGroup = LawConstants.RuleGroup.EXEMPTION, ValueKind = LawConstants.ValueKind.FLAG, DisplayName = "Lương hưu", IsActive = true },
            new LawRuleDefinition { RuleCode = LawConstants.RuleCodes.PIT_EXEMPTION_INSURANCE_COMPENSATION, RuleGroup = LawConstants.RuleGroup.EXEMPTION, ValueKind = LawConstants.ValueKind.FLAG, DisplayName = "Bồi thường BH", IsActive = true }
        };

        [Fact]
        public void DemoScript_RunsAll6Steps_AndMatchesAllTaxYearExpectations()
        {
            var planner = new LawMergePlanner();
            var selector = new EffectiveLawSelector();
            var normalizer = new LawOpNormalizer();

            var activeVersions = new List<LawRuleVersion>();
            var allStoredVersions = new List<LawRuleVersion>();
            var documents = new List<LegalDocument>();
            var mergedRelations = new List<LegalDocumentRelation>();

            int currentRevision = 0;

            // ════════════════════════════════════════════════════════════════════════
            // BƯỚC 1: VBHN 103/VBHN-VPQH
            // ════════════════════════════════════════════════════════════════════════
            var doc103 = new LegalDocument
            {
                Id = Guid.NewGuid(),
                DocumentNumber = "103/VBHN-VPQH",
                NumberNormalized = "103/VBHN-VPQH",
                DocumentType = LawConstants.DocumentType.VBHN,
                Title = "Văn bản hợp nhất Luật Thuế TNCN"
            };
            documents.Add(doc103);

            var cs1 = new LawChangeset
            {
                Id = Guid.NewGuid(),
                DocumentId = doc103.Id,
                Document = doc103,
                BaseRevisionNo = currentRevision,
                Status = LawConstants.ChangesetStatus.READY,
                Origin = LawConstants.ChangesetOrigin.IMPORT,
                Reason = "Bước 1: Luật TNCN hợp nhất",
                Ops = new List<LawChangeOp>
                {
                    new() { Id = Guid.NewGuid(), Seq = 1, Origin = LawConstants.OpOrigin.ADMIN, OpType = LawConstants.OpType.ADD, RuleCode = LawConstants.RuleCodes.PIT_TAX_SCHEDULE, After = "[{\"toAnnual\":60000000,\"rate\":0.05},{\"toAnnual\":120000000,\"rate\":0.1},{\"toAnnual\":216000000,\"rate\":0.15},{\"toAnnual\":384000000,\"rate\":0.2},{\"toAnnual\":624000000,\"rate\":0.25},{\"toAnnual\":960000000,\"rate\":0.3},{\"toAnnual\":null,\"rate\":0.35}]", ApplyFrom = new DateOnly(2009, 1, 1), Article = "22", Decision = LawConstants.Decision.ACCEPTED },
                    new() { Id = Guid.NewGuid(), Seq = 2, Origin = LawConstants.OpOrigin.ADMIN, OpType = LawConstants.OpType.ADD, RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_PERSONAL, After = "{\"valueNumber\":9000000,\"unit\":\"VND/month\"}", ApplyFrom = new DateOnly(2013, 7, 1), Article = "19", Decision = LawConstants.Decision.ACCEPTED },
                    new() { Id = Guid.NewGuid(), Seq = 3, Origin = LawConstants.OpOrigin.ADMIN, OpType = LawConstants.OpType.ADD, RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_DEPENDENT, After = "{\"valueNumber\":3600000,\"unit\":\"VND/person/month\"}", ApplyFrom = new DateOnly(2013, 7, 1), Article = "19", Decision = LawConstants.Decision.ACCEPTED }
                },
                Relations = new List<LawChangesetRelation>
                {
                    new() { Id = Guid.NewGuid(), Origin = LawConstants.OpOrigin.ADMIN, RelationType = LawConstants.RelationType.CONSOLIDATES, TargetDocumentNumber = "04/2007/QH12", Note = "BASE", Decision = LawConstants.Decision.ACCEPTED },
                    new() { Id = Guid.NewGuid(), Origin = LawConstants.OpOrigin.ADMIN, RelationType = LawConstants.RelationType.CONSOLIDATES, TargetDocumentNumber = "26/2012/QH13", Decision = LawConstants.Decision.ACCEPTED },
                    new() { Id = Guid.NewGuid(), Origin = LawConstants.OpOrigin.ADMIN, RelationType = LawConstants.RelationType.CONSOLIDATES, TargetDocumentNumber = "71/2014/QH13", Decision = LawConstants.Decision.ACCEPTED }
                }
            };
            normalizer.Normalize(cs1, activeVersions, _catalog, doc103);
            var plan1 = planner.Plan(new MergeInput
            {
                HeadRevisionNo = currentRevision,
                ActiveVersions = activeVersions,
                AcceptedOps = cs1.Ops.Where(o => o.Decision == LawConstants.Decision.ACCEPTED).ToList(),
                AcceptedRelations = cs1.Relations.Where(r => r.Decision == LawConstants.Decision.ACCEPTED).ToList(),
                AllOps = cs1.Ops.ToList(),
                AllRelations = cs1.Relations.ToList(),
                Catalog = _catalog,
                Documents = documents,
                MergedDocumentRelations = mergedRelations,
                Changeset = cs1,
                Today = new DateOnly(2026, 9, 24)
            });
            string failedChecks = string.Join(", ", plan1.Checks.Where(c => c.Level == CheckLevel.BLOCK && !c.Passed).Select(c => $"{c.Code}: {c.Message}"));
            plan1.CanMerge.Should().BeTrue(because: failedChecks);
            currentRevision = 1;
            ApplyPlan(plan1, currentRevision, activeVersions, allStoredVersions, documents, mergedRelations);

            // ════════════════════════════════════════════════════════════════════════
            // BƯỚC 2: NQ 954/2020/UBTVQH14
            // ════════════════════════════════════════════════════════════════════════
            var doc954 = new LegalDocument
            {
                Id = Guid.NewGuid(),
                DocumentNumber = "954/2020/UBTVQH14",
                NumberNormalized = "954/2020/UBTVQH14",
                DocumentType = LawConstants.DocumentType.NGHI_QUYET,
                Title = "Nghị quyết nâng mức giảm trừ gia cảnh"
            };
            documents.Add(doc954);

            var cs2 = new LawChangeset
            {
                Id = Guid.NewGuid(),
                DocumentId = doc954.Id,
                Document = doc954,
                BaseRevisionNo = currentRevision,
                Status = LawConstants.ChangesetStatus.READY,
                Origin = LawConstants.ChangesetOrigin.IMPORT,
                Reason = "Bước 2: Nâng mức giảm trừ năm 2020",
                Ops = new List<LawChangeOp>
                {
                    new() { Id = Guid.NewGuid(), Seq = 1, Origin = LawConstants.OpOrigin.ADMIN, OpType = LawConstants.OpType.UPDATE, RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_PERSONAL, After = "{\"valueNumber\":11000000,\"unit\":\"VND/month\"}", ApplyFrom = new DateOnly(2020, 1, 1), Article = "1", Decision = LawConstants.Decision.ACCEPTED },
                    new() { Id = Guid.NewGuid(), Seq = 2, Origin = LawConstants.OpOrigin.ADMIN, OpType = LawConstants.OpType.UPDATE, RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_DEPENDENT, After = "{\"valueNumber\":4400000,\"unit\":\"VND/person/month\"}", ApplyFrom = new DateOnly(2020, 1, 1), Article = "1", Decision = LawConstants.Decision.ACCEPTED }
                }
            };
            normalizer.Normalize(cs2, activeVersions, _catalog, doc954);
            var plan2 = planner.Plan(new MergeInput
            {
                HeadRevisionNo = currentRevision,
                ActiveVersions = activeVersions,
                AcceptedOps = cs2.Ops.Where(o => o.Decision == LawConstants.Decision.ACCEPTED).ToList(),
                AcceptedRelations = cs2.Relations.Where(r => r.Decision == LawConstants.Decision.ACCEPTED).ToList(),
                AllOps = cs2.Ops.ToList(),
                AllRelations = cs2.Relations.ToList(),
                Catalog = _catalog,
                Documents = documents,
                MergedDocumentRelations = mergedRelations,
                Changeset = cs2,
                Today = new DateOnly(2026, 9, 24)
            });
            string failedChecks2 = string.Join(", ", plan2.Checks.Where(c => c.Level == CheckLevel.BLOCK && !c.Passed).Select(c => $"{c.Code}: {c.Message}"));
            plan2.CanMerge.Should().BeTrue(because: failedChecks2);
            currentRevision = 2;
            ApplyPlan(plan2, currentRevision, activeVersions, allStoredVersions, documents, mergedRelations);

            // ════════════════════════════════════════════════════════════════════════
            // BƯỚC 3: TT 111/2013/TT-BTC
            // ════════════════════════════════════════════════════════════════════════
            var doc111 = new LegalDocument
            {
                Id = Guid.NewGuid(),
                DocumentNumber = "111/2013/TT-BTC",
                NumberNormalized = "111/2013/TT-BTC",
                DocumentType = LawConstants.DocumentType.THONG_TU,
                Title = "Thông tư hướng dẫn thi hành Luật Thuế TNCN"
            };
            documents.Add(doc111);

            var cs3 = new LawChangeset
            {
                Id = Guid.NewGuid(),
                DocumentId = doc111.Id,
                Document = doc111,
                BaseRevisionNo = currentRevision,
                Status = LawConstants.ChangesetStatus.READY,
                Origin = LawConstants.ChangesetOrigin.IMPORT,
                Reason = "Bước 3: Hướng dẫn Thông tư 111",
                Ops = new List<LawChangeOp>
                {
                    new() { Id = Guid.NewGuid(), Seq = 1, Origin = LawConstants.OpOrigin.ADMIN, OpType = LawConstants.OpType.ADD, RuleCode = LawConstants.RuleCodes.PIT_DEPENDENT_MAX_MONTHLY_INCOME, After = "{\"valueNumber\":1000000,\"unit\":\"VND/month\"}", ApplyFrom = new DateOnly(2013, 7, 1), Article = "9", Decision = LawConstants.Decision.ACCEPTED },
                    new() { Id = Guid.NewGuid(), Seq = 2, Origin = LawConstants.OpOrigin.ADMIN, OpType = LawConstants.OpType.ADD, RuleCode = LawConstants.RuleCodes.PIT_WITHHOLD_CASUAL_RATE, After = "{\"valueNumber\":0.1}", ApplyFrom = new DateOnly(2013, 7, 1), Article = "25", Decision = LawConstants.Decision.ACCEPTED },
                    new() { Id = Guid.NewGuid(), Seq = 3, Origin = LawConstants.OpOrigin.ADMIN, OpType = LawConstants.OpType.ADD, RuleCode = LawConstants.RuleCodes.PIT_WITHHOLD_CASUAL_MIN_PAYMENT, After = "{\"valueNumber\":2000000,\"unit\":\"VND/payment\"}", ApplyFrom = new DateOnly(2013, 7, 1), Article = "25", Decision = LawConstants.Decision.ACCEPTED },
                    new() { Id = Guid.NewGuid(), Seq = 4, Origin = LawConstants.OpOrigin.ADMIN, OpType = LawConstants.OpType.ADD, RuleCode = LawConstants.RuleCodes.PIT_DEPENDENT_GROUPS, After = "{\"groups\":[\"CHILD\",\"SPOUSE\",\"PARENT\"]}", ApplyFrom = new DateOnly(2013, 7, 1), Article = "9", Decision = LawConstants.Decision.ACCEPTED }
                }
            };
            normalizer.Normalize(cs3, activeVersions, _catalog, doc111);
            var plan3 = planner.Plan(new MergeInput
            {
                HeadRevisionNo = currentRevision,
                ActiveVersions = activeVersions,
                AcceptedOps = cs3.Ops.Where(o => o.Decision == LawConstants.Decision.ACCEPTED).ToList(),
                AcceptedRelations = cs3.Relations.Where(r => r.Decision == LawConstants.Decision.ACCEPTED).ToList(),
                AllOps = cs3.Ops.ToList(),
                AllRelations = cs3.Relations.ToList(),
                Catalog = _catalog,
                Documents = documents,
                MergedDocumentRelations = mergedRelations,
                Changeset = cs3,
                Today = new DateOnly(2026, 9, 24)
            });
            string failedChecks3 = string.Join(", ", plan3.Checks.Where(c => c.Level == CheckLevel.BLOCK && !c.Passed).Select(c => $"{c.Code}: {c.Message}"));
            plan3.CanMerge.Should().BeTrue(because: failedChecks3);
            currentRevision = 3;
            ApplyPlan(plan3, currentRevision, activeVersions, allStoredVersions, documents, mergedRelations);

            // ════════════════════════════════════════════════════════════════════════
            // BƯỚC 4: VBHN 112/VBHN-VPQH (Luật 109/2025 & 09/2026)
            // ════════════════════════════════════════════════════════════════════════
            var doc112 = new LegalDocument
            {
                Id = Guid.NewGuid(),
                DocumentNumber = "112/VBHN-VPQH",
                NumberNormalized = "112/VBHN-VPQH",
                DocumentType = LawConstants.DocumentType.VBHN,
                Title = "Văn bản hợp nhất Luật Thuế TNCN mới"
            };
            documents.Add(doc112);

            var cs4 = new LawChangeset
            {
                Id = Guid.NewGuid(),
                DocumentId = doc112.Id,
                Document = doc112,
                BaseRevisionNo = currentRevision,
                Status = LawConstants.ChangesetStatus.READY,
                Origin = LawConstants.ChangesetOrigin.IMPORT,
                Reason = "Bước 4: Luật 109 sửa biểu thuế 5 bậc và nâng mức giảm trừ",
                Ops = new List<LawChangeOp>
                {
                    new() { Id = Guid.NewGuid(), Seq = 1, Origin = LawConstants.OpOrigin.ADMIN, OpType = LawConstants.OpType.UPDATE, RuleCode = LawConstants.RuleCodes.PIT_TAX_SCHEDULE, After = "[{\"toAnnual\":120000000,\"rate\":0.05},{\"toAnnual\":360000000,\"rate\":0.1},{\"toAnnual\":720000000,\"rate\":0.2},{\"toAnnual\":1200000000,\"rate\":0.3},{\"toAnnual\":null,\"rate\":0.35}]", ApplyFrom = new DateOnly(2026, 1, 1), Article = "9", Decision = LawConstants.Decision.ACCEPTED },
                    new() { Id = Guid.NewGuid(), Seq = 2, Origin = LawConstants.OpOrigin.ADMIN, OpType = LawConstants.OpType.UPDATE, RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_PERSONAL, After = "{\"valueNumber\":15500000,\"unit\":\"VND/month\"}", ApplyFrom = new DateOnly(2026, 1, 1), Article = "10", Decision = LawConstants.Decision.ACCEPTED },
                    new() { Id = Guid.NewGuid(), Seq = 3, Origin = LawConstants.OpOrigin.ADMIN, OpType = LawConstants.OpType.UPDATE, RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_DEPENDENT, After = "{\"valueNumber\":6200000,\"unit\":\"VND/person/month\"}", ApplyFrom = new DateOnly(2026, 1, 1), Article = "10", Decision = LawConstants.Decision.ACCEPTED }
                },
                Relations = new List<LawChangesetRelation>
                {
                    new() { Id = Guid.NewGuid(), Origin = LawConstants.OpOrigin.ADMIN, RelationType = LawConstants.RelationType.REPLACES, TargetDocumentNumber = "04/2007/QH12", EffectiveDate = new DateOnly(2026, 1, 1), Decision = LawConstants.Decision.ACCEPTED }
                }
            };
            normalizer.Normalize(cs4, activeVersions, _catalog, doc112);
            var plan4 = planner.Plan(new MergeInput
            {
                HeadRevisionNo = currentRevision,
                ActiveVersions = activeVersions,
                AcceptedOps = cs4.Ops.Where(o => o.Decision == LawConstants.Decision.ACCEPTED).ToList(),
                AcceptedRelations = cs4.Relations.Where(r => r.Decision == LawConstants.Decision.ACCEPTED).ToList(),
                AllOps = cs4.Ops.ToList(),
                AllRelations = cs4.Relations.ToList(),
                Catalog = _catalog,
                Documents = documents,
                MergedDocumentRelations = mergedRelations,
                Changeset = cs4,
                Today = new DateOnly(2026, 9, 24)
            });
            string failedChecks4 = string.Join(", ", plan4.Checks.Where(c => c.Level == CheckLevel.BLOCK && !c.Passed).Select(c => $"{c.Code}: {c.Message}"));
            plan4.CanMerge.Should().BeTrue(because: failedChecks4);
            currentRevision = 4;
            ApplyPlan(plan4, currentRevision, activeVersions, allStoredVersions, documents, mergedRelations);

            // VBHN 103 should now be marked HET_HIEU_LUC according to target document 04/2007/QH12
            var updateFor103 = plan4.LegalStatusUpdates.FirstOrDefault(u => u.DocumentId == doc103.Id);
            updateFor103.Should().NotBeNull();
            updateFor103!.TargetStatus.Should().Be(LawConstants.LegalStatus.HET_HIEU_LUC);

            // ════════════════════════════════════════════════════════════════════════
            // BƯỚC 5: NĐ 253/2026/NĐ-CP
            // ════════════════════════════════════════════════════════════════════════
            var doc253 = new LegalDocument
            {
                Id = Guid.NewGuid(),
                DocumentNumber = "253/2026/NĐ-CP",
                NumberNormalized = "253/2026/ND-CP",
                DocumentType = LawConstants.DocumentType.NGHI_DINH,
                Title = "Nghị định quy định chi tiết Luật Thuế TNCN"
            };
            documents.Add(doc253);

            var cs5 = new LawChangeset
            {
                Id = Guid.NewGuid(),
                DocumentId = doc253.Id,
                Document = doc253,
                BaseRevisionNo = currentRevision,
                Status = LawConstants.ChangesetStatus.READY,
                Origin = LawConstants.ChangesetOrigin.IMPORT,
                Reason = "Bước 5: Nghị định 253 hướng dẫn giảm trừ y tế, giáo dục",
                Ops = new List<LawChangeOp>
                {
                    new() { Id = Guid.NewGuid(), Seq = 1, Origin = LawConstants.OpOrigin.ADMIN, OpType = LawConstants.OpType.ADD, RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_MEDICAL, After = "{\"valueNumber\":23000000,\"unit\":\"VND/year\"}", ApplyFrom = new DateOnly(2026, 1, 1), Article = "49", Decision = LawConstants.Decision.ACCEPTED },
                    new() { Id = Guid.NewGuid(), Seq = 2, Origin = LawConstants.OpOrigin.ADMIN, OpType = LawConstants.OpType.ADD, RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_EDUCATION, After = "{\"valueNumber\":24000000,\"unit\":\"VND/year\"}", ApplyFrom = new DateOnly(2026, 1, 1), Article = "49", Decision = LawConstants.Decision.ACCEPTED },
                    new() { Id = Guid.NewGuid(), Seq = 3, Origin = LawConstants.OpOrigin.ADMIN, OpType = LawConstants.OpType.ADD, RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_VOLUNTARY_INSURANCE_CAP, After = "{\"valueNumber\":3000000,\"unit\":\"VND/month\"}", ApplyFrom = new DateOnly(2026, 1, 1), Article = "48", Decision = LawConstants.Decision.ACCEPTED },
                    new() { Id = Guid.NewGuid(), Seq = 4, Origin = LawConstants.OpOrigin.ADMIN, OpType = LawConstants.OpType.UPDATE, RuleCode = LawConstants.RuleCodes.PIT_WITHHOLD_CASUAL_MIN_PAYMENT, After = "{\"valueNumber\":5000000,\"unit\":\"VND/payment\"}", ApplyFrom = new DateOnly(2026, 7, 1), Article = "50", Decision = LawConstants.Decision.ACCEPTED },
                    new() { Id = Guid.NewGuid(), Seq = 5, Origin = LawConstants.OpOrigin.ADMIN, OpType = LawConstants.OpType.RECITE, RuleCode = LawConstants.RuleCodes.PIT_WITHHOLD_CASUAL_RATE, After = null, ApplyFrom = new DateOnly(2026, 1, 1), Article = "50", Decision = LawConstants.Decision.ACCEPTED }
                },
                Relations = new List<LawChangesetRelation>
                {
                    new() { Id = Guid.NewGuid(), Origin = LawConstants.OpOrigin.ADMIN, RelationType = LawConstants.RelationType.REPLACES, TargetDocumentNumber = "65/2013/NĐ-CP", EffectiveDate = new DateOnly(2026, 7, 1), Decision = LawConstants.Decision.ACCEPTED }
                }
            };
            normalizer.Normalize(cs5, activeVersions, _catalog, doc253);
            var plan5 = planner.Plan(new MergeInput
            {
                HeadRevisionNo = currentRevision,
                ActiveVersions = activeVersions,
                AcceptedOps = cs5.Ops.Where(o => o.Decision == LawConstants.Decision.ACCEPTED).ToList(),
                AcceptedRelations = cs5.Relations.Where(r => r.Decision == LawConstants.Decision.ACCEPTED).ToList(),
                AllOps = cs5.Ops.ToList(),
                AllRelations = cs5.Relations.ToList(),
                Catalog = _catalog,
                Documents = documents,
                MergedDocumentRelations = mergedRelations,
                Changeset = cs5,
                Today = new DateOnly(2026, 9, 24)
            });
            string failedChecks5 = string.Join(", ", plan5.Checks.Where(c => c.Level == CheckLevel.BLOCK && !c.Passed).Select(c => $"{c.Code}: {c.Message}"));
            plan5.CanMerge.Should().BeTrue(because: failedChecks5);
            currentRevision = 5;
            ApplyPlan(plan5, currentRevision, activeVersions, allStoredVersions, documents, mergedRelations);

            // ════════════════════════════════════════════════════════════════════════
            // BƯỚC 6: TT 87/2026/TT-BTC (có rule bị bỏ lại PIT_DEPENDENT_GROUPS)
            // ════════════════════════════════════════════════════════════════════════
            var doc87 = new LegalDocument
            {
                Id = Guid.NewGuid(),
                DocumentNumber = "87/2026/TT-BTC",
                NumberNormalized = "87/2026/TT-BTC",
                DocumentType = LawConstants.DocumentType.THONG_TU,
                Title = "Thông tư 87 thay thế TT 111"
            };
            documents.Add(doc87);

            var cs6 = new LawChangeset
            {
                Id = Guid.NewGuid(),
                DocumentId = doc87.Id,
                Document = doc87,
                BaseRevisionNo = currentRevision,
                Status = LawConstants.ChangesetStatus.READY,
                Origin = LawConstants.ChangesetOrigin.IMPORT,
                Reason = "Bước 6: TT 87 nâng thu nhập tối đa NPT lên 3tr",
                Ops = new List<LawChangeOp>
                {
                    new() { Id = Guid.NewGuid(), Seq = 1, Origin = LawConstants.OpOrigin.ADMIN, OpType = LawConstants.OpType.UPDATE, RuleCode = LawConstants.RuleCodes.PIT_DEPENDENT_MAX_MONTHLY_INCOME, After = "{\"valueNumber\":3000000,\"unit\":\"VND/month\"}", ApplyFrom = new DateOnly(2026, 1, 1), Article = "5", Decision = LawConstants.Decision.ACCEPTED }
                },
                Relations = new List<LawChangesetRelation>
                {
                    new() { Id = Guid.NewGuid(), Origin = LawConstants.OpOrigin.ADMIN, RelationType = LawConstants.RelationType.REPLACES, TargetDocumentNumber = "111/2013/TT-BTC", EffectiveDate = new DateOnly(2026, 7, 1), Decision = LawConstants.Decision.ACCEPTED }
                }
            };
            normalizer.Normalize(cs6, activeVersions, _catalog, doc87);

            // Plan without resolving orphan: CHK-05 must fail
            var plan6Unresolved = planner.Plan(new MergeInput
            {
                HeadRevisionNo = currentRevision,
                ActiveVersions = activeVersions,
                AcceptedOps = cs6.Ops.Where(o => o.Decision == LawConstants.Decision.ACCEPTED).ToList(),
                AcceptedRelations = cs6.Relations.Where(r => r.Decision == LawConstants.Decision.ACCEPTED).ToList(),
                AllOps = cs6.Ops.ToList(),
                AllRelations = cs6.Relations.ToList(),
                Catalog = _catalog,
                Documents = documents,
                MergedDocumentRelations = mergedRelations,
                Changeset = cs6,
                Today = new DateOnly(2026, 9, 24)
            });
            plan6Unresolved.CanMerge.Should().BeFalse();
            plan6Unresolved.Orphans.Should().Contain(o => o.RuleCode == LawConstants.RuleCodes.PIT_DEPENDENT_GROUPS);

            // Now resolve orphan PIT_DEPENDENT_GROUPS via RECITE
            var orphan = plan6Unresolved.Orphans.First(o => o.RuleCode == LawConstants.RuleCodes.PIT_DEPENDENT_GROUPS);
            cs6.Ops.Add(new LawChangeOp
            {
                Id = Guid.NewGuid(),
                Seq = 2,
                Origin = LawConstants.OpOrigin.ADMIN,
                OpType = LawConstants.OpType.RECITE,
                RuleCode = LawConstants.RuleCodes.PIT_DEPENDENT_GROUPS,
                After = null,
                ApplyFrom = new DateOnly(2026, 7, 1),
                Article = "5",
                Decision = LawConstants.Decision.ACCEPTED
            });
            normalizer.Normalize(cs6, activeVersions, _catalog, doc87);

            var plan6Resolved = planner.Plan(new MergeInput
            {
                HeadRevisionNo = currentRevision,
                ActiveVersions = activeVersions,
                AcceptedOps = cs6.Ops.Where(o => o.Decision == LawConstants.Decision.ACCEPTED).ToList(),
                AcceptedRelations = cs6.Relations.Where(r => r.Decision == LawConstants.Decision.ACCEPTED).ToList(),
                AllOps = cs6.Ops.ToList(),
                AllRelations = cs6.Relations.ToList(),
                Catalog = _catalog,
                Documents = documents,
                MergedDocumentRelations = mergedRelations,
                Changeset = cs6,
                Today = new DateOnly(2026, 9, 24)
            });
            string failedChecks6 = string.Join(", ", plan6Resolved.Checks.Where(c => c.Level == CheckLevel.BLOCK && !c.Passed).Select(c => $"{c.Code}: {c.Message}"));
            plan6Resolved.CanMerge.Should().BeTrue(because: failedChecks6);
            currentRevision = 6;
            ApplyPlan(plan6Resolved, currentRevision, activeVersions, allStoredVersions, documents, mergedRelations);

            // ════════════════════════════════════════════════════════════════════════
            // VERIFY HEAD SELECTION AT YEAR 2019, 2020-2025, 2026 (§15.2 TABLE)
            // ════════════════════════════════════════════════════════════════════════
            // Year 2019
            var sel2019 = selector.SelectForTaxYear(2019, activeVersions, _catalog);
            ExtractAmount(sel2019.EffectiveRules[LawConstants.RuleCodes.PIT_DEDUCTION_PERSONAL]).Should().Be(9_000_000);
            ExtractAmount(sel2019.EffectiveRules[LawConstants.RuleCodes.PIT_DEDUCTION_DEPENDENT]).Should().Be(3_600_000);
            ExtractAmount(sel2019.EffectiveRules[LawConstants.RuleCodes.PIT_DEPENDENT_MAX_MONTHLY_INCOME]).Should().Be(1_000_000);
            ExtractAmount(sel2019.EffectiveRules[LawConstants.RuleCodes.PIT_WITHHOLD_CASUAL_MIN_PAYMENT]).Should().Be(2_000_000);
            sel2019.EffectiveRules.ContainsKey(LawConstants.RuleCodes.PIT_DEDUCTION_MEDICAL).Should().BeFalse();

            // Year 2020 - 2025
            var sel2025 = selector.SelectForTaxYear(2025, activeVersions, _catalog);
            ExtractAmount(sel2025.EffectiveRules[LawConstants.RuleCodes.PIT_DEDUCTION_PERSONAL]).Should().Be(11_000_000);
            ExtractAmount(sel2025.EffectiveRules[LawConstants.RuleCodes.PIT_DEDUCTION_DEPENDENT]).Should().Be(4_400_000);
            ExtractAmount(sel2025.EffectiveRules[LawConstants.RuleCodes.PIT_DEPENDENT_MAX_MONTHLY_INCOME]).Should().Be(1_000_000);
            ExtractAmount(sel2025.EffectiveRules[LawConstants.RuleCodes.PIT_WITHHOLD_CASUAL_MIN_PAYMENT]).Should().Be(2_000_000);

            // Year 2026
            var sel2026 = selector.SelectForTaxYear(2026, activeVersions, _catalog);
            ExtractAmount(sel2026.EffectiveRules[LawConstants.RuleCodes.PIT_DEDUCTION_PERSONAL]).Should().Be(15_500_000);
            ExtractAmount(sel2026.EffectiveRules[LawConstants.RuleCodes.PIT_DEDUCTION_DEPENDENT]).Should().Be(6_200_000);
            ExtractAmount(sel2026.EffectiveRules[LawConstants.RuleCodes.PIT_DEPENDENT_MAX_MONTHLY_INCOME]).Should().Be(3_000_000);
            ExtractAmount(sel2026.EffectiveRules[LawConstants.RuleCodes.PIT_DEDUCTION_MEDICAL]).Should().Be(23_000_000);
            ExtractAmount(sel2026.EffectiveRules[LawConstants.RuleCodes.PIT_DEDUCTION_EDUCATION]).Should().Be(24_000_000);
            ExtractAmount(sel2026.EffectiveRules[LawConstants.RuleCodes.PIT_WITHHOLD_CASUAL_MIN_PAYMENT]).Should().Be(5_000_000);
            sel2026.HasMidYearChange.Should().BeTrue();
            sel2026.MidYearChanges.Should().Contain(m => m.RuleCode == LawConstants.RuleCodes.PIT_WITHHOLD_CASUAL_MIN_PAYMENT);

            // ════════════════════════════════════════════════════════════════════════
            // VERIFY CHECKOUT DEMO: As of revision = 3 for taxYear = 2026 (§15.2)
            // ════════════════════════════════════════════════════════════════════════
            var activeAtRev3 = allStoredVersions
                .Where(v => v.CreatedInRevision <= 3 && (!v.SupersededInRevision.HasValue || v.SupersededInRevision.Value > 3))
                .ToList();

            var sel2026AtRev3 = selector.SelectForTaxYear(2026, activeAtRev3, _catalog);
            // At revision = 3, personal deduction for 2026 is still 11tr because Law 109 is not known yet!
            ExtractAmount(sel2026AtRev3.EffectiveRules[LawConstants.RuleCodes.PIT_DEDUCTION_PERSONAL]).Should().Be(11_000_000);
            ExtractAmount(sel2026AtRev3.EffectiveRules[LawConstants.RuleCodes.PIT_DEDUCTION_DEPENDENT]).Should().Be(4_400_000);
        }

        private static void ApplyPlan(
            MergePlan plan,
            int revisionNo,
            List<LawRuleVersion> activeVersions,
            List<LawRuleVersion> allStoredVersions,
            List<LegalDocument> documents,
            List<LegalDocumentRelation> mergedRelations)
        {
            mergedRelations.AddRange(plan.RelationsToCreate);

            foreach (var supId in plan.SupersededVersionIds)
            {
                var v = activeVersions.First(ver => ver.Id == supId);
                v.SupersededInRevision = revisionNo;
                activeVersions.Remove(v);
            }

            foreach (var nv in plan.NewVersions)
            {
                nv.CreatedInRevision = revisionNo;
                activeVersions.Add(nv);
                allStoredVersions.Add(nv);
            }

            foreach (var ph in plan.PlaceholdersToCreate)
            {
                documents.Add(ph);
            }

            foreach (var upd in plan.LegalStatusUpdates)
            {
                var d = documents.FirstOrDefault(doc => doc.Id == upd.DocumentId);
                if (d != null)
                {
                    d.LegalStatus = upd.TargetStatus;
                }
            }
        }

        private static decimal? ExtractAmount(LawRuleVersion v)
        {
            if (string.IsNullOrWhiteSpace(v.RuleValue)) return null;
            using var doc = JsonDocument.Parse(v.RuleValue);
            if (doc.RootElement.TryGetProperty("valueNumber", out var val))
                return val.GetDecimal();
            return null;
        }
    }
}
