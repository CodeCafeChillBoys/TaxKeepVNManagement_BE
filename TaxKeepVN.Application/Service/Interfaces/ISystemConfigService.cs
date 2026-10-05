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
        /// Lấy giá trị cấu hình bắt buộc từ DB. Nếu không tìm thấy, ném ra ngoại lệ InvalidOperationException.
        /// </summary>
        Task<string> GetRequiredConfigValueAsync(string key);

        Task<SystemConfigResponseDto> CreateAsync(SystemConfigCreateDto dto);

        Task<SystemConfigResponseDto> UpdateAsync(string key, SystemConfigUpdateDto dto);
    }
}
