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
    public class AiKeyService : IAiKeyService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AiKeyService> _logger;

        public AiKeyService(
            IHttpClientFactory httpClientFactory,
            ILogger<AiKeyService> logger)
        {
            _httpClient = httpClientFactory.CreateClient("TaxAIService");
            _logger = logger;
        }

        // 1. GET /api/ai-keys
        public async Task<List<AiKeyResponseDto>> GetAllKeysAsync()
        {
            var response = await _httpClient.GetAsync("/api/ai-keys");
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Lỗi khi lấy danh sách AI Keys: {StatusCode} - {Error}", response.StatusCode, error);
                throw new HttpRequestException($"AI Service error ({response.StatusCode}): {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<List<AiKeyResponseDto>>();
            return result ?? new List<AiKeyResponseDto>();
        }

        // 2. POST /api/ai-keys
        public async Task<AiKeyResponseDto> CreateKeyAsync(AiKeyCreateRequestDto request)
        {
            var response = await _httpClient.PostAsJsonAsync("/api/ai-keys", request);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Lỗi khi tạo AI Key mới: {StatusCode} - {Error}", response.StatusCode, error);
                throw new HttpRequestException($"AI Service error ({response.StatusCode}): {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<AiKeyResponseDto>();
            return result ?? throw new InvalidOperationException("Không nhận được dữ liệu phản hồi từ AI Service.");
        }

        // 3. PUT /api/ai-keys/{id}
        public async Task<AiKeyResponseDto?> UpdateKeyAsync(Guid id, AiKeyUpdateRequestDto request)
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/ai-keys/{id}", request);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Lỗi khi cập nhật AI Key {Id}: {StatusCode} - {Error}", id, response.StatusCode, error);
                throw new HttpRequestException($"AI Service error ({response.StatusCode}): {error}");
            }

            return await response.Content.ReadFromJsonAsync<AiKeyResponseDto>();
        }

        // 4. DELETE /api/ai-keys/{id}
        public async Task<bool> DeleteKeyAsync(Guid id)
        {
            var response = await _httpClient.DeleteAsync($"/api/ai-keys/{id}");
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Lỗi khi xóa AI Key {Id}: {StatusCode} - {Error}", id, response.StatusCode, error);
                throw new HttpRequestException($"AI Service error ({response.StatusCode}): {error}");
            }

            return true;
        }
    }
}
