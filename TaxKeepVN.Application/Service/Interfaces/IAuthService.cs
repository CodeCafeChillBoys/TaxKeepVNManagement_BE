using System;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Requests.Auth;
using TaxKeepVN.Application.DTOs.Responses.Auth;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface IAuthService
    {
        /// <summary>Đăng ký tài khoản mới, trả về thông tin user (không có token)</summary>
        Task<RegisterResponse> RegisterAsync(RegisterRequest request);

        /// <summary>Đăng nhập bằng số CCCD và mật khẩu, trả về JWT token</summary>
        Task<AuthResponse> LoginAsync(LoginRequest request);

        /// <summary>Đổi mật khẩu — yêu cầu đăng nhập, xác minh mật khẩu hiện tại</summary>
        Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
    }
}
