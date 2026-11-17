using System.ComponentModel.DataAnnotations;

namespace TaxKeepVN.Application.DTOs.Permissions
{
    public class RoleDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class RolePermissionOptionDto : PermissionDto
    {
        public bool Granted { get; set; }
    }

    public class RolePermissionsDto
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public List<RolePermissionOptionDto> Permissions { get; set; } = new();
    }

    public class UpdateRolePermissionsDto
    {
        // An explicit empty list revokes all permissions; omitted/null is invalid.
        [Required]
        public List<int>? PermissionIds { get; set; }
    }
}
