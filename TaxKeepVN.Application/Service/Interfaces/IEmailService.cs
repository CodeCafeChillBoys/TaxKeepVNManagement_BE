using System.Threading.Tasks;

namespace TaxKeepVN.Application.Service.Interfaces
{
    /// <summary>
    /// Gửi email từ hệ thống (xác minh tài khoản, thông báo, v.v.)
    /// </summary>
    public interface IEmailService
    {
        /// <summary>
        /// Gửi email xác minh tài khoản sau khi đăng ký.
        /// </summary>
        Task SendEmailVerificationAsync(string toEmail, string fullName, string verificationLink);

        /// <summary>
        /// Gửi email đặt lại mật khẩu khi người dùng yêu cầu quên mật khẩu.
        /// </summary>
        Task SendPasswordResetAsync(string toEmail, string fullName, string resetLink);

        /// <summary>
        /// Gửi một email đơn giản với tiêu đề và nội dung HTML tùy chỉnh.
        /// </summary>
        Task SendAsync(string toEmail, string subject, string htmlBody);
    }
}
