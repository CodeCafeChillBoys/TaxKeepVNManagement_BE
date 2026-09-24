using System;
using System.Collections.Generic;
using System.Text.Json;

namespace TaxKeepVN.Application.DTOs.Law
{
    public class RuleHistoryItemDto
    {
        public Guid Id { get; set; }
        public decimal? ValueNumber { get; set; }
        public JsonElement? ValueJson { get; set; }
        public string? ValueText { get; set; }
        public string? Unit { get; set; }
        public JsonElement? Condition { get; set; }
        public string? ConditionText { get; set; }
        public DateOnly ApplyFrom { get; set; }
        public DateOnly? ApplyTo { get; set; }
        public int CreatedInRevision { get; set; }
        public int? SupersededInRevision { get; set; }
        public Guid? DerivedFromVersionId { get; set; }
        public Guid? SourceOpId { get; set; }
        public EffectiveCitationDto Citation { get; set; } = new();
    }

    public class RuleHistoryDto
    {
        public string RuleCode { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string ValueKind { get; set; } = string.Empty;
        public List<RuleHistoryItemDto> Versions { get; set; } = new();
    }
}
