using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaxKeepVN.Application.DTOs.Permissions;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVNManagementSystem.Attributes;

namespace TaxKeepVNManagementSystem.Controllers
{
    [ApiController]
    [Route("api/v1/permissions")]
    [HasPermission("admin.role_permission.manage")]
    public class PermissionController : ControllerBase
    {
        private readonly IPermissionService _service;

        public PermissionController(IPermissionService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var data = await _service.GetAllAsync();

            return Ok(ApiResponse<IEnumerable<PermissionDto>>.Ok(
                data,
                "Lấy danh sách quyền thành công."));
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById([FromRoute] int id)
        {
            var data = await _service.GetByIdAsync(id);

            return Ok(ApiResponse<PermissionDto>.Ok(
                data,
                "Lấy thông tin quyền thành công."));
        }

        [HttpPost]
        public async Task<IActionResult> Create(
            [FromBody] CreatePermissionDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));

            var data = await _service.CreateAsync(dto);

            return StatusCode(
                StatusCodes.Status201Created,
                ApiResponse<PermissionDto>.Ok(
                    data,
                    "Tạo quyền thành công."));
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(
            [FromRoute] int id,
            [FromBody] UpdatePermissionDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));

            var data = await _service.UpdateAsync(id, dto);

            return Ok(ApiResponse<PermissionDto>.Ok(
                data,
                "Cập nhật quyền thành công."));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete([FromRoute] int id)
        {
            var result = await _service.DeleteAsync(id);

            return Ok(ApiResponse<bool>.Ok(
                result,
                "Xóa quyền thành công."));
        }
    }
}