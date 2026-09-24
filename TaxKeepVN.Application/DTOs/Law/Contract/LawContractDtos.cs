using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TaxKeepVN.Application.DTOs.Law.Contract
{
    public class CoverageDto
    {
        public int? TotalPages { get; set; }
        public int? PagesRead { get; set; }
        public string? Mode { get; set; }
        public int Chunks { get; set; } = 1;
    }

    public class DocumentDto
    {
        public string? DocumentNumber { get; set; }
        public string? DocumentType { get; set; }
        public string? Issuer { get; set; }
        public string? Title { get; set; }
        public DateOnly? IssuedDate { get; set; }
        public DateOnly? EffectiveDate { get; set; }
        public int? EvidencePage { get; set; }
    }

    public class CitationDto
    {
        public string? DocumentNumber { get; set; }
        public string? Article { get; set; }
        public string? Clause { get; set; }
        public string? Point { get; set; }
        public int? Page { get; set; }
    }

    public class RelationDto
    {
        public string? RelationKey { get; set; }
        public string Type { get; set; } = string.Empty;
        public string TargetDocumentNumber { get; set; } = string.Empty;
        public JsonElement? TargetScope { get; set; }
        public DateOnly? EffectiveDate { get; set; }
        public CitationDto? Citation { get; set; }
        public string? Evidence { get; set; }
        public string? Note { get; set; }
    }

    public class OperationDto
    {
        public string? OpKey { get; set; }
        public string Op { get; set; } = string.Empty;
        public string RuleCode { get; set; } = string.Empty;
        public bool NewCode { get; set; }
        public JsonElement? ProposedDefinition { get; set; }
        public JsonElement? After { get; set; }
        public DateOnly? ApplyFrom { get; set; }
        public DateOnly? ApplyTo { get; set; }
        public CitationDto? ApplyBasis { get; set; }
        public CitationDto? Citation { get; set; }
        public string? Evidence { get; set; }
        public double? Confidence { get; set; }
        public string? Rationale { get; set; }
    }

    public class ReferenceDto
    {
        public string? Text { get; set; }
        public string? TargetDocumentNumber { get; set; }
        public string? Article { get; set; }
        public string? Clause { get; set; }
        public string? Point { get; set; }
        public int? Page { get; set; }
    }

    public class CurrentRuleDto
    {
        public Guid VersionId { get; set; }
        public string RuleCode { get; set; } = string.Empty;
        public string ValueKind { get; set; } = string.Empty;
        public decimal? ValueNumber { get; set; }
        public JsonElement? ValueJson { get; set; }
        public string? ValueText { get; set; }
        public string? Unit { get; set; }
        public JsonElement? Condition { get; set; }
        public string? ConditionText { get; set; }
        public DateOnly ApplyFrom { get; set; }
        public DateOnly? ApplyTo { get; set; }
        public CitationDto Citation { get; set; } = new();
    }

    public class RuleCatalogItemDto
    {
        public string RuleCode { get; set; } = string.Empty;
        public string RuleGroup { get; set; } = string.Empty;
        public string ValueKind { get; set; } = string.Empty;
        public string? DefaultUnit { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class KnownDocumentDto
    {
        public string DocumentNumber { get; set; } = string.Empty;
        public string? DocumentType { get; set; }
        public string? Title { get; set; }
        public string LegalStatus { get; set; } = string.Empty;
    }

    public class LawChangesetExtractRequest
    {
        public int SchemaVersion { get; set; } = 1;
        public Guid TaskId { get; set; }
        public Guid ChangesetId { get; set; }
        public string FileUrl { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string? SourceUrl { get; set; }
        public string? DocumentNumberHint { get; set; }
        public int BaseRevision { get; set; }
        public List<CurrentRuleDto> CurrentRules { get; set; } = new();
        public List<RuleCatalogItemDto> RuleCatalog { get; set; } = new();
        public List<KnownDocumentDto> KnownDocuments { get; set; } = new();
    }

    public class LawChangesetExtractResult
    {
        public CoverageDto? Coverage { get; set; }
        public DocumentDto? Document { get; set; }
        public List<RelationDto> Relations { get; set; } = new();
        public List<OperationDto> Operations { get; set; } = new();
        public List<ReferenceDto> References { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    public class LawChangesetExtractResponse
    {
        public int SchemaVersion { get; set; } = 1;
        public Guid TaskId { get; set; }
        public Guid ChangesetId { get; set; }
        public int BaseRevision { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public string? Model { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public LawChangesetExtractResult? Result { get; set; }
    }
}
