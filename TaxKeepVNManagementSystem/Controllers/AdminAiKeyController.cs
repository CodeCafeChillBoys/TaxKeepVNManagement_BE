using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaxKeepVN.Application.DTOs.TaxAI;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVNManagementSystem.Controllers
{
    [ApiController]
    [Route("api/v1/admin/ai-keys")]
    [Authorize(Roles = "admin")]
    [Produces("application/json")]
    [Tags("Admin - AI Provider Keys (Quản lý API Key AI)")]
    public class AdminAiKeyController : ControllerBase
    {
        private readonly IAiKeyService _aiKeyService;

        public AdminAiKeyController(IAiKeyService aiKeyService)
        {
            _aiKeyService = aiKeyService;
        }

        /// <summary>
        /// Lấy danh sách các API Key AI trong hệ thống (Đã mask bảo mật)
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(List<AiKeyResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> GetAllKeys()
        {
            try
            {
                var result = await _aiKeyService.GetAllKeysAsync();
                return Ok(result);
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = "Không thể kết nối tới Tax AI Service.",
                    detail = ex.Message
                });
            }
        }

        /// <summary>
        /// Admin thêm API Key AI mới (Tự động test key trên Google trước khi lưu)
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(AiKeyResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> CreateKey([FromBody] AiKeyCreateRequestDto request)
        {
            try
            {
                var result = await _aiKeyService.CreateKeyAsync(request);
                return CreatedAtAction(nameof(GetAllKeys), new { id = result.Id }, result);
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = "Không thể kết nối tới Tax AI Service hoặc Key không hợp lệ.",
                    detail = ex.Message
                });
            }
        }

        /// <summary>
        /// Admin cập nhật API Key (Tên gợi nhớ, Độ ưu tiên priority, Bật/Tắt is_active, Trạng thái)
        /// </summary>
        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(AiKeyResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> UpdateKey([FromRoute] Guid id, [FromBody] AiKeyUpdateRequestDto request)
        {
            try
            {
                var result = await _aiKeyService.UpdateKeyAsync(id, request);
                if (result == null)
                {
                    return NotFound(new { message = $"Không tìm thấy API Key có Id '{id}'." });
                }
                return Ok(result);
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = "Không thể kết nối tới Tax AI Service.",
                    detail = ex.Message
                });
            }
        }

        /// <summary>
        /// Admin xóa một API Key khỏi hệ thống
        /// </summary>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> DeleteKey([FromRoute] Guid id)
        {
            try
            {
                var success = await _aiKeyService.DeleteKeyAsync(id);
                if (!success)
                {
                    return NotFound(new { message = $"Không tìm thấy API Key có Id '{id}' để xóa." });
                }
                return NoContent();
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = "Không thể kết nối tới Tax AI Service.",
                    detail = ex.Message
                });
            }
        }
    }
}
