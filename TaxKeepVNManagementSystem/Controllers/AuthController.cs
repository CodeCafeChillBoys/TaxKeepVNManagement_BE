using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaxKeepVN.Application.DTOs.Requests.Auth;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVNManagementSystem.Controllers
{
    [ApiController]
    [Route("api/v1/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ITokenRevocationService _tokenRevocationService;
        private readonly IOcrService _ocrService;

        public AuthController(
            IAuthService authService, 
            ITokenRevocationService tokenRevocationService,
            IOcrService ocrService)
        {
            _authService = authService;
            _tokenRevocationService = tokenRevocationService;
            _ocrService = ocrService;
        }

        /// <summary>
        /// Bóc tách thông tin CCCD khi đăng ký người nộp thuế (eKYC / Onboarding).
        /// Tự động kiểm tra trùng lặp tài khoản và trả dữ liệu để FE tự điền Form đăng ký.
        /// </summary>
        [HttpPost("cccd-extractions", Name = "ExtractUserCccdOcr")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ExtractUserCccd([FromForm] TaxKeepVN.Application.DTOs.OcrAI.OcrDocumentUploadRequestDto request)
        {
            var result = await _ocrService.ProcessUserCccdOcrAsync(request.File, request.BackFile);
            return Ok(ApiResponse<TaxKeepVN.Application.DTOs.OcrAI.UserCccdOcrResponseDto>.Ok(result, "Bóc tách thông tin CCCD người nộp thuế thành công."));
        }

        /// <summary>Đăng ký tài khoản mới bằng số CCCD</summary>
        [HttpPost("register")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var result = await _authService.RegisterAsync(request);
            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<object>.Ok(result, "Đăng ký tài khoản thành công. Vui lòng kiểm tra email để xác minh tài khoản."));
        }

        /// <summary>
        /// Xác minh email qua token trong link được gửi sau khi đăng ký.
        /// FE truyền token qua query string: GET /api/v1/auth/verify-email?token=...
        /// </summary>
        [HttpGet("verify-email")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> VerifyEmail([FromQuery] string token)
        {
            await _authService.VerifyEmailAsync(token);
            return Ok(ApiResponse<object>.Ok(new { }, "Xác minh email thành công. Bạn có thể đăng nhập ngay bây giờ."));
        }

        /// <summary>
        /// Gửi lại email xác minh cho tài khoản chưa được xác minh.
        /// Body: { "email": "user@example.com" }
        /// </summary>
        [HttpPost("resend-verification")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequest request)
        {
            await _authService.ResendVerificationEmailAsync(request.Email);
            return Ok(ApiResponse<object>.Ok(new { }, "Nếu email tồn tại và chưa được xác minh, chúng tôi đã gửi lại email xác minh."));
        }

        /// <summary>
        /// Yêu cầu đặt lại mật khẩu qua email.
        /// Body: { "email": "user@example.com" }
        /// </summary>
        [HttpPost("forgot-password")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            await _authService.ForgotPasswordAsync(request);
            return Ok(ApiResponse<object>.Ok(new { }, "Nếu email tồn tại trong hệ thống, chúng tôi đã gửi liên kết đặt lại mật khẩu đến hộp thư của bạn."));
        }

        /// <summary>
        /// Kiểm tra tính hợp lệ và thời hạn của token đặt lại mật khẩu trước khi người dùng nhập form mới.
        /// GET /api/v1/auth/validate-reset-token?token=...
        /// </summary>
        [HttpGet("validate-reset-token")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ValidateResetToken([FromQuery] string token)
        {
            var isValid = await _authService.ValidateResetTokenAsync(token);
            if (!isValid)
            {
                return BadRequest(ApiResponse<object>.Fail("INVALID_OR_EXPIRED_TOKEN", "Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn."));
            }
            return Ok(ApiResponse<object>.Ok(new { valid = true }, "Token đặt lại mật khẩu hợp lệ."));
        }

        /// <summary>
        /// Đặt lại mật khẩu mới bằng token đã nhận được từ email.
        /// Body: { "token": "...", "newPassword": "...", "confirmNewPassword": "..." }
        /// </summary>
        [HttpPost("reset-password")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            await _authService.ResetPasswordAsync(request);
            return Ok(ApiResponse<object>.Ok(new { }, "Đặt lại mật khẩu thành công. Bạn có thể đăng nhập bằng mật khẩu mới."));
        }

        /// <summary>Đăng nhập bằng số CCCD và mật khẩu, nhận JWT token</summary>
        [HttpPost("login")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var result = await _authService.LoginAsync(request);
            return Ok(ApiResponse<object>.Ok(result, "Đăng nhập thành công."));
        }

        /// <summary>Đăng xuất — thu hồi JWT token hiện tại</summary>
        [HttpPost("logout")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Logout()
        {
            // Lấy raw token từ Authorization header
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (authHeader == null || !authHeader.StartsWith("Bearer "))
            {
                throw new UnauthorizedException("MISSING_TOKEN", "Không tìm thấy token xác thực.");
            }

            var rawToken = authHeader.Substring("Bearer ".Length).Trim();

            // Đọc claims từ token (không cần validate lại, middleware đã làm)
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(rawToken);

            var jti = jwtToken.Claims
                .FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;

            if (string.IsNullOrEmpty(jti))
            {
                throw new UnauthorizedException("INVALID_TOKEN", "Token không hợp lệ.");
            }

            // Lấy thời điểm token hết hạn
            var expClaim = jwtToken.Claims
                .FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Exp)?.Value;

            var expiresAt = expClaim != null
                ? DateTimeOffset.FromUnixTimeSeconds(long.Parse(expClaim))
                : DateTimeOffset.UtcNow.AddMinutes(1);

            // Thêm JTI vào blacklist
            await _tokenRevocationService.RevokeAsync(jti, expiresAt);

            return Ok(ApiResponse<object>.Ok(new { }, "Đăng xuất thành công."));
        }
        /// <summary>Đổi mật khẩu — yêu cầu đăng nhập</summary>
        [HttpPut("change-password")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            // Lấy userId từ JWT claim "userId" đã được nhúng lúc login
            var userIdClaim = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                throw new UnauthorizedException("INVALID_TOKEN", "Không thể xác định danh tính người dùng từ token.");
            }

            await _authService.ChangePasswordAsync(userId, request);

            return Ok(ApiResponse<object>.Ok(new { }, "Đổi mật khẩu thành công."));
        }
    }
}
