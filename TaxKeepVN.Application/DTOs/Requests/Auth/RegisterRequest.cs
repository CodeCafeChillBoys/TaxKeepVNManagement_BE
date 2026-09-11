using System;
using System.ComponentModel.DataAnnotations;

namespace TaxKeepVN.Application.DTOs.Requests.Auth
{
    public class RegisterRequest
    {
        /// <summary>Số căn cước công dân (12 chữ số)</summary>
        [Required(ErrorMessage = "Số CCCD không được để trống.")]
        [RegularExpression(@"^\d{12}$", ErrorMessage = "Số CCCD phải gồm đúng 12 chữ số.")]
        public string CitizenId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Họ tên không được để trống.")]
        [MaxLength(150, ErrorMessage = "Họ tên không được vượt quá 150 ký tự.")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email không được để trống.")]
        [EmailAddress(ErrorMessage = "Địa chỉ email không hợp lệ.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu không được để trống.")]
        [MinLength(8, ErrorMessage = "Mật khẩu phải có ít nhất 8 ký tự.")]
        public string Password { get; set; } = string.Empty;

        [RegularExpression(@"^(0|\+84)[0-9]{9}$", ErrorMessage = "Số điện thoại không hợp lệ.")]
        public string? PhoneNumber { get; set; }

        public DateOnly? DateOfBirth { get; set; }

        [MaxLength(300)]
        public string? Address { get; set; }
    }
}
