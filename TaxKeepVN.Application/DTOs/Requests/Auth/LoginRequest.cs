using System.ComponentModel.DataAnnotations;

namespace TaxKeepVN.Application.DTOs.Requests.Auth
{
    public class LoginRequest
    {
        /// <summary>Số căn cước công dân dùng để đăng nhập</summary>
        [Required(ErrorMessage = "Số CCCD không được để trống.")]
        public string CitizenId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu không được để trống.")]
        public string Password { get; set; } = string.Empty;
    }
}
