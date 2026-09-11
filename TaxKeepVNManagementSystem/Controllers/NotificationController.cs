using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVNManagementSystem.Controllers
{
    [ApiController]
    [Route("api/v1/notifications")]
    // [Authorize(Roles = "TAXPAYER")]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationService _notificationService;

        public NotificationController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        /// <summary>
        /// Lấy danh sách thông báo của người dùng.
        /// GET /api/v1/notifications?page=1&size=10&search=tuổi&sort=-createdAt&isRead=false
        /// </summary>
        [HttpGet(Name = "GetMyNotifications")]
        public async Task<IActionResult> GetMyNotifications([FromQuery] NotificationQueryParameters query)
        {
            // TODO: Thay bằng User.FindFirst(ClaimTypes.NameIdentifier) khi JWT được tích hợp
            var userIdStr = "c1234567-89ab-cdef-0123-456789abcdef";
            _ = Guid.TryParse(userIdStr, out Guid userId);

            var data = await _notificationService.GetUserNotificationsAsync(userId, query);
            return Ok(ApiResponse<object>.Ok(data, "Lấy danh sách thông báo thành công."));
        }

        /// <summary>
        /// Đánh dấu thông báo là đã đọc.
        /// PATCH /api/v1/notifications/{id}/read
        /// </summary>
        [HttpPatch("{id:guid}/read", Name = "MarkNotificationAsRead")]
        public async Task<IActionResult> MarkAsRead([FromRoute] Guid id)
        {
            // TODO: Thay bằng User.FindFirst(ClaimTypes.NameIdentifier) khi JWT được tích hợp
            var userIdStr = "c1234567-89ab-cdef-0123-456789abcdef";
            _ = Guid.TryParse(userIdStr, out Guid userId);

            await _notificationService.MarkAsReadAsync(id, userId);
            return Ok(ApiResponse<object>.Ok(null, "Đánh dấu đã đọc thành công."));
        }
    }
}
