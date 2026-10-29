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
    public class TaxChatService : ITaxChatService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<TaxChatService> _logger;

        public TaxChatService(
            IHttpClientFactory httpClientFactory,
            ILogger<TaxChatService> logger)
        {
            _httpClient = httpClientFactory.CreateClient("TaxAIService");
            _logger = logger;
        }

        // 1. POST /api/tax-chat
        public async Task<TaxChatResponse> AskAsync(Guid userId, TaxChatRequest request)
        {
            var payload = new
            {
                question = request.Question,
                session_id = request.SessionId,
                tax_year = request.TaxYear,
                user_id = userId
            };

            var response = await _httpClient.PostAsJsonAsync("/api/tax-chat", payload);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Lỗi khi chat với Tax AI: {StatusCode} - {Error}", response.StatusCode, error);
                throw new HttpRequestException($"AI Service error: {response.StatusCode} - {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<TaxChatResponse>();
            return result ?? throw new InvalidOperationException("Không nhận được dữ liệu phản hồi từ AI Service.");
        }

        // 2. GET /api/tax-chat/sessions?user_id={userId}
        public async Task<List<ChatSessionResponse>> GetSessionsAsync(Guid userId)
        {
            var url = $"/api/tax-chat/sessions?user_id={userId}";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Lỗi khi lấy danh sách phiên chat: {StatusCode} - {Error}", response.StatusCode, error);
                throw new HttpRequestException($"AI Service error: {response.StatusCode} - {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<List<ChatSessionResponse>>();
            return result ?? new List<ChatSessionResponse>();
        }

        // 3. GET /api/tax-chat/sessions/{sessionId}/messages
        public async Task<List<ChatMessageResponse>> GetMessagesAsync(Guid sessionId)
        {
            var url = $"/api/tax-chat/sessions/{sessionId}/messages";
            var response = await _httpClient.GetAsync(url);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new List<ChatMessageResponse>();
            }

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Lỗi khi lấy tin nhắn phiên {SessionId}: {StatusCode} - {Error}", sessionId, response.StatusCode, error);
                throw new HttpRequestException($"AI Service error: {response.StatusCode} - {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<List<ChatMessageResponse>>();
            return result ?? new List<ChatMessageResponse>();
        }

        // 4. DELETE /api/tax-chat/sessions/{sessionId}
        public async Task<bool> DeleteSessionAsync(Guid sessionId)
        {
            var response = await _httpClient.DeleteAsync($"/api/tax-chat/sessions/{sessionId}");
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Lỗi khi xóa phiên chat {SessionId}: {StatusCode} - {Error}", sessionId, response.StatusCode, error);
                throw new HttpRequestException($"AI Service error: {response.StatusCode} - {error}");
            }

            return true;
        }
    }
}
