using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using TaxKeepVN.Application.DTOs.SystemConfig;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVNManagementSystem.Controllers
{
    [ApiController]
    [Route("api/admin/system-configs")]
    public class SystemConfigsController : ControllerBase
    {
        private readonly ITaxAiConfigService _configClient;
        public SystemConfigsController(ITaxAiConfigService configClient)
        {
            _configClient = configClient;
        }

        // 1. GET: Lấy danh sách cấu hình
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = false)
        {
            var configs = await _configClient.GetAllConfigsAsync(activeOnly);
            return Ok(configs);
        }
        // 2. GET: Xem chi tiết theo key
        [HttpGet("{key}")]
        public async Task<IActionResult> GetByKey([FromRoute] string key)
        {
            var config = await _configClient.GetConfigByKeyAsync(key);
            if (config == null) return NotFound(new { message = $"Không tìm thấy cấu hình '{key}'." });
            return Ok(config);
        }
        // 3. POST: Admin thêm cấu hình mới
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] SystemConfigCreateRequestDto dto)
        {
            var result = await _configClient.CreateConfigAsync(dto);
            return CreatedAtAction(nameof(GetByKey), new { key = result.ConfigKey }, result);
        }
        // 4. PUT: Admin cập nhật giá trị / trạng thái
        [HttpPut("{key}")]
        public async Task<IActionResult> Update([FromRoute] string key, [FromBody] SystemConfigUpdateRequestDto dto)
        {
            var result = await _configClient.UpdateConfigAsync(key, dto);
            return Ok(result);
        }
        // 5. DELETE: Xóa mềm cấu hình
        [HttpDelete("{key}")]
        public async Task<IActionResult> Delete([FromRoute] string key, [FromQuery] Guid? adminId)
        {
            var success = await _configClient.DeleteConfigAsync(key, adminId);
            if (!success) return NotFound(new { message = $"Không tìm thấy cấu hình '{key}' để xóa." });
            return Ok(new { message = $"Đã xóa mềm cấu hình '{key.ToUpper()}' thành công." });
        }
        // 6. PATCH: Khôi phục cấu hình đã xóa mềm
        [HttpPatch("{key}/restore")]
        public async Task<IActionResult> Restore([FromRoute] string key, [FromQuery] Guid? adminId)
        {
            var result = await _configClient.RestoreConfigAsync(key, adminId);
            return Ok(result);
        }
        // 7. GET: Test thử xem loại hóa đơn này đang nhận ngưỡng nào
        [HttpGet("threshold/test-resolve")]
        public async Task<IActionResult> TestResolve([FromQuery] string? categoryCode)
        {
            var result = await _configClient.TestResolveThresholdAsync(categoryCode);
            return Ok(result);
        }
    }
}