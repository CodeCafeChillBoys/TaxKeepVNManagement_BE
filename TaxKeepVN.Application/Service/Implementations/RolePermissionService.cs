using TaxKeepDomain.Entities;
using TaxKeepVN.Application.DTOs.Permissions;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    public class RolePermissionService : IRolePermissionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPermissionCacheService? _permissionCacheService;

        public RolePermissionService(IUnitOfWork unitOfWork, IPermissionCacheService? permissionCacheService = null)
        {
            _unitOfWork = unitOfWork;
            _permissionCacheService = permissionCacheService;
        }

        public async Task<IEnumerable<RoleDto>> GetRolesAsync()
        {
            var roles = await _unitOfWork.Repository<Role>().GetAllAsync();
            return roles.OrderBy(r => r.Id).Select(r => new RoleDto
            {
                Id = r.Id, Code = r.Code, Name = r.Name, IsActive = r.IsActive
            }).ToList();
        }

        public async Task<RolePermissionsDto> GetPermissionsAsync(int roleId)
        {
            var role = await FindRoleAsync(roleId);
            var permissions = await _unitOfWork.Repository<Permission>().GetAllAsync();
            var assignments = await _unitOfWork.Repository<RolePermission>()
                .FindAsync(rp => rp.RoleId == roleId);
            return Map(role, permissions, assignments.Select(rp => rp.PermissionId).ToHashSet());
        }

        public async Task<RolePermissionsDto> UpdatePermissionsAsync(int roleId, UpdateRolePermissionsDto dto)
        {
            var role = await FindRoleAsync(roleId);
            if (dto.PermissionIds == null || dto.PermissionIds.Any(id => id <= 0))
                throw new BadRequestException("INVALID_PERMISSION_IDS", "Danh sách quyền phải được cung cấp và chỉ chứa ID dương.");

            var selectedIds = dto.PermissionIds.ToHashSet();
            var permissions = (await _unitOfWork.Repository<Permission>().GetAllAsync()).ToList();
            var validIds = permissions.Select(p => p.Id).ToHashSet();
            if (!selectedIds.IsSubsetOf(validIds))
                throw new BadRequestException("INVALID_PERMISSION_IDS", "Danh sách có quyền không tồn tại.");

            var repo = _unitOfWork.Repository<RolePermission>();
            var existing = (await repo.FindAsync(rp => rp.RoleId == roleId)).ToList();
            var existingIds = existing.Select(rp => rp.PermissionId).ToHashSet();

            foreach (var assignment in existing.Where(rp => !selectedIds.Contains(rp.PermissionId)))
                repo.Remove(assignment);

            foreach (var permissionId in selectedIds.Except(existingIds))
                await repo.AddAsync(new RolePermission { RoleId = roleId, PermissionId = permissionId });

            // One SaveChanges commits the additions and removals atomically.
            await _unitOfWork.SaveChangesAsync();

            if (_permissionCacheService != null)
            {
                await _permissionCacheService.WarmupRolePermissionsAsync(roleId);
            }

            return Map(role, permissions, selectedIds);
        }

        private async Task<Role> FindRoleAsync(int roleId)
        {
            var roles = await _unitOfWork.Repository<Role>().FindAsync(r => r.Id == roleId);
            return roles.FirstOrDefault()
                ?? throw new NotFoundException($"Không tìm thấy vai trò với Id = {roleId}.");
        }

        private static RolePermissionsDto Map(Role role, IEnumerable<Permission> permissions, HashSet<int> selectedIds)
        {
            return new RolePermissionsDto
            {
                RoleId = role.Id,
                RoleName = role.Name,
                Permissions = permissions.OrderBy(p => p.GroupName).ThenBy(p => p.Code)
                    .Select(p => new RolePermissionOptionDto
                    {
                        Id = p.Id, Code = p.Code, Name = p.Name,
                        GroupName = p.GroupName, Granted = selectedIds.Contains(p.Id)
                    }).ToList()
            };
        }
    }
}
