using TaxKeepVN.Application.DTOs.Permissions;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface IRolePermissionService
    {
        Task<IEnumerable<RoleDto>> GetRolesAsync();
        Task<RolePermissionsDto> GetPermissionsAsync(int roleId);
        Task<RolePermissionsDto> UpdatePermissionsAsync(int roleId, UpdateRolePermissionsDto dto);
    }
}
