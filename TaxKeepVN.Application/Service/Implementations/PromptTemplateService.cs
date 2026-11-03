using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TaxKeepVN.Application.DTOs.TaxAI;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVN.Application.Service.Implementations
{
    public class PromptTemplateService : IPromptTemplateService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<PromptTemplateService> _logger;

        public PromptTemplateService(
            IHttpClientFactory httpClientFactory,
            ILogger<PromptTemplateService> logger)
        {
            _httpClient = httpClientFactory.CreateClient("TaxAIService");
            _logger = logger;
        }

        // 1. GET /api/prompt-templates?active_only=...
        public async Task<List<PromptTemplateResponseDto>> GetAllPromptsAsync(bool activeOnly = false)
        {
            var url = $"/api/prompt-templates?active_only={activeOnly.ToString().ToLower()}";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Lỗi khi lấy danh sách Prompt Templates: {StatusCode} - {Error}", response.StatusCode, error);
                throw new HttpRequestException($"AI Service error ({response.StatusCode}): {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<List<PromptTemplateResponseDto>>();
            return result ?? new List<PromptTemplateResponseDto>();
        }

        // 2. GET /api/prompt-templates/{prompt_key}
        public async Task<PromptTemplateResponseDto?> GetPromptByKeyAsync(string promptKey)
        {
            var response = await _httpClient.GetAsync($"/api/prompt-templates/{Uri.EscapeDataString(promptKey)}");
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Lỗi khi lấy chi tiết Prompt {Key}: {StatusCode} - {Error}", promptKey, response.StatusCode, error);
                throw new HttpRequestException($"AI Service error ({response.StatusCode}): {error}");
            }

            return await response.Content.ReadFromJsonAsync<PromptTemplateResponseDto>();
        }

        // 3. POST /api/prompt-templates
        public async Task<PromptTemplateResponseDto> CreatePromptAsync(PromptTemplateCreateRequestDto request)
        {
            var response = await _httpClient.PostAsJsonAsync("/api/prompt-templates", request);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Lỗi khi tạo Prompt mới: {StatusCode} - {Error}", response.StatusCode, error);
                throw new HttpRequestException($"AI Service error ({response.StatusCode}): {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<PromptTemplateResponseDto>();
            return result ?? throw new InvalidOperationException("Không nhận được dữ liệu phản hồi từ AI Service.");
        }

        // 4. PUT /api/prompt-templates/{prompt_key}
        public async Task<PromptTemplateResponseDto> UpdatePromptAsync(string promptKey, PromptTemplateUpdateRequestDto request)
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/prompt-templates/{Uri.EscapeDataString(promptKey)}", request);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Lỗi khi cập nhật Prompt {Key}: {StatusCode} - {Error}", promptKey, response.StatusCode, error);
                throw new HttpRequestException($"AI Service error ({response.StatusCode}): {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<PromptTemplateResponseDto>();
            return result ?? throw new InvalidOperationException("Không nhận được dữ liệu phản hồi từ AI Service.");
        }

        // 5. DELETE /api/prompt-templates/{prompt_key}
        public async Task<bool> DeletePromptAsync(string promptKey)
        {
            var response = await _httpClient.DeleteAsync($"/api/prompt-templates/{Uri.EscapeDataString(promptKey)}");
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Lỗi khi xóa Prompt {Key}: {StatusCode} - {Error}", promptKey, response.StatusCode, error);
                throw new HttpRequestException($"AI Service error ({response.StatusCode}): {error}");
            }

            return true;
        }

        // 6. POST /api/prompt-templates/test-render
        public async Task<PromptTestRenderResponseDto> TestRenderPromptAsync(PromptTestRenderRequestDto request)
        {
            var response = await _httpClient.PostAsJsonAsync("/api/prompt-templates/test-render", request);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Lỗi khi test render Prompt {Key}: {StatusCode} - {Error}", request.PromptKey, response.StatusCode, error);
                throw new HttpRequestException($"AI Service error ({response.StatusCode}): {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<PromptTestRenderResponseDto>();
            return result ?? throw new InvalidOperationException("Không nhận được dữ liệu phản hồi từ AI Service.");
        }
    }
}
