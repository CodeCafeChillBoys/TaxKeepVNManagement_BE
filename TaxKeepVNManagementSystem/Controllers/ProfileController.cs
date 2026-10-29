using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaxKeepVN.Application.DTOs.Requests.Profile;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVNManagementSystem.Controllers
{
    [ApiController]
    [Route("api/profile")]
    [Authorize]
    public class ProfileController : ControllerBase
    {
        private readonly IProfileService _profileService;

        public ProfileController(IProfileService profileService)
        {
            _profileService = profileService;
        }

        /// <summary>Lấy thông tin cá nhân của người dùng đang đăng nhập</summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetProfile()
        {
            var userId = GetUserIdFromToken();
            var result = await _profileService.GetProfileAsync(userId);
            return Ok(ApiResponse<object>.Ok(result, "Lấy thông tin cá nhân thành công."));
        }

        /// <summary>Cập nhật thông tin cá nhân (họ tên, số điện thoại, địa chỉ, ngày sinh, mã số thuế)</summary>
        [HttpPut]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            var userId = GetUserIdFromToken();
            var result = await _profileService.UpdateProfileAsync(userId, request);
            return Ok(ApiResponse<object>.Ok(result, "Cập nhật thông tin cá nhân thành công."));
        }

        // ── Helper: trích userId từ JWT claim ──────────────────────────────────
        private Guid GetUserIdFromToken()
        {
            var userIdClaim = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                throw new UnauthorizedException("INVALID_TOKEN", "Không thể xác định danh tính người dùng từ token.");
            }
            return userId;
        }
    }
}
