using System;
using System.Threading;
using System.Threading.Tasks;

namespace TaxKeepVN.Application.Law.Merge
{
    public interface ILawMergeExecutor
    {
        Task<int> ExecuteAsync(Guid changesetId, Guid adminId, CancellationToken ct = default);
    }
}
