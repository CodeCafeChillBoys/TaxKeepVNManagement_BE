using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaxKeepVN.Application.DTOs.Requests.Dependent;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVNManagementSystem.Controllers
{
    [ApiController]
    [Route("api/dependents")]
    [Authorize]
    public class DependentController : ControllerBase
    {
        private readonly IDependentService _dependentService;

        public DependentController(IDependentService dependentService)
        {
            _dependentService = dependentService;
        }

        /// <summary>
        /// Đăng ký người phụ thuộc mới cho người nộp thuế đang đăng nhập.
        /// Sau khi tạo thành công, sử dụng dependentId trong response để upload
        /// giấy tờ minh chứng qua endpoint: POST /api/dependents/{dependentId}/documents
        /// </summary>
        [HttpPost]
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

        // ── Helper ──────────────────────────────────────────────────────────────
        private Guid GetUserIdFromToken()
        {
            var userIdClaim = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                throw new UnauthorizedException("INVALID_TOKEN",
                    "Không thể xác định danh tính người dùng từ token.");
            }
            return userId;
        }
    }
}
