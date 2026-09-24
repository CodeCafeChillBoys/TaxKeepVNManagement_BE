using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Rules;
using TaxKeepVN.Application.DTOs.TaxAI;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface IDependentRuleService
    {
        Task<PagedResult<DependentRuleResponseDto>> GetAllAsync(DependentRuleQueryParameters query);
        Task<DependentRuleResponseDto> GetByIdAsync(Guid ruleId);
        Task<DependentRuleResponseDto> CreateAsync(CreateDependentRuleDto dto);
        Task<DependentRuleResponseDto> UpdateAsync(Guid ruleId, UpdateDependentRuleDto dto);
        Task DeleteAsync(Guid ruleId);

        /// <summary>
        /// Đồng bộ (upsert) danh sách giấy tờ bắt buộc từ danh sách DependentRules mà TaxAIService trả về.
        /// Gọi sau khi Admin Approve một bộ luật. Tự động xóa cache.
        /// Trả về số bản ghi được thêm mới và số bản ghi được cập nhật.
        /// </summary>
        Task<(int added, int updated)> SyncFromAiAsync(IEnumerable<DependentRuleFromAiDto> dependentRules);

        /// <summary>
        /// Lấy tất cả TargetGroup có is_active = true trong bảng dependent_document_rules,
        /// có thể lọc theo relationship prefix (CHILD, SPOUSE, PARENT, OTHER_DEPENDENT).
        /// </summary>
        Task<IEnumerable<string>> GetActiveGroupsAsync(string? relationshipFilter = null);
    }
}
