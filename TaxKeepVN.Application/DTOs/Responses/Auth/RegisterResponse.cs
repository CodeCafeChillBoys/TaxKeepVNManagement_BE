using System;

namespace TaxKeepVN.Application.DTOs.Responses.Auth
{
    public class RegisterResponse
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string CitizenId { get; set; } = string.Empty;
        public string UserRole { get; set; } = string.Empty;
        public bool IsVerified { get; set; }
    }
}
