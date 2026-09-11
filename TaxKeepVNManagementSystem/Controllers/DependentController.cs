using Microsoft.AspNetCore.Mvc;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVNManagementSystem.Controllers
{
    [ApiController]
    [Route("api/v1/dependents")]
    // [Authorize(Roles = "TAXPAYER")]
    public class DependentController : ControllerBase
    {
        private readonly IDependentReminderService _reminderService;

        public DependentController(IDependentReminderService reminderService)
        {
            _reminderService = reminderService;
        }

        /// <summary>
        /// Lấy danh sách nhắc nhở chuyển nhóm tuổi người phụ thuộc.
        /// GET /api/v1/dependents/reminders/age-transitions?taxYear=2026&page=1&size=10&search=Nguyen&sort=daysRemaining
        /// </summary>
        [HttpGet("reminders/age-transitions", Name = "GetAgeTransitionReminders")]
        public async Task<IActionResult> GetAgeTransitionReminders(
            [FromQuery][Range(2000, 2100, ErrorMessage = "Năm tính thuế phải từ 2000 đến 2100.")] int taxYear = 2026,
            [FromQuery] QueryParameters query = null!)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));

            query ??= new QueryParameters();

            // TODO: Thay bằng User.FindFirst(ClaimTypes.NameIdentifier) khi JWT được tích hợp
            var userIdStr = "c1234567-89ab-cdef-0123-456789abcdef";
            _ = Guid.TryParse(userIdStr, out Guid userId);

            var data = await _reminderService.GetAgeTransitionRemindersAsync(userId, taxYear, query);
            return Ok(ApiResponse<object>.Ok(data, "Lấy danh sách nhắc nhở chuyển nhóm tuổi NPT thành công."));
        }
    }
}
