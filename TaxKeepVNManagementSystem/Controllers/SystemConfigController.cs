using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.DTOs.SystemConfigs;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVNManagementSystem.Controllers
{
    [ApiController]
    [Route("api/v1/system-configs")]
    [Authorize(Roles = "admin")]
    public class SystemConfigController : ControllerBase
    {
        private readonly ISystemConfigService _service;

        public SystemConfigController(ISystemConfigService service)
        {
            _service = service;
        }

        /// <summary>
        /// Lấy danh sách cấu hình hệ thống (Hỗ trợ Searching, Sorting, Pagination theo API Guidelines)
        /// </summary>
        [HttpGet(Name = "GetSystemConfigs")]
        public async Task<IActionResult> GetAll([FromQuery] SystemConfigQueryParameters query)
        {
            var data = await _service.GetAllAsync(query);
            return Ok(ApiResponse<object>.Ok(data, "Lấy danh sách cấu hình hệ thống thành công."));
        }

        /// <summary>
        /// Lấy chi tiết một cấu hình hệ thống theo Key (vd: TAX_SETTLEMENT_REMINDER_DAYS)
        /// </summary>
        [HttpGet("{key}", Name = "GetSystemConfigByKey")]
        public async Task<IActionResult> GetByKey([FromRoute] string key)
        {
            var data = await _service.GetByKeyAsync(key);
            if (data == null)
            {
                return NotFound(ApiResponse<object>.Fail("NOT_FOUND", $"Không tìm thấy cấu hình với key '{key}'."));
            }
            return Ok(ApiResponse<object>.Ok(data, "Lấy thông tin cấu hình thành công."));
        }

        /// <summary>
        /// Admin tạo mới một cấu hình hệ thống
        /// </summary>
        [HttpPost(Name = "CreateSystemConfig")]
        public async Task<IActionResult> Create([FromBody] SystemConfigCreateDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));
            }

            try
            {
                var data = await _service.CreateAsync(dto);
                return CreatedAtRoute("GetSystemConfigByKey", new { key = data.ConfigKey }, ApiResponse<object>.Ok(data, $"Tạo mới cấu hình '{dto.ConfigKey}' thành công."));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ApiResponse<object>.Fail("ALREADY_EXISTS", ex.Message));
            }
        }

        /// <summary>
        /// Admin cập nhật cấu hình hệ thống theo Key
        /// </summary>
        [HttpPut("{key}", Name = "UpdateSystemConfig")]
        public async Task<IActionResult> Update([FromRoute] string key, [FromBody] SystemConfigUpdateDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));
            }

            var data = await _service.UpdateAsync(key, dto);
            return Ok(ApiResponse<object>.Ok(data, $"Cập nhật cấu hình '{key}' thành công."));
        }
    }
}
