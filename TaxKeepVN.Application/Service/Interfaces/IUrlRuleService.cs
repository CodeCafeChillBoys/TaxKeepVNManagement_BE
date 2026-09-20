using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.TaxAI;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface IUrlRuleService
    {
        /// <summary>
        /// Lấy danh sách các quy tắc kiểm tra URL
        /// </summary>
        Task<List<UrlRuleResponse>> GetUrlRulesAsync(bool activeOnly = false);

        /// <summary>
        /// Xem chi tiết một quy tắc URL theo ID
        /// </summary>
        Task<UrlRuleResponse?> GetUrlRuleByIdAsync(Guid id);

        /// <summary>
        /// Admin tạo mới quy tắc kiểm tra URL
        /// </summary>
        Task<UrlRuleResponse> CreateUrlRuleAsync(UrlRuleCreateRequest request, Guid adminId);

        /// <summary>
        /// Admin cập nhật quy tắc kiểm tra URL
        /// </summary>
        Task<UrlRuleResponse?> UpdateUrlRuleAsync(Guid id, UrlRuleUpdateRequest request, Guid adminId);

        /// <summary>
        /// Admin xóa một quy tắc kiểm tra URL
        /// </summary>
        Task<bool> DeleteUrlRuleAsync(Guid id);
    }
}
