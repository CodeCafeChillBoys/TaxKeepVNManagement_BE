using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaxKeepVN.Application.DTOs.TaxAI;

namespace TaxKeepVNManagementSystem.Controllers
{
    [ApiController]
    [Route("api/v1/admin/url-rules")]
    [Authorize]
    [Produces("application/json")]
    public class UrlRuleController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public UrlRuleController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
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
            var isactiveOnly = activeOnly ?? false;
            var httpClient = _httpClientFactory.CreateClient("TaxAIService");
            try
            {
                var response = await httpClient.GetAsync($"/api/url-rules?active_only={isactiveOnly.ToString().ToLower()}");
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<List<UrlRuleResponse>>();
                    return Ok(result);
                }

                var error = await response.Content.ReadFromJsonAsync<object>();
                return StatusCode((int)response.StatusCode, error);
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
            var httpClient = _httpClientFactory.CreateClient("TaxAIService");
            try
            {
                var response = await httpClient.GetAsync($"/api/url-rules/{id}");
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<UrlRuleResponse>();
                    return Ok(result);
                }

                var error = await response.Content.ReadFromJsonAsync<object>();
                return StatusCode((int)response.StatusCode, error);
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

            var httpClient = _httpClientFactory.CreateClient("TaxAIService");
            try
            {
                var payload = new
                {
                    name = request.Name,
                    domain = request.Domain,
                    description = request.Description,
                    isActive = request.IsActive,
                    is_active = request.IsActive,
                    createdBy = adminId.Value,
                    created_by = adminId.Value,
                    updatedBy = adminId.Value,
                    updated_by = adminId.Value
                };

                var response = await httpClient.PostAsJsonAsync("/api/url-rules", payload);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<UrlRuleResponse>();
                    return StatusCode(StatusCodes.Status201Created, result);
                }

                var error = await response.Content.ReadFromJsonAsync<object>();
                return StatusCode((int)response.StatusCode, error);
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

            var httpClient = _httpClientFactory.CreateClient("TaxAIService");
            try
            {
                var payload = new Dictionary<string, object?>();
                if (request.Name != null) payload["name"] = request.Name;
                if (!string.IsNullOrWhiteSpace(request.Domain))
                {
                    payload["domain"] = request.Domain;
                }
                if (request.Description != null) payload["description"] = request.Description;
                if (request.IsActive.HasValue)
                {
                    payload["is_active"] = request.IsActive.Value;
                    payload["isActive"] = request.IsActive.Value;
                }
                payload["updated_by"] = adminId.Value;
                payload["updatedBy"] = adminId.Value;

                var response = await httpClient.PutAsJsonAsync($"/api/url-rules/{id}", payload);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<UrlRuleResponse>();
                    return Ok(result);
                }

                var error = await response.Content.ReadFromJsonAsync<object>();
                return StatusCode((int)response.StatusCode, error);
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
            var httpClient = _httpClientFactory.CreateClient("TaxAIService");
            try
            {
                var response = await httpClient.DeleteAsync($"/api/url-rules/{id}");
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<object>();
                    return Ok(result);
                }

                var error = await response.Content.ReadFromJsonAsync<object>();
                return StatusCode((int)response.StatusCode, error);
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
