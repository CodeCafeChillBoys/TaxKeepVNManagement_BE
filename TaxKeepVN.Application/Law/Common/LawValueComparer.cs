using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using TaxKeepVN.Domain.Constants;

namespace TaxKeepVN.Application.Law.Common
{
    public static class LawValueComparer
    {
        public static bool IsValidValue(string? jsonValue, string valueKind)
        {
            if (string.IsNullOrWhiteSpace(jsonValue))
                return false;

            try
            {
                using var doc = JsonDocument.Parse(jsonValue);
                var root = doc.RootElement;

                switch (valueKind.ToUpperInvariant())
                {
                    case LawConstants.ValueKind.AMOUNT:
                        decimal amount = ExtractNumber(root);
                        return amount >= 0;

                    case LawConstants.ValueKind.RATE:
                        decimal rate = ExtractNumber(root);
                        return rate >= 0 && rate <= 1;

                    case LawConstants.ValueKind.SCHEDULE:
                        return ValidateSchedule(root);

                    case LawConstants.ValueKind.JSON:
                        return root.ValueKind == JsonValueKind.Object || root.ValueKind == JsonValueKind.Array;

                    case LawConstants.ValueKind.FLAG:
                    case LawConstants.ValueKind.TEXT:
                        string text = ExtractText(root);
                        return !string.IsNullOrWhiteSpace(text);

                    default:
                        return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool AreValuesEqual(string? v1, string? v2, string? valueKind)
        {
            if (string.IsNullOrWhiteSpace(v1) && string.IsNullOrWhiteSpace(v2))
                return true;
            if (string.IsNullOrWhiteSpace(v1) || string.IsNullOrWhiteSpace(v2))
                return false;

            try
            {
                using var doc1 = JsonDocument.Parse(v1);
                using var doc2 = JsonDocument.Parse(v2);
                var r1 = doc1.RootElement;
                var r2 = doc2.RootElement;

                // Compare conditions if present in either
                bool hasCond1 = TryGetProperty(r1, "condition", out var cond1) && cond1.ValueKind != JsonValueKind.Null;
                bool hasCond2 = TryGetProperty(r2, "condition", out var cond2) && cond2.ValueKind != JsonValueKind.Null;

                if (hasCond1 && hasCond2)
                {
                    if (!AreJsonElementsEqual(cond1, cond2))
                        return false;
                }
                else if (hasCond1 != hasCond2)
                {
                    return false;
                }

                switch (valueKind?.ToUpperInvariant())
                {
                    case LawConstants.ValueKind.AMOUNT:
                    case LawConstants.ValueKind.RATE:
                        decimal n1 = ExtractNumber(r1);
                        decimal n2 = ExtractNumber(r2);
                        if (n1 != n2)
                            return false;

                        string u1 = ExtractUnit(r1);
                        string u2 = ExtractUnit(r2);
                        return string.Equals(u1, u2, StringComparison.OrdinalIgnoreCase);

                    case LawConstants.ValueKind.SCHEDULE:
                        return AreSchedulesEqual(r1, r2);

                    case LawConstants.ValueKind.FLAG:
                    case LawConstants.ValueKind.TEXT:
                        string t1 = NormalizeText(ExtractText(r1));
                        string t2 = NormalizeText(ExtractText(r2));
                        return string.Equals(t1, t2, StringComparison.Ordinal);

                    case LawConstants.ValueKind.JSON:
                    default:
                        return AreJsonElementsEqual(r1, r2);
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool ValidateSchedule(JsonElement root)
        {
            JsonElement brackets = root;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("brackets", out var bProp))
            {
                brackets = bProp;
            }

            if (brackets.ValueKind != JsonValueKind.Array)
                return false;

            int count = brackets.GetArrayLength();
            if (count < 1)
                return false;

            decimal prevToAnnual = -1;
            decimal prevRate = -1;

            for (int i = 0; i < count; i++)
            {
                var item = brackets[i];
                if (item.ValueKind != JsonValueKind.Object)
                    return false;

                // rate validation
                if (!item.TryGetProperty("rate", out var rProp) || !rProp.TryGetDecimal(out var rate))
                    return false;

                if (rate < 0 || rate > 1)
                    return false;

                if (rate < prevRate)
                    return false; // non-decreasing

                prevRate = rate;

                // toAnnual validation
                bool isLast = (i == count - 1);
                if (item.TryGetProperty("toAnnual", out var toProp))
                {
                    if (toProp.ValueKind == JsonValueKind.Null)
                    {
                        if (!isLast)
                            return false; // null only allowed on last bracket
                    }
                    else if (toProp.TryGetDecimal(out var toAnnual))
                    {
                        if (toAnnual <= prevToAnnual)
                            return false; // strictly ascending
                        prevToAnnual = toAnnual;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    if (!isLast)
                        return false;
                }
            }

            return true;
        }

        private static bool AreSchedulesEqual(JsonElement r1, JsonElement r2)
        {
            JsonElement b1 = (r1.ValueKind == JsonValueKind.Object && r1.TryGetProperty("brackets", out var p1)) ? p1 : r1;
            JsonElement b2 = (r2.ValueKind == JsonValueKind.Object && r2.TryGetProperty("brackets", out var p2)) ? p2 : r2;

            if (b1.ValueKind != JsonValueKind.Array || b2.ValueKind != JsonValueKind.Array)
                return false;

            if (b1.GetArrayLength() != b2.GetArrayLength())
                return false;

            for (int i = 0; i < b1.GetArrayLength(); i++)
            {
                var item1 = b1[i];
                var item2 = b2[i];

                decimal rRate1 = item1.GetProperty("rate").GetDecimal();
                decimal rRate2 = item2.GetProperty("rate").GetDecimal();
                if (rRate1 != rRate2)
                    return false;

                bool hasTo1 = item1.TryGetProperty("toAnnual", out var toProp1) && toProp1.ValueKind != JsonValueKind.Null;
                bool hasTo2 = item2.TryGetProperty("toAnnual", out var toProp2) && toProp2.ValueKind != JsonValueKind.Null;

                if (hasTo1 != hasTo2)
                    return false;

                if (hasTo1 && toProp1.GetDecimal() != toProp2.GetDecimal())
                    return false;
            }

            return true;
        }

        public static decimal ExtractNumber(JsonElement root)
        {
            if (root.ValueKind == JsonValueKind.Number)
                return root.GetDecimal();

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("valueNumber", out var v1) && v1.TryGetDecimal(out var d1))
                    return d1;
                if (root.TryGetProperty("value", out var v2) && v2.TryGetDecimal(out var d2))
                    return d2;
                if (root.TryGetProperty("amount", out var v3) && v3.TryGetDecimal(out var d3))
                    return d3;
                if (root.TryGetProperty("rate", out var v4) && v4.TryGetDecimal(out var d4))
                    return d4;
            }

            if (root.ValueKind == JsonValueKind.String && decimal.TryParse(root.GetString(), out var parsed))
                return parsed;

            return 0;
        }

        public static string ExtractUnit(JsonElement root)
        {
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("unit", out var u) && u.ValueKind == JsonValueKind.String)
                return u.GetString()?.Trim() ?? string.Empty;

            return string.Empty;
        }

        public static string ExtractText(JsonElement root)
        {
            if (root.ValueKind == JsonValueKind.String)
                return root.GetString() ?? string.Empty;

            if (root.ValueKind == JsonValueKind.True)
                return "true";
            if (root.ValueKind == JsonValueKind.False)
                return "false";

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("valueText", out var vt) && vt.ValueKind == JsonValueKind.String)
                    return vt.GetString() ?? string.Empty;
                if (root.TryGetProperty("value", out var v))
                    return v.ToString();
            }

            return root.GetRawText();
        }

        private static string NormalizeText(string s)
        {
            return Regex.Replace(s.Trim(), @"\s+", " ");
        }

        private static bool TryGetProperty(JsonElement element, string propName, out JsonElement prop)
        {
            if (element.ValueKind == JsonValueKind.Object)
                return element.TryGetProperty(propName, out prop);

            prop = default;
            return false;
        }

        private static bool AreJsonElementsEqual(JsonElement e1, JsonElement e2)
        {
            if (e1.ValueKind != e2.ValueKind)
                return false;

            switch (e1.ValueKind)
            {
                case JsonValueKind.Object:
                    var props1 = new Dictionary<string, JsonElement>();
                    foreach (var prop in e1.EnumerateObject())
                        props1[prop.Name] = prop.Value;

                    var props2 = new Dictionary<string, JsonElement>();
                    foreach (var prop in e2.EnumerateObject())
                        props2[prop.Name] = prop.Value;

                    if (props1.Count != props2.Count)
                        return false;

                    foreach (var kvp in props1)
                    {
                        if (!props2.TryGetValue(kvp.Key, out var val2) || !AreJsonElementsEqual(kvp.Value, val2))
                            return false;
                    }
                    return true;

                case JsonValueKind.Array:
                    if (e1.GetArrayLength() != e2.GetArrayLength())
                        return false;

                    for (int i = 0; i < e1.GetArrayLength(); i++)
                    {
                        if (!AreJsonElementsEqual(e1[i], e2[i]))
                            return false;
                    }
                    return true;

                case JsonValueKind.Number:
                    return e1.GetDecimal() == e2.GetDecimal();

                case JsonValueKind.String:
                    return string.Equals(e1.GetString(), e2.GetString(), StringComparison.Ordinal);

                case JsonValueKind.True:
                case JsonValueKind.False:
                    return e1.GetBoolean() == e2.GetBoolean();

                case JsonValueKind.Null:
                    return true;

                default:
                    return e1.GetRawText() == e2.GetRawText();
            }
        }
    }
}
