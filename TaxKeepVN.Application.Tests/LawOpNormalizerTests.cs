using System;
using System.Collections.Generic;
using FluentAssertions;
using TaxKeepVN.Application.Law.Normalizer;
using TaxKeepVN.Domain.Constants;
using TaxKeepVN.Domain.Entities.Law;
using Xunit;

namespace TaxKeepVN.Application.Tests
{
    public class LawOpNormalizerTests
    {
        private readonly LawOpNormalizer _normalizer = new();
        private readonly List<LawRuleDefinition> _catalog = new()
        {
            new LawRuleDefinition
            {
                RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_PERSONAL,
                RuleGroup = LawConstants.RuleGroup.DEDUCTION,
                ValueKind = LawConstants.ValueKind.AMOUNT,
                DefaultUnit = "VND/month",
                IsActive = true
            },
            new LawRuleDefinition
            {
                RuleCode = LawConstants.RuleCodes.PIT_WITHHOLD_CASUAL_RATE,
                RuleGroup = LawConstants.RuleGroup.WITHHOLDING,
                ValueKind = LawConstants.ValueKind.RATE,
                DefaultUnit = "%",
                IsActive = true
            }
        };

        private readonly LegalDocument _docNd = new()
        {
            Id = Guid.NewGuid(),
            DocumentNumber = "253/2026/ND-CP",
            DocumentType = LawConstants.DocumentType.NGHI_DINH
        };

        private readonly LegalDocument _docTt = new()
        {
            Id = Guid.NewGuid(),
            DocumentNumber = "111/2013/TT-BTC",
            DocumentType = LawConstants.DocumentType.THONG_TU
        };

        [Fact]
        public void P4_Normalizer_ADD_Becomes_UPDATE_When_Rule_Exists_With_Different_Value()
        {
            var active = new List<LawRuleVersion>
            {
                new LawRuleVersion
                {
                    Id = Guid.NewGuid(),
                    RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_PERSONAL,
                    ApplyFrom = new DateOnly(2020, 1, 1),
                    RuleValue = "{\"valueNumber\": 11000000, \"unit\": \"VND/month\"}",
                    DocumentId = _docNd.Id
                }
            };

            var changeset = new LawChangeset();
            var op = new LawChangeOp
            {
                OpType = LawConstants.OpType.ADD,
                RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_PERSONAL,
                ApplyFrom = new DateOnly(2026, 1, 1),
                After = "{\"valueNumber\": 15500000, \"unit\": \"VND/month\"}",
                Article = "Điều 1"
            };
            changeset.Ops.Add(op);

            _normalizer.Normalize(changeset, active, _catalog, _docNd);

            op.OpType.Should().Be(LawConstants.OpType.UPDATE);
            op.Flags.Should().Contain(LawConstants.OpFlag.ADD_TO_UPDATE);
        }

        [Fact]
        public void P5_Normalizer_ADD_Becomes_RECITE_When_Rule_Exists_With_Same_Value()
        {
            var active = new List<LawRuleVersion>
            {
                new LawRuleVersion
                {
                    Id = Guid.NewGuid(),
                    RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_PERSONAL,
                    ApplyFrom = new DateOnly(2020, 1, 1),
                    RuleValue = "{\"valueNumber\": 11000000, \"unit\": \"VND/month\"}",
                    DocumentId = _docNd.Id
                }
            };

            var changeset = new LawChangeset();
            var op = new LawChangeOp
            {
                OpType = LawConstants.OpType.ADD,
                RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_PERSONAL,
                ApplyFrom = new DateOnly(2026, 1, 1),
                After = "{\"valueNumber\": 11000000, \"unit\": \"VND/month\"}",
                Article = "Điều 1"
            };
            changeset.Ops.Add(op);

            _normalizer.Normalize(changeset, active, _catalog, _docTt);

            op.OpType.Should().Be(LawConstants.OpType.RECITE);
            op.Flags.Should().Contain(LawConstants.OpFlag.ADD_TO_RECITE);
        }

        [Fact]
        public void P6_Normalizer_UPDATE_Becomes_ADD_When_Rule_Does_Not_Exist()
        {
            var active = new List<LawRuleVersion>(); // empty

            var changeset = new LawChangeset();
            var op = new LawChangeOp
            {
                OpType = LawConstants.OpType.UPDATE,
                RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_PERSONAL,
                ApplyFrom = new DateOnly(2026, 1, 1),
                After = "{\"valueNumber\": 15500000, \"unit\": \"VND/month\"}",
                Article = "Điều 1"
            };
            changeset.Ops.Add(op);

            _normalizer.Normalize(changeset, active, _catalog, _docNd);

            op.OpType.Should().Be(LawConstants.OpType.ADD);
            op.Flags.Should().Contain(LawConstants.OpFlag.UPDATE_TO_ADD);
        }

        [Fact]
        public void P7_Normalizer_UPDATE_Becomes_RECITE_When_Rule_Exists_With_Same_Value_Different_Doc()
        {
            var active = new List<LawRuleVersion>
            {
                new LawRuleVersion
                {
                    Id = Guid.NewGuid(),
                    RuleCode = LawConstants.RuleCodes.PIT_WITHHOLD_CASUAL_RATE,
                    ApplyFrom = new DateOnly(2020, 1, 1),
                    RuleValue = "{\"rate\": 0.1, \"unit\": \"%\"}",
                    DocumentId = _docNd.Id
                }
            };

            var changeset = new LawChangeset();
            var op = new LawChangeOp
            {
                OpType = LawConstants.OpType.UPDATE,
                RuleCode = LawConstants.RuleCodes.PIT_WITHHOLD_CASUAL_RATE,
                ApplyFrom = new DateOnly(2026, 1, 1),
                After = "{\"rate\": 0.1, \"unit\": \"%\"}",
                Article = "Điều 2"
            };
            changeset.Ops.Add(op);

            _normalizer.Normalize(changeset, active, _catalog, _docTt);

            op.OpType.Should().Be(LawConstants.OpType.RECITE);
            op.Flags.Should().Contain(LawConstants.OpFlag.UPDATE_TO_RECITE);
        }

        [Fact]
        public void P8_Normalizer_RECITE_Becomes_ADD_When_Rule_Does_Not_Exist()
        {
            var active = new List<LawRuleVersion>();

            var changeset = new LawChangeset();
            var op = new LawChangeOp
            {
                OpType = LawConstants.OpType.RECITE,
                RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_PERSONAL,
                ApplyFrom = new DateOnly(2026, 1, 1),
                After = "{\"valueNumber\": 15500000, \"unit\": \"VND/month\"}",
                Article = "Điều 1"
            };
            changeset.Ops.Add(op);

            _normalizer.Normalize(changeset, active, _catalog, _docNd);

            op.OpType.Should().Be(LawConstants.OpType.ADD);
            op.Flags.Should().Contain(LawConstants.OpFlag.RECITE_TO_ADD);
        }

        [Fact]
        public void P9_Normalizer_RECITE_Becomes_UPDATE_When_Rule_Exists_With_Different_Value()
        {
            var active = new List<LawRuleVersion>
            {
                new LawRuleVersion
                {
                    Id = Guid.NewGuid(),
                    RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_PERSONAL,
                    ApplyFrom = new DateOnly(2020, 1, 1),
                    RuleValue = "{\"valueNumber\": 11000000, \"unit\": \"VND/month\"}",
                    DocumentId = _docNd.Id
                }
            };

            var changeset = new LawChangeset();
            var op = new LawChangeOp
            {
                OpType = LawConstants.OpType.RECITE,
                RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_PERSONAL,
                ApplyFrom = new DateOnly(2026, 1, 1),
                After = "{\"valueNumber\": 15500000, \"unit\": \"VND/month\"}",
                Article = "Điều 1"
            };
            changeset.Ops.Add(op);

            _normalizer.Normalize(changeset, active, _catalog, _docNd);

            op.OpType.Should().Be(LawConstants.OpType.UPDATE);
            op.Flags.Should().Contain(LawConstants.OpFlag.RECITE_TO_UPDATE);
        }

        [Fact]
        public void P10_Normalizer_Flags_LOWER_LEVEL_RECITE()
        {
            var active = new List<LawRuleVersion>
            {
                new LawRuleVersion
                {
                    Id = Guid.NewGuid(),
                    RuleCode = LawConstants.RuleCodes.PIT_WITHHOLD_CASUAL_RATE,
                    ApplyFrom = new DateOnly(2020, 1, 1),
                    RuleValue = "{\"rate\": 0.1, \"unit\": \"%\"}",
                    DocumentId = _docNd.Id,
                    Document = _docNd
                }
            };

            var changeset = new LawChangeset();
            var op = new LawChangeOp
            {
                OpType = LawConstants.OpType.RECITE,
                RuleCode = LawConstants.RuleCodes.PIT_WITHHOLD_CASUAL_RATE,
                ApplyFrom = new DateOnly(2026, 1, 1),
                After = "{\"rate\": 0.1, \"unit\": \"%\"}",
                Article = "Điều 5"
            };
            changeset.Ops.Add(op);

            // Document is THONG_TU (level 5), reciting NGHI_DINH (level 3)
            _normalizer.Normalize(changeset, active, _catalog, _docTt);

            op.Flags.Should().Contain(LawConstants.OpFlag.LOWER_LEVEL_RECITE);
        }

        [Fact]
        public void P11_Normalizer_Flags_NOTHING_TO_END()
        {
            var active = new List<LawRuleVersion>(); // No rule active

            var changeset = new LawChangeset();
            var op = new LawChangeOp
            {
                OpType = LawConstants.OpType.END,
                RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_PERSONAL,
                ApplyTo = new DateOnly(2026, 1, 1),
                Article = "Điều 10"
            };
            changeset.Ops.Add(op);

            _normalizer.Normalize(changeset, active, _catalog, _docNd);

            op.Flags.Should().Contain(LawConstants.OpFlag.NOTHING_TO_END);
            op.Decision.Should().Be(LawConstants.Decision.REJECTED);
        }

        [Fact]
        public void P12_Normalizer_Flags_DUPLICATE_OP()
        {
            var active = new List<LawRuleVersion>();

            var changeset = new LawChangeset();
            var op1 = new LawChangeOp
            {
                OpType = LawConstants.OpType.ADD,
                RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_PERSONAL,
                ApplyFrom = new DateOnly(2026, 1, 1),
                After = "{\"valueNumber\": 15500000, \"unit\": \"VND/month\"}",
                Article = "Điều 1"
            };
            var op2 = new LawChangeOp
            {
                OpType = LawConstants.OpType.ADD,
                RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_PERSONAL,
                ApplyFrom = new DateOnly(2026, 1, 1),
                After = "{\"valueNumber\": 15500000, \"unit\": \"VND/month\"}",
                Article = "Điều 2"
            };
            changeset.Ops.Add(op1);
            changeset.Ops.Add(op2);

            _normalizer.Normalize(changeset, active, _catalog, _docNd);

            op1.Flags.Should().Contain(LawConstants.OpFlag.DUPLICATE_OP);
            op2.Flags.Should().Contain(LawConstants.OpFlag.DUPLICATE_OP);
        }

        [Fact]
        public void P13_Normalizer_Populates_Before_Snapshot()
        {
            var active = new List<LawRuleVersion>
            {
                new LawRuleVersion
                {
                    Id = Guid.NewGuid(),
                    RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_PERSONAL,
                    ApplyFrom = new DateOnly(2020, 1, 1),
                    RuleValue = "{\"valueNumber\": 11000000, \"unit\": \"VND/month\"}",
                    DocumentId = _docNd.Id,
                    Article = "Điều 1",
                    Clause = "Khoản 1"
                }
            };

            var changeset = new LawChangeset();
            var op = new LawChangeOp
            {
                OpType = LawConstants.OpType.UPDATE,
                RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_PERSONAL,
                ApplyFrom = new DateOnly(2026, 1, 1),
                After = "{\"valueNumber\": 15500000, \"unit\": \"VND/month\"}",
                Article = "Điều 1"
            };
            changeset.Ops.Add(op);

            _normalizer.Normalize(changeset, active, _catalog, _docNd);

            op.Before.Should().NotBeNullOrEmpty();
            op.Before.Should().Contain("11000000");
            op.Before.Should().Contain("2020-01-01");
        }
    }
}
