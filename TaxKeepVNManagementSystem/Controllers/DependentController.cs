using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Requests.Dependent;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVNManagementSystem.Controllers
{
    [ApiController]
    [Route("api/v1/dependents")]
    [Authorize]
    public class DependentController : ControllerBase
    {
        private readonly IDependentService _dependentService;
        private readonly IDependentReminderService _reminderService;

        public DependentController(
            IDependentService dependentService,
            IDependentReminderService reminderService)
        {
            _dependentService = dependentService;
            _reminderService = reminderService;
        }

        /// <summary>
        /// Đăng ký người phụ thuộc mới cho người nộp thuế đang đăng nhập.
        /// Sau khi tạo thành công, sử dụng dependentId trong response để upload
        /// giấy tờ minh chứng qua endpoint: POST /api/v1/dependents/{dependentId}/documents
        /// </summary>
        [HttpPost(Name = "CreateDependent")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CreateDependent([FromBody] CreateDependentRequest request)
        {
            var taxpayerId = GetUserIdFromToken();
            var result = await _dependentService.CreateDependentAsync(taxpayerId, request);

            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<object>.Ok(result,
                    "Đăng ký người phụ thuộc thành công. " +
                    "Vui lòng upload giấy tờ minh chứng để hoàn tất hồ sơ."));
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
            var userId = GetUserIdFromToken();

            var data = await _reminderService.GetAgeTransitionRemindersAsync(userId, taxYear, query);
            return Ok(ApiResponse<object>.Ok(data, "Lấy danh sách nhắc nhở chuyển nhóm tuổi NPT thành công."));
        }

        // ── Helper ──────────────────────────────────────────────────────────────
        private Guid GetUserIdFromToken()
        {
            var userIdClaim = User.FindFirst("userId")?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                throw new UnauthorizedException("INVALID_TOKEN",
                    "Không thể xác định danh tính người dùng từ token. Vui lòng đăng nhập lại.");
            }
            return userId;
        }
    }
}
