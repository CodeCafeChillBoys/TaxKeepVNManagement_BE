using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using TaxKeepVN.Application.DTOs.Law;

namespace TaxKeepVN.Application.Law.Changeset
{
    public interface ILawChangesetService
    {
        Task<List<ChangesetListItemDto>> GetChangesetsAsync(string? status, int page = 1, int size = 20, CancellationToken ct = default);
        Task<ChangesetDetailDto?> GetOpenChangesetAsync(CancellationToken ct = default);
        Task<ChangesetDetailDto> GetChangesetDetailAsync(Guid id, CancellationToken ct = default);
        Task<ChangesetDetailDto> CreateManualChangesetAsync(ManualChangesetInput input, Guid adminId, CancellationToken ct = default);
        Task<ChangesetDetailDto> ImportChangesetAsync(IFormFile file, string reason, Guid? documentId, Guid adminId, CancellationToken ct = default);
        Task<OpDto> AddOpAsync(Guid changesetId, OperationInput input, Guid adminId, CancellationToken ct = default);
        Task<OpDto> PatchOpAsync(Guid changesetId, Guid opId, OperationPatchInput input, Guid adminId, CancellationToken ct = default);
        Task<ChangesetDetailDto> AcceptAllAsync(Guid changesetId, Guid adminId, CancellationToken ct = default);
        Task<RelationItemDto> AddRelationAsync(Guid changesetId, RelationInput input, Guid adminId, CancellationToken ct = default);
        Task<RelationItemDto> PatchRelationAsync(Guid changesetId, Guid relId, RelationPatchInput input, Guid adminId, CancellationToken ct = default);
        Task<ChangesetDetailDto> ResolveOrphanAsync(Guid changesetId, OrphanResolveInput input, Guid adminId, CancellationToken ct = default);
        Task<ChangesetDetailDto> RebaseAsync(Guid changesetId, Guid adminId, CancellationToken ct = default);
        Task<CheckResultDto> CheckAsync(Guid changesetId, CancellationToken ct = default);
        Task<ChangesetDetailDto> RejectAsync(Guid changesetId, string reason, Guid adminId, CancellationToken ct = default);
        Task<int> MergeAsync(Guid changesetId, Guid adminId, CancellationToken ct = default);
        Task RetryAsync(Guid changesetId, Guid adminId, CancellationToken ct = default);
    }
}
