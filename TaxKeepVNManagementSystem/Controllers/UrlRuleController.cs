using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaxKeepVN.Application.DTOs.TaxAI;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVNManagementSystem.Controllers
{
    [ApiController]
    [Route("api/v1/admin/url-rules")]
    [Authorize]
    [Produces("application/json")]
    public class UrlRuleController : ControllerBase
    {
        private readonly IUrlRuleService _urlRuleService;

        public UrlRuleController(IUrlRuleService urlRuleService)
        {
            _urlRuleService = urlRuleService;
        }

        /// <summary>
        /// Lấy danh sách các quy tắc kiểm tra URL
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(List<UrlRuleResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> GetUrlRules(
            [FromQuery(Name = "activeOnly")] bool? activeOnly = null)
        {
            try
            {
                var isactiveOnly = activeOnly ?? false;
                var result = await _urlRuleService.GetUrlRulesAsync(isactiveOnly);
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
        /// Xem chi tiết một quy tắc URL
        /// </summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(UrlRuleResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> GetUrlRuleById([FromRoute] Guid id)
        {
            try
            {
                var result = await _urlRuleService.GetUrlRuleByIdAsync(id);
                if (result == null)
                {
                    return NotFound(new { message = "Không tìm thấy quy tắc URL yêu cầu." });
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
        /// Admin tạo mới quy tắc kiểm tra URL
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(UrlRuleResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> CreateUrlRule([FromBody] UrlRuleCreateRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var adminId = GetAdminId();
            if (!adminId.HasValue)
            {
                return Unauthorized(new { message = "Không xác định được danh tính Admin từ token." });
            }

            try
            {
                var result = await _urlRuleService.CreateUrlRuleAsync(request, adminId.Value);
                return StatusCode(StatusCodes.Status201Created, result);
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
        /// Admin cập nhật quy tắc kiểm tra URL
        /// </summary>
        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(UrlRuleResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> UpdateUrlRule([FromRoute] Guid id, [FromBody] UrlRuleUpdateRequest request)
        {
            var adminId = GetAdminId();
            if (!adminId.HasValue)
            {
                return Unauthorized(new { message = "Không xác định được danh tính Admin từ token." });
            }

            try
            {
                var result = await _urlRuleService.UpdateUrlRuleAsync(id, request, adminId.Value);
                if (result == null)
                {
                    return NotFound(new { message = "Không tìm thấy quy tắc URL để cập nhật." });
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
        /// Admin xóa một quy tắc kiểm tra URL
        /// </summary>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> DeleteUrlRule([FromRoute] Guid id)
        {
            try
            {
                var success = await _urlRuleService.DeleteUrlRuleAsync(id);
                if (!success)
                {
                    return NotFound(new { message = "Không tìm thấy quy tắc URL để xóa." });
                }

                return Ok(new { message = "Xóa quy tắc URL thành công." });
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

        private Guid? GetAdminId()
        {
            var adminIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("userId")
                ?? User.FindFirstValue("sub")
                ?? User.FindFirstValue("adminId");
            return Guid.TryParse(adminIdClaim, out var parsedGuid) ? parsedGuid : null;
        }
    }
}
