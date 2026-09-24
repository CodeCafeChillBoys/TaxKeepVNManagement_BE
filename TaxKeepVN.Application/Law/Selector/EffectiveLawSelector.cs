using System;
using System.Collections.Generic;
using System.Linq;
using TaxKeepVN.Domain.Entities.Law;

namespace TaxKeepVN.Application.Law.Selector
{
    public class TaxYearLawSelectionResult
    {
        public int TaxYear { get; set; }
        public Dictionary<string, LawRuleVersion> EffectiveRules { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<MidYearChangeInfo> MidYearChanges { get; set; } = new();
        public List<string> MissingRequiredRules { get; set; } = new();
        public bool HasMidYearChange => MidYearChanges.Count > 0;
    }

    public class MidYearChangeInfo
    {
        public string RuleCode { get; set; } = string.Empty;
        public List<LawRuleVersion> Segments { get; set; } = new();
    }

    public class EffectiveLawSelector
    {
        public IReadOnlyList<LawRuleVersion> SelectAt(DateOnly date, IReadOnlyList<LawRuleVersion> versions)
        {
            return versions
                .Where(v => v.ApplyFrom <= date && (!v.ApplyTo.HasValue || v.ApplyTo.Value > date))
                .ToList();
        }

        public LawRuleVersion? SelectRuleAt(string ruleCode, DateOnly date, IReadOnlyList<LawRuleVersion> versions)
        {
            return versions
                .FirstOrDefault(v => string.Equals(v.RuleCode, ruleCode, StringComparison.OrdinalIgnoreCase)
                                     && v.ApplyFrom <= date
                                     && (!v.ApplyTo.HasValue || v.ApplyTo.Value > date));
        }

        public TaxYearLawSelectionResult SelectForTaxYear(
            int taxYear,
            IReadOnlyList<LawRuleVersion> stateVersions,
            IReadOnlyList<LawRuleDefinition> definitions)
        {
            var result = new TaxYearLawSelectionResult { TaxYear = taxYear };
            var yearStart = new DateOnly(taxYear, 1, 1);
            var yearEnd = new DateOnly(taxYear, 12, 31);

            // Group by RuleCode
            var grouped = stateVersions
                .GroupBy(v => v.RuleCode, StringComparer.OrdinalIgnoreCase);

            foreach (var group in grouped)
            {
                string ruleCode = group.Key;

                // Effective version at Dec 31
                var effectiveAtEnd = group.FirstOrDefault(v =>
                    v.ApplyFrom <= yearEnd && (!v.ApplyTo.HasValue || v.ApplyTo.Value > yearEnd));

                if (effectiveAtEnd != null)
                {
                    result.EffectiveRules[ruleCode] = effectiveAtEnd;
                }

                // Overlapping versions in the tax year [yearStart, yearEnd]
                var overlapping = group.Where(v =>
                    v.ApplyFrom <= yearEnd && (!v.ApplyTo.HasValue || v.ApplyTo.Value >= yearStart))
                    .OrderBy(v => v.ApplyFrom)
                    .ToList();

                if (overlapping.Count > 1)
                {
                    result.MidYearChanges.Add(new MidYearChangeInfo
                    {
                        RuleCode = ruleCode,
                        Segments = overlapping
                    });
                }
            }

            // Check missing required rules for Flow 3
            foreach (var def in definitions)
            {
                if (def.RequiredForFlow3 && (!def.RequiredFromTaxYear.HasValue || def.RequiredFromTaxYear.Value <= taxYear))
                {
                    if (!result.EffectiveRules.ContainsKey(def.RuleCode))
                    {
                        result.MissingRequiredRules.Add(def.RuleCode);
                    }
                }
            }

            return result;
        }
    }
}
