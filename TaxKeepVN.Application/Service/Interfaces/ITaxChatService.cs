using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.TaxAI;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface ITaxChatService
    {
        /// <summary>
        /// Gửi câu hỏi tư vấn luật thuế tới AI Service
        /// </summary>
        Task<TaxChatResponse> AskAsync(Guid userId, TaxChatRequest request);

        /// <summary>
        /// Lấy danh sách các phiên trò chuyện của người dùng
        /// </summary>
        Task<List<ChatSessionResponse>> GetSessionsAsync(Guid userId);

        /// <summary>
        /// Lấy toàn bộ tin nhắn trong một phiên trò chuyện
        /// </summary>
        Task<List<ChatMessageResponse>> GetMessagesAsync(Guid sessionId);

        /// <summary>
        /// Xóa một phiên trò chuyện
        /// </summary>
        Task<bool> DeleteSessionAsync(Guid sessionId);
    }
}
