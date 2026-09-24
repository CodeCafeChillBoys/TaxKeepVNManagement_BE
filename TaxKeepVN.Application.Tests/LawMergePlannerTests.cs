using System;
using System.Collections.Generic;
using FluentAssertions;
using TaxKeepVN.Application.Law.Merge;
using TaxKeepVN.Domain.Constants;
using TaxKeepVN.Domain.Entities.Law;
using Xunit;

namespace TaxKeepVN.Application.Tests
{
    public class LawMergePlannerTests
    {
        private readonly LawMergePlanner _planner = new();

        private readonly LawRuleDefinition _defPersonal = new()
        {
            RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_PERSONAL,
            RuleGroup = LawConstants.RuleGroup.DEDUCTION,
            ValueKind = LawConstants.ValueKind.AMOUNT,
            DefaultUnit = "VND/month",
            IsActive = true
        };

        private readonly LawRuleDefinition _defDependent = new()
        {
            RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_DEPENDENT,
            RuleGroup = LawConstants.RuleGroup.DEDUCTION,
            ValueKind = LawConstants.ValueKind.AMOUNT,
            DefaultUnit = "VND/person/month",
            IsActive = true
        };

        private readonly LawRuleDefinition _defDependentGroups = new()
        {
            RuleCode = LawConstants.RuleCodes.PIT_DEPENDENT_GROUPS,
            RuleGroup = LawConstants.RuleGroup.DEPENDENT,
            ValueKind = LawConstants.ValueKind.JSON,
            IsActive = true
        };

        [Fact]
        public void P17_MergePlanner_Detects_Orphans_On_REPLACES()
        {
            var oldDoc = new LegalDocument
            {
                Id = Guid.NewGuid(),
                DocumentNumber = "65/2013/ND-CP",
                NumberNormalized = "65/2013/ND-CP",
                DocumentType = LawConstants.DocumentType.NGHI_DINH
            };

            var newDoc = new LegalDocument
            {
                Id = Guid.NewGuid(),
                DocumentNumber = "253/2026/ND-CP",
                NumberNormalized = "253/2026/ND-CP",
                DocumentType = LawConstants.DocumentType.NGHI_DINH,
                EffectiveDate = new DateOnly(2026, 7, 1)
            };

            // Version of rule in old doc that is NOT mentioned in new changeset
            var orphanVersion = new LawRuleVersion
            {
                Id = Guid.NewGuid(),
                RuleCode = LawConstants.RuleCodes.PIT_DEPENDENT_GROUPS,
                DocumentId = oldDoc.Id,
                ApplyFrom = new DateOnly(2013, 7, 1),
                ApplyTo = null,
                RuleValue = "[{\"group\": \"CHILD\"}]"
            };

            var changeset = new LawChangeset
            {
                Id = Guid.NewGuid(),
                DocumentId = newDoc.Id,
                Document = newDoc,
                BaseRevisionNo = 1,
                Origin = LawConstants.ChangesetOrigin.MANUAL,
                Reason = "Demo changeset"
            };

            var rel = new LawChangesetRelation
            {
                Id = Guid.NewGuid(),
                ChangesetId = changeset.Id,
                RelationType = LawConstants.RelationType.REPLACES,
                TargetDocumentNumber = "65/2013/ND-CP",
                Decision = LawConstants.Decision.ACCEPTED
            };

            var opPersonal = new LawChangeOp
            {
                Id = Guid.NewGuid(),
                ChangesetId = changeset.Id,
                OpType = LawConstants.OpType.ADD,
                RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_PERSONAL,
                ApplyFrom = new DateOnly(2026, 7, 1),
                After = "{\"valueNumber\": 15500000, \"unit\": \"VND/month\"}",
                Article = "Điều 1",
                Decision = LawConstants.Decision.ACCEPTED
            };

            var input = new MergeInput
            {
                HeadRevisionNo = 1,
                ActiveVersions = new List<LawRuleVersion> { orphanVersion },
                AcceptedOps = new List<LawChangeOp> { opPersonal },
                AcceptedRelations = new List<LawChangesetRelation> { rel },
                AllOps = new List<LawChangeOp> { opPersonal },
                AllRelations = new List<LawChangesetRelation> { rel },
                Catalog = new List<LawRuleDefinition> { _defPersonal, _defDependentGroups },
                Documents = new List<LegalDocument> { oldDoc, newDoc },
                Changeset = changeset,
                Today = new DateOnly(2026, 7, 1)
            };

            var plan = _planner.Plan(input);

            plan.Orphans.Should().HaveCount(1);
            plan.Orphans[0].RuleCode.Should().Be(LawConstants.RuleCodes.PIT_DEPENDENT_GROUPS);
            plan.CanMerge.Should().BeFalse(); // Blocked by CHK-05
        }

        [Fact]
        public void P18_MergePlanner_Passes_When_Orphans_Resolved()
        {
            var oldDoc = new LegalDocument
            {
                Id = Guid.NewGuid(),
                DocumentNumber = "65/2013/ND-CP",
                NumberNormalized = "65/2013/ND-CP",
                DocumentType = LawConstants.DocumentType.NGHI_DINH
            };

            var newDoc = new LegalDocument
            {
                Id = Guid.NewGuid(),
                DocumentNumber = "253/2026/ND-CP",
                NumberNormalized = "253/2026/ND-CP",
                DocumentType = LawConstants.DocumentType.NGHI_DINH,
                EffectiveDate = new DateOnly(2026, 7, 1)
            };

            var orphanVersion = new LawRuleVersion
            {
                Id = Guid.NewGuid(),
                RuleCode = LawConstants.RuleCodes.PIT_DEPENDENT_GROUPS,
                DocumentId = oldDoc.Id,
                ApplyFrom = new DateOnly(2013, 7, 1),
                ApplyTo = null,
                RuleValue = "[{\"group\": \"CHILD\"}]"
            };

            var changeset = new LawChangeset
            {
                Id = Guid.NewGuid(),
                DocumentId = newDoc.Id,
                Document = newDoc,
                BaseRevisionNo = 1,
                Origin = LawConstants.ChangesetOrigin.MANUAL,
                Reason = "Demo changeset"
            };

            var rel = new LawChangesetRelation
            {
                Id = Guid.NewGuid(),
                ChangesetId = changeset.Id,
                RelationType = LawConstants.RelationType.REPLACES,
                TargetDocumentNumber = "65/2013/ND-CP",
                Decision = LawConstants.Decision.ACCEPTED
            };

            var opPersonal = new LawChangeOp
            {
                Id = Guid.NewGuid(),
                ChangesetId = changeset.Id,
                OpType = LawConstants.OpType.ADD,
                RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_PERSONAL,
                ApplyFrom = new DateOnly(2026, 7, 1),
                After = "{\"valueNumber\": 15500000, \"unit\": \"VND/month\"}",
                Article = "Điều 1",
                Page = 1,
                Decision = LawConstants.Decision.ACCEPTED
            };

            // Orphan resolved by KEEP
            var resolution = new LawOrphanResolution
            {
                Id = Guid.NewGuid(),
                ChangesetId = changeset.Id,
                VersionId = orphanVersion.Id,
                Action = LawConstants.OrphanAction.KEEP,
                Note = "Keep until new guidance"
            };

            var input = new MergeInput
            {
                HeadRevisionNo = 1,
                ActiveVersions = new List<LawRuleVersion> { orphanVersion },
                AcceptedOps = new List<LawChangeOp> { opPersonal },
                AcceptedRelations = new List<LawChangesetRelation> { rel },
                OrphanResolutions = new List<LawOrphanResolution> { resolution },
                AllOps = new List<LawChangeOp> { opPersonal },
                AllRelations = new List<LawChangesetRelation> { rel },
                Catalog = new List<LawRuleDefinition> { _defPersonal, _defDependentGroups },
                Documents = new List<LegalDocument> { oldDoc, newDoc },
                Changeset = changeset,
                Today = new DateOnly(2026, 7, 1)
            };

            var plan = _planner.Plan(input);

            plan.Orphans.Should().BeEmpty();
            plan.Checks.Where(c => c.Level == CheckLevel.BLOCK && !c.Passed).Select(c => c.Code + ": " + c.Message).Should().BeEmpty();
            plan.CanMerge.Should().BeTrue();
        }

        [Fact]
        public void S1_CHK15_Fails_When_BaseRevision_Stale()
        {
            var changeset = new LawChangeset
            {
                Id = Guid.NewGuid(),
                BaseRevisionNo = 1, // Stale! HEAD is 2
                Origin = LawConstants.ChangesetOrigin.MANUAL,
                Reason = "Demo"
            };

            var input = new MergeInput
            {
                HeadRevisionNo = 2,
                Changeset = changeset,
                Today = new DateOnly(2026, 1, 1)
            };

            var plan = _planner.Plan(input);

            plan.CanMerge.Should().BeFalse();
            plan.Checks.Should().Contain(c => c.Code == "CHK-15" && !c.Passed);
        }

        [Fact]
        public void S2_CHK07_Fails_When_ApplyTo_Before_ApplyFrom()
        {
            var changeset = new LawChangeset
            {
                Id = Guid.NewGuid(),
                BaseRevisionNo = 1,
                Origin = LawConstants.ChangesetOrigin.MANUAL,
                Reason = "Demo"
            };

            var op = new LawChangeOp
            {
                Id = Guid.NewGuid(),
                ChangesetId = changeset.Id,
                OpType = LawConstants.OpType.ADD,
                RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_PERSONAL,
                ApplyFrom = new DateOnly(2026, 7, 1),
                ApplyTo = new DateOnly(2026, 1, 1), // Invalid range!
                Article = "Điều 1",
                Decision = LawConstants.Decision.ACCEPTED
            };

            var input = new MergeInput
            {
                HeadRevisionNo = 1,
                Changeset = changeset,
                AcceptedOps = new List<LawChangeOp> { op },
                AllOps = new List<LawChangeOp> { op },
                Catalog = new List<LawRuleDefinition> { _defPersonal },
                Today = new DateOnly(2026, 1, 1)
            };

            var plan = _planner.Plan(input);

            plan.CanMerge.Should().BeFalse();
            plan.Checks.Should().Contain(c => c.Code == "CHK-07" && !c.Passed);
        }

        [Fact]
        public void S3_CHK02_Fails_When_AMOUNT_Is_Negative_Or_Invalid_JSON()
        {
            var changeset = new LawChangeset
            {
                Id = Guid.NewGuid(),
                BaseRevisionNo = 1,
                Origin = LawConstants.ChangesetOrigin.MANUAL,
                Reason = "Demo"
            };

            var op = new LawChangeOp
            {
                Id = Guid.NewGuid(),
                ChangesetId = changeset.Id,
                OpType = LawConstants.OpType.ADD,
                RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_PERSONAL,
                ApplyFrom = new DateOnly(2026, 1, 1),
                After = "{\"valueNumber\": -5000000, \"unit\": \"VND/month\"}", // Negative amount!
                Article = "Điều 1",
                Decision = LawConstants.Decision.ACCEPTED
            };

            var input = new MergeInput
            {
                HeadRevisionNo = 1,
                Changeset = changeset,
                AcceptedOps = new List<LawChangeOp> { op },
                AllOps = new List<LawChangeOp> { op },
                Catalog = new List<LawRuleDefinition> { _defPersonal },
                Today = new DateOnly(2026, 1, 1)
            };

            var plan = _planner.Plan(input);

            plan.CanMerge.Should().BeFalse();
            plan.Checks.Should().Contain(c => c.Code == "CHK-02" && !c.Passed);
        }

        [Fact]
        public void S4_CHK05_Fails_When_Orphans_Remain_Unhandled()
        {
            var oldDoc = new LegalDocument
            {
                Id = Guid.NewGuid(),
                DocumentNumber = "65/2013/ND-CP",
                NumberNormalized = "65/2013/ND-CP"
            };

            var newDoc = new LegalDocument
            {
                Id = Guid.NewGuid(),
                DocumentNumber = "253/2026/ND-CP",
                NumberNormalized = "253/2026/ND-CP",
                EffectiveDate = new DateOnly(2026, 7, 1)
            };

            var orphan = new LawRuleVersion
            {
                Id = Guid.NewGuid(),
                RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_DEPENDENT,
                DocumentId = oldDoc.Id,
                ApplyFrom = new DateOnly(2013, 7, 1),
                ApplyTo = null
            };

            var changeset = new LawChangeset
            {
                Id = Guid.NewGuid(),
                DocumentId = newDoc.Id,
                Document = newDoc,
                BaseRevisionNo = 1,
                Origin = LawConstants.ChangesetOrigin.MANUAL,
                Reason = "Demo"
            };

            var rel = new LawChangesetRelation
            {
                Id = Guid.NewGuid(),
                ChangesetId = changeset.Id,
                RelationType = LawConstants.RelationType.REPLACES,
                TargetDocumentNumber = "65/2013/ND-CP",
                Decision = LawConstants.Decision.ACCEPTED
            };

            var input = new MergeInput
            {
                HeadRevisionNo = 1,
                ActiveVersions = new List<LawRuleVersion> { orphan },
                AcceptedRelations = new List<LawChangesetRelation> { rel },
                AllRelations = new List<LawChangesetRelation> { rel },
                Catalog = new List<LawRuleDefinition> { _defDependent },
                Documents = new List<LegalDocument> { oldDoc, newDoc },
                Changeset = changeset,
                Today = new DateOnly(2026, 7, 1)
            };

            var plan = _planner.Plan(input);

            plan.CanMerge.Should().BeFalse();
            plan.Checks.Should().Contain(c => c.Code == "CHK-05" && !c.Passed);
        }
    }
}
