using System.Collections.Generic;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.SystemConfigs;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface ISystemConfigService
    {
        Task<PagedResult<SystemConfigResponseDto>> GetAllAsync(SystemConfigQueryParameters query);
        Task<SystemConfigResponseDto?> GetByKeyAsync(string key);

        /// <summary>
        /// Lấy giá trị cấu hình bắt buộc (config chung — không theo năm).
        /// Nếu không tìm thấy, ném ra ngoại lệ InvalidOperationException.
        /// </summary>
        Task<string> GetRequiredConfigValueAsync(string key);

        /// <summary>
        /// Lấy giá trị cấu hình phù hợp nhất với năm thuế đang tính.
        /// Logic: ưu tiên row có AppliesFromYear lớn nhất mà vẫn &lt;= taxYear.
        ///        Nếu không tìm thấy → fallback về row có AppliesFromYear = NULL (config chung).
        ///        Nếu vẫn không có → ném InvalidOperationException.
        /// </summary>
        Task<string> GetRequiredConfigValueAsync(string key, int taxYear);

        Task<SystemConfigResponseDto> CreateAsync(SystemConfigCreateDto dto);

        Task<SystemConfigResponseDto> UpdateAsync(string key, SystemConfigUpdateDto dto);
    }
}
