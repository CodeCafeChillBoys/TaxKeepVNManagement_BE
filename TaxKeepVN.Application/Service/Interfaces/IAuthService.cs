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

        /// <summary>Xác minh email qua token trong link được gửi sau khi đăng ký</summary>
        Task VerifyEmailAsync(string token);

        /// <summary>Gửi lại email xác minh cho tài khoản chưa được xác minh</summary>
        Task ResendVerificationEmailAsync(string email);

        /// <summary>Yêu cầu đặt lại mật khẩu — gửi email hướng dẫn chứa token</summary>
        Task ForgotPasswordAsync(ForgotPasswordRequest request);

        /// <summary>Đặt lại mật khẩu mới sử dụng token được gửi qua email</summary>
        Task ResetPasswordAsync(ResetPasswordRequest request);

        /// <summary>Kiểm tra tính hợp lệ và thời hạn của token đặt lại mật khẩu</summary>
        Task<bool> ValidateResetTokenAsync(string token);
    }
}
