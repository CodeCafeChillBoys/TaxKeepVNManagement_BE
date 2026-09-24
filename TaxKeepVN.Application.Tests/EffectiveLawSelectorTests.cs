using System;
using System.Collections.Generic;
using FluentAssertions;
using TaxKeepVN.Application.Law.Selector;
using TaxKeepVN.Domain.Constants;
using TaxKeepVN.Domain.Entities.Law;
using Xunit;

namespace TaxKeepVN.Application.Tests
{
    public class EffectiveLawSelectorTests
    {
        private readonly EffectiveLawSelector _selector = new();

        [Fact]
        public void P14_SelectAt_PointInTime_Lookup()
        {
            var versions = new List<LawRuleVersion>
            {
                new LawRuleVersion
                {
                    Id = Guid.NewGuid(),
                    RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_PERSONAL,
                    ApplyFrom = new DateOnly(2013, 7, 1),
                    ApplyTo = new DateOnly(2020, 1, 1),
                    RuleValue = "{\"valueNumber\": 9000000}"
                },
                new LawRuleVersion
                {
                    Id = Guid.NewGuid(),
                    RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_PERSONAL,
                    ApplyFrom = new DateOnly(2020, 1, 1),
                    ApplyTo = new DateOnly(2026, 1, 1),
                    RuleValue = "{\"valueNumber\": 11000000}"
                },
                new LawRuleVersion
                {
                    Id = Guid.NewGuid(),
                    RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_PERSONAL,
                    ApplyFrom = new DateOnly(2026, 1, 1),
                    ApplyTo = null,
                    RuleValue = "{\"valueNumber\": 15500000}"
                }
            };

            var v2015 = _selector.SelectRuleAt(LawConstants.RuleCodes.PIT_DEDUCTION_PERSONAL, new DateOnly(2015, 6, 1), versions);
            v2015.Should().NotBeNull();
            v2015!.RuleValue.Should().Contain("9000000");

            var v2021 = _selector.SelectRuleAt(LawConstants.RuleCodes.PIT_DEDUCTION_PERSONAL, new DateOnly(2021, 6, 1), versions);
            v2021.Should().NotBeNull();
            v2021!.RuleValue.Should().Contain("11000000");

            var v2026 = _selector.SelectRuleAt(LawConstants.RuleCodes.PIT_DEDUCTION_PERSONAL, new DateOnly(2026, 7, 1), versions);
            v2026.Should().NotBeNull();
            v2026!.RuleValue.Should().Contain("15500000");
        }

        [Fact]
        public void P15_SelectForTaxYear_MidYearChange_Detected()
        {
            var versions = new List<LawRuleVersion>
            {
                new LawRuleVersion
                {
                    Id = Guid.NewGuid(),
                    RuleCode = LawConstants.RuleCodes.PIT_WITHHOLD_CASUAL_MIN_PAYMENT,
                    ApplyFrom = new DateOnly(2020, 1, 1),
                    ApplyTo = new DateOnly(2026, 7, 1),
                    RuleValue = "{\"amount\": 2000000}"
                },
                new LawRuleVersion
                {
                    Id = Guid.NewGuid(),
                    RuleCode = LawConstants.RuleCodes.PIT_WITHHOLD_CASUAL_MIN_PAYMENT,
                    ApplyFrom = new DateOnly(2026, 7, 1),
                    ApplyTo = null,
                    RuleValue = "{\"amount\": 5000000}"
                }
            };

            var definitions = new List<LawRuleDefinition>();

            var result = _selector.SelectForTaxYear(2026, versions, definitions);

            result.HasMidYearChange.Should().BeTrue();
            result.MidYearChanges.Should().HaveCount(1);
            result.MidYearChanges[0].RuleCode.Should().Be(LawConstants.RuleCodes.PIT_WITHHOLD_CASUAL_MIN_PAYMENT);
            result.MidYearChanges[0].Segments.Should().HaveCount(2);
            result.EffectiveRules[LawConstants.RuleCodes.PIT_WITHHOLD_CASUAL_MIN_PAYMENT].RuleValue.Should().Contain("5000000");
        }

        [Fact]
        public void P16_SelectForTaxYear_MissingRequiredRules_Detected()
        {
            var versions = new List<LawRuleVersion>
            {
                new LawRuleVersion
                {
                    Id = Guid.NewGuid(),
                    RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_PERSONAL,
                    ApplyFrom = new DateOnly(2026, 1, 1),
                    RuleValue = "{\"valueNumber\": 15500000}"
                }
            };

            var definitions = new List<LawRuleDefinition>
            {
                new LawRuleDefinition
                {
                    RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_PERSONAL,
                    RequiredForFlow3 = true
                },
                new LawRuleDefinition
                {
                    RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_DEPENDENT,
                    RequiredForFlow3 = true
                },
                new LawRuleDefinition
                {
                    RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_MEDICAL,
                    RequiredForFlow3 = true,
                    RequiredFromTaxYear = 2026
                },
                new LawRuleDefinition
                {
                    RuleCode = LawConstants.RuleCodes.PIT_DEDUCTION_VOLUNTARY_INSURANCE_CAP,
                    RequiredForFlow3 = false
                }
            };

            var result = _selector.SelectForTaxYear(2026, versions, definitions);

            result.MissingRequiredRules.Should().Contain(LawConstants.RuleCodes.PIT_DEDUCTION_DEPENDENT);
            result.MissingRequiredRules.Should().Contain(LawConstants.RuleCodes.PIT_DEDUCTION_MEDICAL);
            result.MissingRequiredRules.Should().NotContain(LawConstants.RuleCodes.PIT_DEDUCTION_PERSONAL);
            result.MissingRequiredRules.Should().NotContain(LawConstants.RuleCodes.PIT_DEDUCTION_VOLUNTARY_INSURANCE_CAP);
        }
    }
}
