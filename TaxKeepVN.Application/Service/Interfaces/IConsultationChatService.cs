using System;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Consultations;

namespace TaxKeepVN.Application.Service.Interfaces
{
    /// <summary>
    /// Service quản lý nghiệp vụ nhắn tin trao đổi trực tiếp trong phiên tư vấn chuyên gia 1-1 (Đặc tả 5 & Flow 4)
    /// </summary>
    public interface IConsultationChatService
    {
        /// <summary>
        /// Gửi tin nhắn mới trong phiên tư vấn (Lưu DB + Broadcast SignalR + Ghi nhận Check-in nếu ca CHAT)
        /// </summary>
        Task<ConsultationMessageDto> SendMessageAsync(Guid senderId, string senderRole, Guid bookingId, SendConsultationMessageRequest request);

        /// <summary>
        /// Lấy toàn bộ lịch sử tin nhắn của phiên tư vấn hỗ trợ Searching, Sorting, Paging (Mục 2.2 API Guidelines)
        /// </summary>
        Task<ConsultationChatHistoryResponse> GetChatHistoryAsync(Guid userId, string userRole, Guid bookingId, ConsultationMessageQueryParameters query);

        /// <summary>
        /// Lấy chi tiết một tin nhắn theo ID trong ca tư vấn
        /// </summary>
        Task<ConsultationMessageDto> GetMessageByIdAsync(Guid userId, string userRole, Guid bookingId, Guid messageId);

        /// <summary>
        /// Đánh dấu tất cả tin nhắn đối phương gửi đến là đã đọc
        /// </summary>
        Task<bool> MarkMessagesAsReadAsync(Guid userId, string userRole, Guid bookingId);

        /// <summary>
        /// Bắn thông báo trạng thái đang gõ phím (Typing Indicator)
        /// </summary>
        Task<bool> SendTypingIndicatorAsync(Guid userId, string userRole, Guid bookingId, bool isTyping);
    }
}
