using System.ComponentModel.DataAnnotations;

namespace TaxKeepVN.Application.DTOs.Permissions
{
    public class PermissionDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
    }

    public class CreatePermissionDto
    {
        [Required(ErrorMessage = "Mã quyền không được để trống.")]
        [StringLength(100)]
        [RegularExpression(
            @"^[a-z][a-z0-9]*(\.[a-z][a-z0-9]*)+$",
            ErrorMessage = "Mã quyền phải có dạng document.create.")]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên quyền không được để trống.")]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nhóm quyền không được để trống.")]
        [StringLength(100)]
        public string GroupName { get; set; } = string.Empty;
    }

    public class UpdatePermissionDto
    {
        [Required(ErrorMessage = "Tên quyền không được để trống.")]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nhóm quyền không được để trống.")]
        [StringLength(100)]
        public string GroupName { get; set; } = string.Empty;
    }
}