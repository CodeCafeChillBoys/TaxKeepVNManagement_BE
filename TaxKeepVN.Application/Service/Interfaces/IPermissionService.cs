using TaxKeepVN.Application.DTOs.Permissions;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface IPermissionService
    {
        Task<IEnumerable<PermissionDto>> GetAllAsync();
        Task<PermissionDto> GetByIdAsync(int id);
        Task<PermissionDto> CreateAsync(CreatePermissionDto dto);
        Task<PermissionDto> UpdateAsync(int id, UpdatePermissionDto dto);
        Task<bool> DeleteAsync(int id);

    }
}