using System;

namespace TaxKeepVN.Application.DTOs.Responses.Auth
{
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class RegisterResponse
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string CitizenId { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public string UserRole { get; set; } = string.Empty;
        public bool IsVerified { get; set; }

        /// <summary>
        /// True nếu hệ thống đã gửi email xác minh — FE hiển thị thông báo "Vui lòng kiểm tra hộp thư".
        /// </summary>
        public bool RequiresEmailVerification { get; set; }
    }
}

