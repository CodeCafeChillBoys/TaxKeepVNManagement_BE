using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Consultations;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVNManagementSystem.Attributes;

namespace TaxKeepVNManagementSystem.Controllers
{
    /// <summary>
    /// Controller điều phối tin nhắn trò chuyện trực tiếp trong ca tư vấn 1-1 giữa Khách hàng và Chuyên gia (Đặc tả 5 & Flow 4)
    /// Tuân thủ nghiêm ngặt API Guidelines: Resource-based URLs, 201 Created cho POST, Paging/Sorting/Searching Metadata và Content Negotiation.
    /// </summary>
    [ApiController]
    [Route("api/v1/consultations/{bookingId:guid}")]
    [Authorize]
    [Tags("Consultation Chat - Trò Chuyện Trực Tuyến 1-1 Với Chuyên Gia")]
    [Produces("application/json", "application/xml")]
    public class ConsultationChatController : ControllerBase
    {
        private readonly IConsultationChatService _chatService;

        public ConsultationChatController(IConsultationChatService chatService)
        {
            _chatService = chatService;
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst("userId")?.Value 
                     ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }

        private string GetCurrentUserRole()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value 
                ?? User.FindFirst("role")?.Value 
                ?? "taxpayer";
        }

        /// <summary>
        /// Gửi tin nhắn mới vào phòng chat tư vấn 1-1 (Mục 2.1 & 3.3 API Guidelines: 201 Created)
        /// POST /api/v1/consultations/{bookingId}/messages
        /// </summary>
        [HttpPost("messages", Name = "SendConsultationMessage")]
        [HasPermission("consultation.join")]
        [EndpointSummary("Gửi tin nhắn mới trong phòng chat tư vấn 1-1")]
        [ProducesResponseType(typeof(ApiResponse<ConsultationMessageDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SendMessage(
            [FromRoute] Guid bookingId,
            [FromBody] SendConsultationMessageRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));
            }

            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();
            var result = await _chatService.SendMessageAsync(userId, role, bookingId, request);

            return CreatedAtAction(
                nameof(GetMessageById),
                new { bookingId, messageId = result.Id },
                ApiResponse<ConsultationMessageDto>.Ok(result, "Gửi tin nhắn thành công."));
        }

        /// <summary>
        /// Lấy toàn bộ lịch sử trò chuyện trong ca tư vấn (Mục 2.2 API Guidelines: Searching, Sorting, Paging)
        /// GET /api/v1/consultations/{bookingId}/messages?search=...&sort=createdAt&page=1&size=50
        /// </summary>
        [HttpGet("messages", Name = "GetConsultationChatHistory")]
        [HasPermission("consultation.join")]
        [EndpointSummary("Lấy danh sách tin nhắn và ngữ cảnh ca tư vấn")]
        [ProducesResponseType(typeof(ApiResponse<ConsultationChatHistoryResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetChatHistory(
            [FromRoute] Guid bookingId,
            [FromQuery] ConsultationMessageQueryParameters query)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();
            var result = await _chatService.GetChatHistoryAsync(userId, role, bookingId, query);
            return Ok(ApiResponse<ConsultationChatHistoryResponse>.Ok(result, "Tải lịch sử tin nhắn thành công."));
        }

        /// <summary>
        /// Lấy chi tiết một tin nhắn cụ thể theo ID trong ca tư vấn (Mục 2.1 API Guidelines: Resource-based)
        /// GET /api/v1/consultations/{bookingId}/messages/{messageId}
        /// </summary>
        [HttpGet("messages/{messageId:guid}", Name = "GetConsultationMessageById")]
        [HasPermission("consultation.join")]
        [EndpointSummary("Lấy thông tin chi tiết một tin nhắn")]
        [ProducesResponseType(typeof(ApiResponse<ConsultationMessageDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetMessageById(
            [FromRoute] Guid bookingId,
            [FromRoute] Guid messageId)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();
            var result = await _chatService.GetMessageByIdAsync(userId, role, bookingId, messageId);
            return Ok(ApiResponse<ConsultationMessageDto>.Ok(result, "Lấy thông tin tin nhắn thành công."));
        }

        /// <summary>
        /// Cập nhật trạng thái đã đọc của tin nhắn (Mục 2.1 API Guidelines: Resource-based PATCH)
        /// PATCH /api/v1/consultations/{bookingId}/messages/read-status
        /// </summary>
        [HttpPatch("messages/read-status", Name = "UpdateConsultationMessagesReadStatus")]
        [HasPermission("consultation.join")]
        [EndpointSummary("Đánh dấu tin nhắn đối phương gửi đến là đã đọc")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateReadStatus([FromRoute] Guid bookingId)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();
            var result = await _chatService.MarkMessagesAsReadAsync(userId, role, bookingId);
            return Ok(ApiResponse<bool>.Ok(result, "Đã cập nhật trạng thái đã đọc của tin nhắn."));
        }

        /// <summary>
        /// Bắn tín hiệu trạng thái gõ phím qua tài nguyên RESTful (Mục 2.1 API Guidelines: Resource-based typing-indicators)
        /// POST /api/v1/consultations/{bookingId}/typing-indicators
        /// </summary>
        [HttpPost("typing-indicators", Name = "SendConsultationTypingIndicator")]
        [HasPermission("consultation.join")]
        [EndpointSummary("Gửi tín hiệu đang gõ phím tới phòng tư vấn")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        public async Task<IActionResult> SendTypingIndicator(
            [FromRoute] Guid bookingId,
            [FromQuery] bool isTyping = true)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();
            var result = await _chatService.SendTypingIndicatorAsync(userId, role, bookingId, isTyping);
            return Ok(ApiResponse<bool>.Ok(result, "Đã phát tín hiệu trạng thái gõ phím."));
        }
    }
}
