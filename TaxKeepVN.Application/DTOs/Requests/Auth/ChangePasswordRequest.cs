using System.ComponentModel.DataAnnotations;

namespace TaxKeepVN.Application.DTOs.Requests.Auth
{
    public class ChangePasswordRequest
    {
        [Required(ErrorMessage = "Mật khẩu hiện tại không được để trống.")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu mới không được để trống.")]
        [MinLength(8, ErrorMessage = "Mật khẩu mới phải có ít nhất 8 ký tự.")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Xác nhận mật khẩu không được để trống.")]
        [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp với mật khẩu mới.")]
        public string ConfirmNewPassword { get; set; } = string.Empty;
    }
}
