using System;
using System.Collections.Generic;
using TaxKeepVN.Domain.Entities.Law;

namespace TaxKeepVN.Application.Law.Merge
{
    public class MergeInput
    {
        public int HeadRevisionNo { get; set; }
        public IReadOnlyList<LawRuleVersion> ActiveVersions { get; set; } = new List<LawRuleVersion>();
        public IReadOnlyList<LawChangeOp> AcceptedOps { get; set; } = new List<LawChangeOp>();
        public IReadOnlyList<LawChangesetRelation> AcceptedRelations { get; set; } = new List<LawChangesetRelation>();
        public IReadOnlyList<LawOrphanResolution> OrphanResolutions { get; set; } = new List<LawOrphanResolution>();
        public IReadOnlyList<LawRuleDefinition> Catalog { get; set; } = new List<LawRuleDefinition>();
        public IReadOnlyList<LegalDocument> Documents { get; set; } = new List<LegalDocument>();
        public IReadOnlyList<LegalDocumentRelation> MergedDocumentRelations { get; set; } = new List<LegalDocumentRelation>();
        public LawChangeset Changeset { get; set; } = null!;
        public IReadOnlyList<LawChangeOp> AllOps { get; set; } = new List<LawChangeOp>();
        public IReadOnlyList<LawChangesetRelation> AllRelations { get; set; } = new List<LawChangesetRelation>();
        public DateOnly Today { get; set; }
    }

    public class MergePlan
    {
        public List<Guid> SupersededVersionIds { get; set; } = new();
        public List<LawRuleVersion> NewVersions { get; set; } = new();
        public List<LegalDocument> PlaceholdersToCreate { get; set; } = new();
        public List<LegalDocumentRelation> RelationsToCreate { get; set; } = new();
        public List<LegalStatusUpdate> LegalStatusUpdates { get; set; } = new();
        public List<OrphanRuleInfo> Orphans { get; set; } = new();
        public List<MergeCheckResult> Checks { get; set; } = new();
        public MergeSummary Summary { get; set; } = new();
        public Dictionary<Guid, List<string>> OpInfo { get; set; } = new();
        public bool CanMerge => Checks.All(c => c.Level != CheckLevel.BLOCK || c.Passed);
    }

    public class LegalStatusUpdate
    {
        public Guid DocumentId { get; set; }
        public string TargetStatus { get; set; } = string.Empty;
        public string? Note { get; set; }
    }

    public class OrphanRuleInfo
    {
        public Guid VersionId { get; set; }
        public string RuleCode { get; set; } = string.Empty;
        public Guid DocumentId { get; set; }
        public string? DocumentNumber { get; set; }
        public DateOnly ApplyFrom { get; set; }
        public DateOnly? ApplyTo { get; set; }
        public string? Article { get; set; }
        public string? Clause { get; set; }
        public string? Point { get; set; }
    }

    public enum CheckLevel
    {
        BLOCK,
        WARN,
        INFO
    }

    public class MergeCheckResult
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public CheckLevel Level { get; set; }
        public bool Passed { get; set; }
        public List<string> Items { get; set; } = new();
    }

    public class MergeSummary
    {
        public int TotalOpsApplied { get; set; }
        public int AddOpsCount { get; set; }
        public int UpdateOpsCount { get; set; }
        public int ReciteOpsCount { get; set; }
        public int EndOpsCount { get; set; }
        public int RelationsCount { get; set; }
        public int NewVersionsCount { get; set; }
        public int SupersededVersionsCount { get; set; }
        public int PlaceholdersCount { get; set; }
    }
}
