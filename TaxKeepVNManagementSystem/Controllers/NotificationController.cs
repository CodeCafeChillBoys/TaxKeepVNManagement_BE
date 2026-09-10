using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
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

        [HttpGet]
        public async Task<IActionResult> GetMyNotifications()
        {
            var userIdStr = "c1234567-89ab-cdef-0123-456789abcdef"; // Mock UUID
            _ = Guid.TryParse(userIdStr, out Guid userId);

            var data = await _notificationService.GetUserNotificationsAsync(userId);

            return Ok(ApiResponse<object>.Ok(new { items = data }, "Lấy danh sách thông báo thành công."));
        }

        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(Guid id)
        {
            var userIdStr = "c1234567-89ab-cdef-0123-456789abcdef"; // Mock UUID
            _ = Guid.TryParse(userIdStr, out Guid userId);

            await _notificationService.MarkAsReadAsync(id, userId);

            return Ok(ApiResponse<object>.Ok(null, "Đánh dấu đã đọc thành công."));
        }
    }
}
