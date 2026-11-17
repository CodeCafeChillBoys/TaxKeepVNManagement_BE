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
    public class UrlRuleService : IUrlRuleService
    {
        private const string CachePrefix = "taxkeep:url_rules:";
        private readonly HttpClient _httpClient;
        private readonly ILogger<UrlRuleService> _logger;
        private readonly IRedisCacheService? _redisCache;

        public UrlRuleService(
            IHttpClientFactory httpClientFactory,
            ILogger<UrlRuleService> logger,
            IRedisCacheService? redisCache = null)
        {
            _httpClient = httpClientFactory.CreateClient("TaxAIService");
            _logger = logger;
            _redisCache = redisCache;
        }

        // 1. GET /api/url-rules
        public async Task<List<UrlRuleResponse>> GetUrlRulesAsync(bool activeOnly = false)
        {
            var cacheKey = $"{CachePrefix}{(activeOnly ? "active" : "all")}";
            if (_redisCache != null)
            {
                var cached = await _redisCache.GetAsync<List<UrlRuleResponse>>(cacheKey);
                if (cached != null && cached.Count > 0)
                    return cached;
            }

            var url = $"/api/url-rules?active_only={activeOnly.ToString().ToLower()}";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Lỗi khi lấy danh sách UrlRules: {StatusCode} - {Error}", response.StatusCode, error);
                throw new HttpRequestException($"AI Service error: {response.StatusCode} - {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<List<UrlRuleResponse>>() ?? new List<UrlRuleResponse>();

            if (_redisCache != null && result.Count > 0)
            {
                await _redisCache.SetAsync(cacheKey, result, TimeSpan.FromHours(24));
            }

            return result;
        }

        // 2. GET /api/url-rules/{id}
        public async Task<UrlRuleResponse?> GetUrlRuleByIdAsync(Guid id)
        {
            var response = await _httpClient.GetAsync($"/api/url-rules/{id}");
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Lỗi khi lấy chi tiết UrlRule {Id}: {StatusCode} - {Error}", id, response.StatusCode, error);
                throw new HttpRequestException($"AI Service error: {response.StatusCode} - {error}");
            }

            return await response.Content.ReadFromJsonAsync<UrlRuleResponse>();
        }

        // 3. POST /api/url-rules
        public async Task<UrlRuleResponse> CreateUrlRuleAsync(UrlRuleCreateRequest request, Guid adminId)
        {
            var payload = new
            {
                name = request.Name,
                domain = request.Domain,
                description = request.Description,
                isActive = request.IsActive,
                is_active = request.IsActive,
                createdBy = adminId,
                created_by = adminId,
                updatedBy = adminId,
                updated_by = adminId
            };

            var response = await _httpClient.PostAsJsonAsync("/api/url-rules", payload);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Lỗi khi tạo UrlRule: {StatusCode} - {Error}", response.StatusCode, error);
                throw new HttpRequestException($"AI Service error: {response.StatusCode} - {error}");
            }

            if (_redisCache != null) await _redisCache.RemoveByPrefixAsync(CachePrefix);

            var result = await response.Content.ReadFromJsonAsync<UrlRuleResponse>();
            return result!;
        }

        // 4. PUT /api/url-rules/{id}
        public async Task<UrlRuleResponse?> UpdateUrlRuleAsync(Guid id, UrlRuleUpdateRequest request, Guid adminId)
        {
            var payload = new Dictionary<string, object?>();
            if (request.Name != null) payload["name"] = request.Name;
            if (!string.IsNullOrWhiteSpace(request.Domain)) payload["domain"] = request.Domain;
            if (request.Description != null) payload["description"] = request.Description;
            if (request.IsActive.HasValue)
            {
                payload["is_active"] = request.IsActive.Value;
                payload["isActive"] = request.IsActive.Value;
            }
            payload["updated_by"] = adminId;
            payload["updatedBy"] = adminId;

            var response = await _httpClient.PutAsJsonAsync($"/api/url-rules/{id}", payload);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Lỗi khi cập nhật UrlRule {Id}: {StatusCode} - {Error}", id, response.StatusCode, error);
                throw new HttpRequestException($"AI Service error: {response.StatusCode} - {error}");
            }

            if (_redisCache != null) await _redisCache.RemoveByPrefixAsync(CachePrefix);

            return await response.Content.ReadFromJsonAsync<UrlRuleResponse>();
        }

        // 5. DELETE /api/url-rules/{id}
        public async Task<bool> DeleteUrlRuleAsync(Guid id)
        {
            var response = await _httpClient.DeleteAsync($"/api/url-rules/{id}");
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Lỗi khi xóa UrlRule {Id}: {StatusCode} - {Error}", id, response.StatusCode, error);
                throw new HttpRequestException($"AI Service error: {response.StatusCode} - {error}");
            }

            if (_redisCache != null) await _redisCache.RemoveByPrefixAsync(CachePrefix);

            return true;
        }
    }
}
