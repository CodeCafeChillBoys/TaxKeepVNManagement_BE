using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.DTOs.TaxAI;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVNManagementSystem.Controllers
{
    [ApiController]
    [Route("api/v1/tax-chat")]
    [Authorize]
    [Produces("application/json")]
    public class TaxChatController : ControllerBase
    {
        private readonly ITaxChatService _chatService;

        public TaxChatController(ITaxChatService chatService)
        {
            _chatService = chatService;
        }

        /// <summary>
        /// Gửi câu hỏi tư vấn luật thuế tới AI (truyền sessionId nếu tiếp tục cuộc trò chuyện)
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<TaxChatResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> Ask([FromBody] TaxChatRequest request)
        {
            try
            {
                var userId = GetUserIdFromToken();
                var result = await _chatService.AskAsync(userId, request);
                return Ok(ApiResponse<TaxChatResponse>.Ok(result, "Phản hồi từ Tax AI thành công."));
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = "Không thể kết nối tới Tax AI Service.",
                    detail = ex.Message
                });
            }
        }

        /// <summary>
        /// Lấy danh sách các phiên trò chuyện của người dùng hiện tại
        /// </summary>
        [HttpGet("sessions")]
        [ProducesResponseType(typeof(ApiResponse<List<ChatSessionResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetSessions()
        {
            try
            {
                var userId = GetUserIdFromToken();
                var result = await _chatService.GetSessionsAsync(userId);
                return Ok(ApiResponse<List<ChatSessionResponse>>.Ok(result, "Lấy danh sách phiên chat thành công."));
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = "Không thể kết nối tới Tax AI Service.",
                    detail = ex.Message
                });
            }
        }

        /// <summary>
        /// Lấy toàn bộ lịch sử tin nhắn trong một phiên trò chuyện
        /// </summary>
        [HttpGet("sessions/{sessionId:guid}/messages")]
        [ProducesResponseType(typeof(ApiResponse<List<ChatMessageResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMessages([FromRoute] Guid sessionId)
        {
            try
            {
                var result = await _chatService.GetMessagesAsync(sessionId);
                return Ok(ApiResponse<List<ChatMessageResponse>>.Ok(result, "Lấy danh sách tin nhắn thành công."));
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = "Không thể kết nối tới Tax AI Service.",
                    detail = ex.Message
                });
            }
        }

        /// <summary>
        /// Xóa một phiên trò chuyện
        /// </summary>
        [HttpDelete("sessions/{sessionId:guid}")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteSession([FromRoute] Guid sessionId)
        {
            try
            {
                var success = await _chatService.DeleteSessionAsync(sessionId);
                if (!success)
                {
                    return NotFound(ApiResponse<object>.Fail("SESSION_NOT_FOUND", "Không tìm thấy phiên trò chuyện cần xóa."));
                }
                return Ok(ApiResponse<object>.Ok(null, "Đã xóa phiên trò chuyện thành công."));
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = "Không thể kết nối tới Tax AI Service.",
                    detail = ex.Message
                });
            }
        }

        // ── Helper: trích userId từ JWT claim ──────────────────────────────────
        private Guid GetUserIdFromToken()
        {
            var userIdClaim = User.FindFirst("userId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                throw new UnauthorizedException("INVALID_TOKEN", "Không thể xác định danh tính người dùng từ token.");
            }
            return userId;
        }
    }
}
