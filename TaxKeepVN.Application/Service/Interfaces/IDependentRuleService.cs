using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Rules;
using TaxKeepVN.Application.DTOs.TaxAI;
using TaxKeepVN.Domain.Entities;

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
        /// Gọi sau khi Admin Approve một bộ luật hoặc từ endpoint sync-active.
        /// Trả về số bản ghi (added, updated, deactivated).
        /// </summary>
        Task<(int added, int updated, int deactivated)> SyncFromAiAsync(IEnumerable<DependentRuleFromAiDto> dependentRules);

        /// <summary>
        /// Lấy tất cả TargetGroup có is_active = true trong bảng dependent_groups
        /// và có ít nhất một quy tắc giấy tờ active trong dependent_document_rules.
        /// Lọc chính xác theo relationshipCode (không so sánh tiền tố).
        /// </summary>
        Task<IEnumerable<string>> GetActiveGroupsAsync(string? relationshipFilter = null);

        /// <summary>
        /// Lấy danh sách quy tắc tài liệu đang kích hoạt (active) cho một nhóm đối tượng.
        /// Sử dụng cache 2 lớp (MemoryCache và RedisCache).
        /// </summary>
        Task<List<DependentDocumentRule>> GetActiveRulesForGroupAsync(string targetGroup);

        /// <summary>
        /// Tính toán danh sách giấy tờ còn thiếu của người phụ thuộc dựa trên quy tắc trong cơ sở dữ liệu.
        /// Hỗ trợ kiểm tra các nhóm giấy tờ có thể thay thế nhau (AlternativeGroup).
        /// Trả về (IsComplete = true/false, Danh sách mã giấy tờ còn thiếu).
        /// Nếu nhóm chưa có quy tắc nào active, trả về (false, [ErrorCodes.Dependent.RulesNotConfigured]).
        /// </summary>
        Task<(bool IsComplete, List<string> MissingDocuments)> ComputeMissingDocumentsAsync(string targetGroup, IEnumerable<string> uploadedDocTypes);
    }
}
