using System.ComponentModel.DataAnnotations;

namespace TaxKeepVN.Application.DTOs.Requests.Auth
{
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class ForgotPasswordRequest
    {
        [Required(ErrorMessage = "Email không được để trống.")]
        [EmailAddress(ErrorMessage = "Địa chỉ email không hợp lệ.")]
        public string Email { get; set; } = string.Empty;
    }
}
