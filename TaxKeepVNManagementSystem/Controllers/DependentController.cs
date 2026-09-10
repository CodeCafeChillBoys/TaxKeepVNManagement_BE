using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
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

        [HttpGet("reminders/age-transitions")]
        public async Task<IActionResult> GetAgeTransitionReminders([FromQuery] int? taxYear)
        {
            // Dummy user ID for now since JWT is not fully setup
            var userIdStr = "c1234567-89ab-cdef-0123-456789abcdef"; // Mock UUID
            _ = Guid.TryParse(userIdStr, out Guid userId);

            // Default to current year if not provided
            int yearToCheck = taxYear ?? 2026;

            var data = await _reminderService.GetAgeTransitionRemindersAsync(userId, yearToCheck);

            return Ok(ApiResponse<object>.Ok(data, "Lấy danh sách nhắc nhở chuyển nhóm tuổi NPT thành công."));
        }
    }
}
