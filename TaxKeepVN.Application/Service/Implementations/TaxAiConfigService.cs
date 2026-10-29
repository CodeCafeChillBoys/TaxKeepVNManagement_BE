using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using TaxKeepVN.Application.DTOs.SystemConfig;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVN.Application.Service.Implementations
{
    public class TaxAiConfigService : ITaxAiConfigService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<TaxAiConfigService> _logger;
        public TaxAiConfigService(
            IHttpClientFactory httpClientFactory,
            ILogger<TaxAiConfigService> logger)
        {
            // Khởi tạo client từ tên "TaxAIService" bạn vừa đăng ký ở Program.cs
            _httpClient = httpClientFactory.CreateClient("TaxAIService");
            _logger = logger;
        }
        // 1. GET /api/system-configs
        public async Task<List<SystemConfigResponseDto>> GetAllConfigsAsync(bool activeOnly = false)
        {
            var url = $"/api/system-configs?active_only={activeOnly.ToString().ToLower()}";
            var res = await _httpClient.GetFromJsonAsync<List<SystemConfigResponseDto>>(url);
            return res ?? new List<SystemConfigResponseDto>();
        }
        // 2. GET /api/system-configs/{key}
        public async Task<SystemConfigResponseDto?> GetConfigByKeyAsync(string key)
        {
            var res = await _httpClient.GetAsync($"/api/system-configs/{Uri.EscapeDataString(key)}");
            if (res.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            await EnsureSuccessOrThrowAsync(res);
            return await res.Content.ReadFromJsonAsync<SystemConfigResponseDto>();
        }
        // 3. POST /api/system-configs
        public async Task<SystemConfigResponseDto> CreateConfigAsync(SystemConfigCreateRequestDto request)
        {
            var res = await _httpClient.PostAsJsonAsync("/api/system-configs", request);
            await EnsureSuccessOrThrowAsync(res);
            return (await res.Content.ReadFromJsonAsync<SystemConfigResponseDto>())!;
        }
        // 4. PUT /api/system-configs/{key}
        public async Task<SystemConfigResponseDto> UpdateConfigAsync(string key, SystemConfigUpdateRequestDto request)
        {
            var res = await _httpClient.PutAsJsonAsync($"/api/system-configs/{Uri.EscapeDataString(key)}", request);
            await EnsureSuccessOrThrowAsync(res);
            return (await res.Content.ReadFromJsonAsync<SystemConfigResponseDto>())!;
        }
        // 5. DELETE /api/system-configs/{key}
        public async Task<bool> DeleteConfigAsync(string key, Guid? adminId = null)
        {
            var url = $"/api/system-configs/{Uri.EscapeDataString(key)}";
            if (adminId.HasValue) url += $"?admin_id={adminId.Value}";
            var res = await _httpClient.DeleteAsync(url);
            if (res.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
            await EnsureSuccessOrThrowAsync(res);
            return true;
        }
        // 6. PATCH /api/system-configs/{key}/restore
        public async Task<SystemConfigResponseDto> RestoreConfigAsync(string key, Guid? adminId = null)
        {
            var url = $"/api/system-configs/{Uri.EscapeDataString(key)}/restore";
            if (adminId.HasValue) url += $"?admin_id={adminId.Value}";
            var res = await _httpClient.PatchAsync(url, null);
            await EnsureSuccessOrThrowAsync(res);
            return (await res.Content.ReadFromJsonAsync<SystemConfigResponseDto>())!;
        }
        // 7. GET /api/system-configs/threshold/test-resolve
        public async Task<ThresholdResolveTestResponseDto> TestResolveThresholdAsync(string? categoryCode = null)
        {
            var url = "/api/system-configs/threshold/test-resolve";
            if (!string.IsNullOrEmpty(categoryCode)) url += $"?category_code={Uri.EscapeDataString(categoryCode)}";
            var res = await _httpClient.GetFromJsonAsync<ThresholdResolveTestResponseDto>(url);
            return res!;
        }
        private async Task EnsureSuccessOrThrowAsync(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                _logger.LogError("FastAPI Error: {Status} - {Detail}", response.StatusCode, err);
                throw new HttpRequestException($"Lỗi từ Python AI Service ({response.StatusCode}): {err}");
            }
        }
    }
}