using System;
using System.Collections.Generic;
using System.Text.Json;

namespace TaxKeepVN.Application.DTOs.Law
{
    public class EffectiveCitationDto
    {
        public Guid DocumentId { get; set; }
        public string DocumentNumber { get; set; } = string.Empty;
        public string? Article { get; set; }
        public string? Clause { get; set; }
        public string? Point { get; set; }
        public int? Page { get; set; }
    }

    public class EffectiveRuleItemDto
    {
        public string RuleCode { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string RuleGroup { get; set; } = string.Empty;
        public string ValueKind { get; set; } = string.Empty;
        public decimal? ValueNumber { get; set; }
        public JsonElement? ValueJson { get; set; }
        public string? ValueText { get; set; }
        public string? Unit { get; set; }
        public JsonElement? Condition { get; set; }
        public string? ConditionText { get; set; }
        public DateOnly ApplyFrom { get; set; }
        public DateOnly? ApplyTo { get; set; }
        public Guid VersionId { get; set; }
        public int CreatedInRevision { get; set; }
        public EffectiveCitationDto Citation { get; set; } = new();
    }

    public class EffectiveDocumentDto
    {
        public Guid Id { get; set; }
        public string DocumentNumber { get; set; } = string.Empty;
        public string? DocumentType { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateOnly? IssuedDate { get; set; }
        public DateOnly? EffectiveDate { get; set; }
        public string? SourceUrl { get; set; }
        public string? FileUrl { get; set; }
        public string LegalStatus { get; set; } = string.Empty;
    }

    public class EffectiveWarningDto
    {
        public string Code { get; set; } = string.Empty;
        public string RuleCode { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public List<Guid> VersionIds { get; set; } = new();
    }

    public class EffectiveLawDto
    {
        public int TaxYear { get; set; }
        public int RevisionNo { get; set; }
        public List<EffectiveRuleItemDto> Rules { get; set; } = new();
        public List<EffectiveDocumentDto> Documents { get; set; } = new();
        public List<EffectiveWarningDto> Warnings { get; set; } = new();
        public List<string> MissingRequired { get; set; } = new();
    }
}
