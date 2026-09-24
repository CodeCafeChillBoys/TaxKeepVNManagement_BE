using System;
using System.Text.Json;
using TaxKeepVN.Domain.Entities.Law;

namespace TaxKeepVN.Application.DTOs.Law
{
    public class RuleValueSnapshot
    {
        public Guid? VersionId { get; set; }
        public decimal? ValueNumber { get; set; }
        public JsonElement? ValueJson { get; set; }
        public string? ValueText { get; set; }
        public string? Unit { get; set; }
        public JsonElement? Condition { get; set; }
        public string? ConditionText { get; set; }
        public DateOnly? ApplyFrom { get; set; }
        public DateOnly? ApplyTo { get; set; }
        public string? DocumentNumber { get; set; }
        public string? Article { get; set; }
        public string? Clause { get; set; }
        public string? Point { get; set; }
        public int? Page { get; set; }

        public static RuleValueSnapshot FromVersion(LawRuleVersion v)
        {
            decimal? valueNumber = null;
            JsonElement? valueJson = null;
            string? valueText = null;
            string? unit = null;
            JsonElement? condition = null;
            string? conditionText = null;

            if (!string.IsNullOrWhiteSpace(v.RuleValue))
            {
                try
                {
                    using var doc = JsonDocument.Parse(v.RuleValue);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("valueNumber", out var vn) && vn.ValueKind == JsonValueKind.Number)
                        valueNumber = vn.GetDecimal();
                    if (root.TryGetProperty("valueJson", out var vj) && vj.ValueKind != JsonValueKind.Null && vj.ValueKind != JsonValueKind.Undefined)
                        valueJson = vj.Clone();
                    if (root.TryGetProperty("valueText", out var vt) && vt.ValueKind == JsonValueKind.String)
                        valueText = vt.GetString();
                    if (root.TryGetProperty("unit", out var u) && u.ValueKind == JsonValueKind.String)
                        unit = u.GetString();
                    if (root.TryGetProperty("condition", out var c) && c.ValueKind != JsonValueKind.Null && c.ValueKind != JsonValueKind.Undefined)
                        condition = c.Clone();
                    if (root.TryGetProperty("conditionText", out var ct) && ct.ValueKind == JsonValueKind.String)
                        conditionText = ct.GetString();
                }
                catch { }
            }

            return new RuleValueSnapshot
            {
                VersionId = v.Id,
                ValueNumber = valueNumber,
                ValueJson = valueJson,
                ValueText = valueText,
                Unit = unit,
                Condition = condition,
                ConditionText = conditionText,
                ApplyFrom = v.ApplyFrom,
                ApplyTo = v.ApplyTo,
                DocumentNumber = v.Document?.DocumentNumber,
                Article = v.Article,
                Clause = v.Clause,
                Point = v.Point,
                Page = v.Page
            };
        }
    }
}
