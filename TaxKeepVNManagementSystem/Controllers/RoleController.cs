using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaxKeepVN.Application.DTOs.Permissions;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVNManagementSystem.Attributes;

namespace TaxKeepVNManagementSystem.Controllers
{
    [ApiController]
    [Route("api/v1/roles")]
    [HasPermission("admin.role_permission.manage")]
    public class RoleController : ControllerBase
    {
        private readonly IRolePermissionService _service;

        public RoleController(IRolePermissionService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var data = await _service.GetRolesAsync();
            return Ok(ApiResponse<IEnumerable<RoleDto>>.Ok(data, "Lấy danh sách vai trò thành công."));
        }

        [HttpGet("{id:int}/permissions")]
        public async Task<IActionResult> GetPermissions([FromRoute] int id)
        {
            var data = await _service.GetPermissionsAsync(id);
            return Ok(ApiResponse<RolePermissionsDto>.Ok(data, "Lấy quyền của vai trò thành công."));
        }

        [HttpPut("{id:int}/permissions")]
        public async Task<IActionResult> UpdatePermissions([FromRoute] int id, [FromBody] UpdateRolePermissionsDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));

            var data = await _service.UpdatePermissionsAsync(id, dto);
            return Ok(ApiResponse<RolePermissionsDto>.Ok(data, "Cập nhật quyền của vai trò thành công."));
        }
    }
}
