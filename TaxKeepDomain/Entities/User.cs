using System;

namespace TaxKeepVN.Domain.Entities
{
    public class User
    {
        public Guid UserId { get; set; }

        /// <summary>Mã số thuế cá nhân (MST), có thể chưa có khi đăng ký</summary>
        public string? TaxIdNumber { get; set; }

        /// <summary>Số căn cước công dân — dùng để đăng nhập</summary>
        public string CitizenId { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string? PhoneNumber { get; set; }

        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>taxpayer | admin</summary>
        public string UserRole { get; set; } = "taxpayer";

        public bool IsVerified { get; set; } = false;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        public DateOnly? DateOfBirth { get; set; }

        public string? Address { get; set; }

        /// <summary>active | inactive | suspended</summary>
        public string Status { get; set; } = "active";
    }
}
