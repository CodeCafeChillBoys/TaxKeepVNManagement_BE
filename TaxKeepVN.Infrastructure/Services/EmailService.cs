using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVN.Infrastructure.Services
{
    /// <summary>
    /// Triển khai gửi email qua SMTP (cấu hình từ appsettings.json mục "Email").
    /// </summary>
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class EmailService : IEmailService
    {
        private readonly string _smtpHost;
        private readonly int _smtpPort;
        private readonly string _smtpUser;
        private readonly string _smtpPassword;
        private readonly string _fromAddress;
        private readonly string _fromDisplayName;
        private readonly bool _enableSsl;

        public EmailService(IConfiguration configuration)
        {
            var section = configuration.GetSection("Email");
            _smtpHost        = section["SmtpHost"]      ?? throw new InvalidOperationException("Email:SmtpHost chưa được cấu hình.");
            _smtpPort        = int.Parse(section["SmtpPort"] ?? "587");
            _smtpUser        = section["SmtpUser"]      ?? throw new InvalidOperationException("Email:SmtpUser chưa được cấu hình.");
            _smtpPassword    = section["SmtpPassword"]  ?? throw new InvalidOperationException("Email:SmtpPassword chưa được cấu hình.");
            _fromAddress     = section["FromAddress"]   ?? _smtpUser;
            _fromDisplayName = section["FromDisplayName"] ?? "TaxKeepVN";
            _enableSsl       = bool.Parse(section["EnableSsl"] ?? "true");
        }

        /// <inheritdoc />
        public async Task SendEmailVerificationAsync(string toEmail, string fullName, string verificationLink)
        {
            var subject = "[TaxKeepVN] Xác minh địa chỉ email của bạn";
            var htmlBody = BuildVerificationEmailHtml(fullName, verificationLink);
            await SendAsync(toEmail, subject, htmlBody);
        }

        /// <inheritdoc />
        public async Task SendPasswordResetAsync(string toEmail, string fullName, string resetLink)
        {
            var subject = "[TaxKeepVN] Yêu cầu đặt lại mật khẩu";
            var htmlBody = BuildPasswordResetEmailHtml(fullName, resetLink);
            await SendAsync(toEmail, subject, htmlBody);
        }

        /// <inheritdoc />
        public async Task SendAsync(string toEmail, string subject, string htmlBody)
        {
            using var client = new SmtpClient(_smtpHost, _smtpPort)
            {
                EnableSsl   = _enableSsl,
                Credentials = new NetworkCredential(_smtpUser, _smtpPassword),
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            using var message = new MailMessage
            {
                From       = new MailAddress(_fromAddress, _fromDisplayName),
                Subject    = subject,
                Body       = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            await client.SendMailAsync(message);
        }

        private static string HtmlEncode(string text)
            => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

        // ── Template email xác minh ──────────────────────────────────────────
        private static string BuildVerificationEmailHtml(string fullName, string verificationLink)
        {
            return $@"
<!DOCTYPE html>
<html lang=""vi"">
<head>
  <meta charset=""UTF-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
  <title>Xác minh email - TaxKeepVN</title>
</head>
<body style=""margin:0;padding:0;background:#f4f7fb;font-family:Segoe UI,Arial,sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background:#f4f7fb;padding:40px 0;"">
    <tr>
      <td align=""center"">
        <table width=""560"" cellpadding=""0"" cellspacing=""0"" style=""background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,.08);"">
          <!-- Header -->
          <tr>
            <td style=""background:#1a73e8;padding:32px 40px;text-align:center;"">
              <h1 style=""margin:0;color:#ffffff;font-size:24px;font-weight:700;letter-spacing:.5px;"">TaxKeepVN</h1>
              <p style=""margin:6px 0 0;color:#d2e3fc;font-size:13px;"">Nền tảng quản lý thuế thu nhập cá nhân</p>
            </td>
          </tr>
          <!-- Body -->
          <tr>
            <td style=""padding:40px 40px 32px;"">
              <p style=""margin:0 0 16px;font-size:16px;color:#202124;"">Xin chào <strong>{HtmlEncode(fullName)}</strong>,</p>
              <p style=""margin:0 0 24px;font-size:14px;color:#5f6368;line-height:1.6;"">
                Cảm ơn bạn đã đăng ký tài khoản tại <strong>TaxKeepVN</strong>. Vui lòng nhấn vào nút bên dưới để xác minh địa chỉ email và kích hoạt tài khoản của bạn.
              </p>
              <table cellpadding=""0"" cellspacing=""0"" style=""margin:0 auto 32px;"">
                <tr>
                  <td style=""background:#1a73e8;border-radius:8px;"">
                    <a href=""{verificationLink}""
                       style=""display:inline-block;padding:14px 36px;color:#ffffff;font-size:15px;font-weight:600;text-decoration:none;letter-spacing:.3px;"">
                      Xác minh email
                    </a>
                  </td>
                </tr>
              </table>
              <p style=""margin:0 0 8px;font-size:13px;color:#5f6368;"">Hoặc copy đường dẫn sau vào trình duyệt:</p>
              <p style=""margin:0 0 24px;font-size:12px;color:#1a73e8;word-break:break-all;"">{verificationLink}</p>
              <p style=""margin:0;font-size:13px;color:#9aa0a6;"">
                ⚠️ Liên kết này có hiệu lực trong <strong>24 giờ</strong>. Nếu bạn không đăng ký tài khoản, hãy bỏ qua email này.
              </p>
            </td>
          </tr>
          <!-- Footer -->
          <tr>
            <td style=""background:#f8f9fa;padding:20px 40px;text-align:center;border-top:1px solid #e8eaed;"">
              <p style=""margin:0;font-size:12px;color:#9aa0a6;"">© 2026 TaxKeepVN. Mọi quyền được bảo lưu.</p>
            </td>
          </tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>";
        }

        // ── Template email đặt lại mật khẩu ─────────────────────────────────
        private static string BuildPasswordResetEmailHtml(string fullName, string resetLink)
        {
            return $@"
<!DOCTYPE html>
<html lang=""vi"">
<head>
  <meta charset=""UTF-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
  <title>Đặt lại mật khẩu - TaxKeepVN</title>
</head>
<body style=""margin:0;padding:0;background:#f4f7fb;font-family:Segoe UI,Arial,sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background:#f4f7fb;padding:40px 0;"">
    <tr>
      <td align=""center"">
        <table width=""560"" cellpadding=""0"" cellspacing=""0"" style=""background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,.08);"">
          <!-- Header -->
          <tr>
            <td style=""background:#d93025;padding:32px 40px;text-align:center;"">
              <h1 style=""margin:0;color:#ffffff;font-size:24px;font-weight:700;letter-spacing:.5px;"">TaxKeepVN</h1>
              <p style=""margin:6px 0 0;color:#fce8e6;font-size:13px;"">Yêu cầu đặt lại mật khẩu</p>
            </td>
          </tr>
          <!-- Body -->
          <tr>
            <td style=""padding:40px 40px 32px;"">
              <p style=""margin:0 0 16px;font-size:16px;color:#202124;"">Xin chào <strong>{HtmlEncode(fullName)}</strong>,</p>
              <p style=""margin:0 0 24px;font-size:14px;color:#5f6368;line-height:1.6;"">
                Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản TaxKeepVN liên kết với email này. Vui lòng nhấn vào nút bên dưới để tiến hành tạo mật khẩu mới.
              </p>
              <table cellpadding=""0"" cellspacing=""0"" style=""margin:0 auto 32px;"">
                <tr>
                  <td style=""background:#d93025;border-radius:8px;"">
                    <a href=""{resetLink}""
                       style=""display:inline-block;padding:14px 36px;color:#ffffff;font-size:15px;font-weight:600;text-decoration:none;letter-spacing:.3px;"">
                      Đặt lại mật khẩu
                    </a>
                  </td>
                </tr>
              </table>
              <p style=""margin:0 0 8px;font-size:13px;color:#5f6368;"">Hoặc copy đường dẫn sau vào trình duyệt:</p>
              <p style=""margin:0 0 24px;font-size:12px;color:#d93025;word-break:break-all;"">{resetLink}</p>
              <p style=""margin:0 0 8px;font-size:13px;color:#5f6368;"">
                ⏱️ Liên kết này có hiệu lực trong <strong>15 phút</strong>.
              </p>
              <p style=""margin:0;font-size:13px;color:#9aa0a6;"">
                🔒 Nếu bạn không yêu cầu đặt lại mật khẩu, bạn có thể an tâm bỏ qua email này. Mật khẩu hiện tại của bạn vẫn được giữ an toàn.
              </p>
            </td>
          </tr>
          <!-- Footer -->
          <tr>
            <td style=""background:#f8f9fa;padding:20px 40px;text-align:center;border-top:1px solid #e8eaed;"">
              <p style=""margin:0;font-size:12px;color:#9aa0a6;"">© 2026 TaxKeepVN. Mọi quyền được bảo lưu.</p>
            </td>
          </tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>";
        }
    }
}
