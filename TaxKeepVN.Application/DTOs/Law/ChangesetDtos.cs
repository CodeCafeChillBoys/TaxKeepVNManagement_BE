using System;
using System.Collections.Generic;
using System.Text.Json;
using TaxKeepVN.Application.DTOs.Law.Contract;

namespace TaxKeepVN.Application.DTOs.Law
{
    public class OperationInput
    {
        public string Op { get; set; } = string.Empty;
        public string RuleCode { get; set; } = string.Empty;
        public JsonElement? After { get; set; }
        public DateOnly? ApplyFrom { get; set; }
        public DateOnly? ApplyTo { get; set; }
        public CitationDto? ApplyBasis { get; set; }
        public string? Article { get; set; }
        public string? Clause { get; set; }
        public string? Point { get; set; }
        public int? Page { get; set; }
        public string? Evidence { get; set; }
        public string? AdminNote { get; set; }
    }

    public class OperationPatchInput
    {
        public string? Decision { get; set; }
        public JsonElement? After { get; set; }
        public DateOnly? ApplyFrom { get; set; }
        public DateOnly? ApplyTo { get; set; }
        public CitationDto? ApplyBasis { get; set; }
        public string? Article { get; set; }
        public string? Clause { get; set; }
        public string? Point { get; set; }
        public int? Page { get; set; }
        public string? AdminNote { get; set; }
        public string? ConflictState { get; set; }
    }

    public class RelationInput
    {
        public string RelationType { get; set; } = string.Empty;
        public string TargetDocumentNumber { get; set; } = string.Empty;
        public string? TargetArticle { get; set; }
        public string? TargetClause { get; set; }
        public string? TargetPoint { get; set; }
        public DateOnly? EffectiveDate { get; set; }
        public string? Evidence { get; set; }
        public string? Note { get; set; }
    }

    public class RelationPatchInput
    {
        public string? Decision { get; set; }
        public string? RelationType { get; set; }
        public string? TargetDocumentNumber { get; set; }
        public string? TargetArticle { get; set; }
        public string? TargetClause { get; set; }
        public string? TargetPoint { get; set; }
        public DateOnly? EffectiveDate { get; set; }
    }

    public class OrphanResolveInput
    {
        public Guid VersionId { get; set; }
        public string Action { get; set; } = string.Empty; // RECITE | END | KEEP
        public DateOnly? EffectiveDate { get; set; }
        public string? Article { get; set; }
        public string? Clause { get; set; }
        public string? Point { get; set; }
        public string? Note { get; set; }
    }

    public class ManualChangesetInput
    {
        public Guid? DocumentId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public List<OperationInput> Operations { get; set; } = new();
        public List<RelationInput> Relations { get; set; } = new();
    }

    public class RejectChangesetInput
    {
        public string Reason { get; set; } = string.Empty;
    }

    public class LegalDocumentDto
    {
        public Guid Id { get; set; }
        public string DocumentNumber { get; set; } = string.Empty;
        public string? DocumentType { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Issuer { get; set; }
        public DateOnly? IssuedDate { get; set; }
        public DateOnly? EffectiveDate { get; set; }
        public string? SourceUrl { get; set; }
        public string? FileUrl { get; set; }
        public string LegalStatus { get; set; } = string.Empty;
        public bool IsPlaceholder { get; set; }
    }

    public class OpDto
    {
        public Guid Id { get; set; }
        public int Seq { get; set; }
        public string OpType { get; set; } = string.Empty;
        public string RuleCode { get; set; } = string.Empty;
        public string? RuleDisplayName { get; set; }
        public bool NewCode { get; set; }
        public JsonElement? ProposedDefinition { get; set; }
        public JsonElement? Before { get; set; }
        public JsonElement? After { get; set; }
        public DateOnly? ApplyFrom { get; set; }
        public DateOnly? ApplyTo { get; set; }
        public CitationDto? ApplyBasis { get; set; }
        public CitationDto Citation { get; set; } = new();
        public string? Evidence { get; set; }
        public double? Confidence { get; set; }
        public string? Rationale { get; set; }
        public string Origin { get; set; } = string.Empty;
        public string Decision { get; set; } = string.Empty;
        public bool EditedByAdmin { get; set; }
        public string? AdminNote { get; set; }
        public string ConflictState { get; set; } = string.Empty;
        public List<string> Flags { get; set; } = new();
    }

    public class RelationItemDto
    {
        public Guid Id { get; set; }
        public string RelationType { get; set; } = string.Empty;
        public string TargetDocumentNumber { get; set; } = string.Empty;
        public string? TargetArticle { get; set; }
        public string? TargetClause { get; set; }
        public string? TargetPoint { get; set; }
        public DateOnly? EffectiveDate { get; set; }
        public string? Evidence { get; set; }
        public string? Note { get; set; }
        public string Origin { get; set; } = string.Empty;
        public string Decision { get; set; } = string.Empty;
    }

    public class OrphanDto
    {
        public Guid VersionId { get; set; }
        public string RuleCode { get; set; } = string.Empty;
        public string? RuleDisplayName { get; set; }
        public string? ValueSummary { get; set; }
        public CitationDto Citation { get; set; } = new();
        public DateOnly ApplyFrom { get; set; }
        public DateOnly? ApplyTo { get; set; }
        public string RelationType { get; set; } = string.Empty;
        public string TargetDocumentNumber { get; set; } = string.Empty;
        public DateOnly? SuggestedEffectiveDate { get; set; }
        public string? Resolution { get; set; }
    }

    public class CheckItemDto
    {
        public string Code { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty; // BLOCK | WARN | INFO
        public bool Passed { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Items { get; set; } = new();
    }

    public class CheckPreviewDto
    {
        public int Added { get; set; }
        public int Updated { get; set; }
        public int Ended { get; set; }
        public int Recited { get; set; }
        public int Relations { get; set; }
        public List<Guid> NewVersionIds { get; set; } = new();
        public List<Guid> SupersededVersionIds { get; set; } = new();
    }

    public class CheckResultDto
    {
        public bool CanMerge { get; set; }
        public int HeadRevisionNo { get; set; }
        public int BaseRevisionNo { get; set; }
        public List<CheckItemDto> Checks { get; set; } = new();
        public CheckPreviewDto Preview { get; set; } = new();
    }

    public class ChangesetCountsDto
    {
        public int Pending { get; set; }
        public int Accepted { get; set; }
        public int Rejected { get; set; }
    }

    public class ChangesetListItemDto
    {
        public Guid Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Origin { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public int BaseRevisionNo { get; set; }
        public int? MergedRevisionNo { get; set; }
        public Guid? DocumentId { get; set; }
        public string? DocumentNumber { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
    }

    public class ChangesetDetailDto
    {
        public Guid Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Origin { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public LegalDocumentDto? Document { get; set; }
        public int BaseRevisionNo { get; set; }
        public int HeadRevisionNo { get; set; }
        public bool IsStale { get; set; }
        public CoverageDto? Coverage { get; set; }
        public List<string> AiWarnings { get; set; } = new();
        public string? AiErrorCode { get; set; }
        public string? AiErrorMessage { get; set; }
        public List<OpDto> Ops { get; set; } = new();
        public List<RelationItemDto> Relations { get; set; } = new();
        public List<OrphanDto> Orphans { get; set; } = new();
        public CheckResultDto Check { get; set; } = new();
        public ChangesetCountsDto Counts { get; set; } = new();
    }

    public class LegalDocumentDetailDto : LegalDocumentDto
    {
        public List<RelationItemDto> IncomingRelations { get; set; } = new();
        public List<RelationItemDto> OutgoingRelations { get; set; } = new();
        public List<ChangesetListItemDto> Changesets { get; set; } = new();
    }

    public class UpdateLegalDocumentInput
    {
        public string? DocumentNumber { get; set; }
        public string? DocumentType { get; set; }
        public string? Title { get; set; }
        public string? Issuer { get; set; }
        public DateOnly? IssuedDate { get; set; }
        public DateOnly? EffectiveDate { get; set; }
        public string? SourceUrl { get; set; }
    }

    public class UploadDocumentResponseDto
    {
        public Guid DocumentId { get; set; }
        public Guid ChangesetId { get; set; }
        public Guid TaskId { get; set; }
    }
}
