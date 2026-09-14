using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
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
            [FromQuery(Name = "active_only")] bool? activeOnly = null,
            [FromQuery(Name = "activeOnly")] bool? activeOnlyCamel = null)
        {
            var isactiveOnly = activeOnly ?? activeOnlyCamel ?? false;
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

        /// <summary>
        /// Admin tạo mới quy tắc kiểm tra URL
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(UrlRuleResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> CreateUrlRule([FromBody] UrlRuleCreateRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var httpClient = _httpClientFactory.CreateClient("TaxAIService");
            try
            {
                var payload = new
                {
                    name = request.Name,
                    rule_type = request.RuleType,
                    ruleType = request.RuleType,
                    pattern = request.Pattern,
                    description = request.Description,
                    is_active = request.IsActive,
                    isActive = request.IsActive
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
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> UpdateUrlRule([FromRoute] Guid id, [FromBody] UrlRuleUpdateRequest request)
        {
            var httpClient = _httpClientFactory.CreateClient("TaxAIService");
            try
            {
                var payload = new Dictionary<string, object?>();
                if (request.Name != null) payload["name"] = request.Name;
                if (!string.IsNullOrWhiteSpace(request.RuleType))
                {
                    payload["rule_type"] = request.RuleType;
                    payload["ruleType"] = request.RuleType;
                }
                if (request.Pattern != null) payload["pattern"] = request.Pattern;
                if (request.Description != null) payload["description"] = request.Description;
                if (request.IsActive.HasValue)
                {
                    payload["is_active"] = request.IsActive.Value;
                    payload["isActive"] = request.IsActive.Value;
                }

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
