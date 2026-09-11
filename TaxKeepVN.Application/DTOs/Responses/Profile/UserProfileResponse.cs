using System;

namespace TaxKeepVN.Application.DTOs.Responses.Profile
{
    public class UserProfileResponse
    {
        public Guid UserId { get; set; }
        public string CitizenId { get; set; } = string.Empty;
        public string? TaxIdNumber { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public DateOnly? DateOfBirth { get; set; }
        public string? Address { get; set; }
        public string UserRole { get; set; } = string.Empty;
        public bool IsVerified { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
