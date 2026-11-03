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
    [Route("api/v1/admin/prompt-templates")]
    [Authorize(Roles = "admin")]
    [Produces("application/json")]
    [Tags("Admin - AI Prompt Templates (Quản lý System Prompt động)")]
    public class AdminPromptTemplateController : ControllerBase
    {
        private readonly IPromptTemplateService _promptService;

        public AdminPromptTemplateController(IPromptTemplateService promptService)
        {
            _promptService = promptService;
        }

        /// <summary>
        /// Lấy danh sách tất cả các System Prompt Templates
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(List<PromptTemplateResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> GetAllPrompts([FromQuery] bool activeOnly = false)
        {
            try
            {
                var result = await _promptService.GetAllPromptsAsync(activeOnly);
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
        /// Xem chi tiết một System Prompt theo mã khóa (VD: PROMPT_TAX_CHAT_ADVISORY)
        /// </summary>
        [HttpGet("{promptKey}")]
        [ProducesResponseType(typeof(PromptTemplateResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> GetPromptByKey([FromRoute] string promptKey)
        {
            try
            {
                var result = await _promptService.GetPromptByKeyAsync(promptKey);
                if (result == null)
                {
                    return NotFound(new { message = $"Không tìm thấy System Prompt có mã '{promptKey}'." });
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
        /// Admin tạo một System Prompt mới
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(PromptTemplateResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> CreatePrompt([FromBody] PromptTemplateCreateRequestDto request)
        {
            try
            {
                var result = await _promptService.CreatePromptAsync(request);
                return CreatedAtAction(nameof(GetPromptByKey), new { promptKey = result.PromptKey }, result);
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
        /// Admin cập nhật nội dung System Prompt (Tự tăng Version và xóa Cache)
        /// </summary>
        [HttpPut("{promptKey}")]
        [ProducesResponseType(typeof(PromptTemplateResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> UpdatePrompt([FromRoute] string promptKey, [FromBody] PromptTemplateUpdateRequestDto request)
        {
            try
            {
                var result = await _promptService.UpdatePromptAsync(promptKey, request);
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
        /// Admin xóa một System Prompt khỏi hệ thống
        /// </summary>
        [HttpDelete("{promptKey}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> DeletePrompt([FromRoute] string promptKey)
        {
            try
            {
                var success = await _promptService.DeletePromptAsync(promptKey);
                if (!success)
                {
                    return NotFound(new { message = $"Không tìm thấy System Prompt '{promptKey}' để xóa." });
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

        /// <summary>
        /// Thử nghiệm render System Prompt với các biến tham số giả lập
        /// </summary>
        [HttpPost("test-render")]
        [ProducesResponseType(typeof(PromptTestRenderResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> TestRenderPrompt([FromBody] PromptTestRenderRequestDto request)
        {
            try
            {
                var result = await _promptService.TestRenderPromptAsync(request);
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
    }
}
