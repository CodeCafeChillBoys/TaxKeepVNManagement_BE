using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Law;

namespace TaxKeepVN.Application.Law.Query
{
    public interface ISystemLawQueryService
    {
        Task<int> GetHeadRevisionAsync(CancellationToken ct = default);
        Task<EffectiveLawDto> GetEffectiveAsync(int taxYear, int? asOfRevision = null, CancellationToken ct = default);
        Task<RuleHistoryDto> GetRuleHistoryAsync(string ruleCode, CancellationToken ct = default);
        Task<List<RevisionDto>> GetRevisionsAsync(int page = 1, int size = 20, CancellationToken ct = default);
        Task<RevisionDetailDto> GetRevisionDetailAsync(int revisionNo, CancellationToken ct = default);
        Task<List<LawRuleDefinitionDto>> GetRuleDefinitionsAsync(bool? activeOnly = null, CancellationToken ct = default);
        Task<LawRuleDefinitionDto> CreateRuleDefinitionAsync(LawRuleDefinitionInput input, CancellationToken ct = default);
        Task<LawRuleDefinitionDto> UpdateRuleDefinitionAsync(string ruleCode, LawRuleDefinitionUpdateInput input, CancellationToken ct = default);
        void InvalidateCache();
        void IncrementMetaVersion();
    }
}
