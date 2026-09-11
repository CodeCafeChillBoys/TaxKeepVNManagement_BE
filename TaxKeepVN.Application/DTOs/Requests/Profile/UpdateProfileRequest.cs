using System;
using System.ComponentModel.DataAnnotations;

namespace TaxKeepVN.Application.DTOs.Requests.Profile
{
    public class UpdateProfileRequest
    {
        [Required(ErrorMessage = "Họ tên không được để trống.")]
        [MaxLength(150, ErrorMessage = "Họ tên không được vượt quá 150 ký tự.")]
        public string FullName { get; set; } = string.Empty;

        [RegularExpression(@"^(0|\+84)[0-9]{9}$", ErrorMessage = "Số điện thoại không hợp lệ.")]
        public string? PhoneNumber { get; set; }

        public DateOnly? DateOfBirth { get; set; }

        [MaxLength(300, ErrorMessage = "Địa chỉ không được vượt quá 300 ký tự.")]
        public string? Address { get; set; }

        /// <summary>
        /// Mã số thuế cá nhân (MST).
        /// - 10 chữ số: MST cấp theo định danh cũ hoặc dành cho người nước ngoài.
        /// - 12 chữ số: Số CCCD được dùng làm MST theo Luật Quản lý Thuế sửa đổi.
        /// </summary>
        [RegularExpression(@"^\d{10}$|^\d{12}$",
            ErrorMessage = "Mã số thuế chỉ được phép là chuỗi đúng 10 chữ số (MST cũ/người nước ngoài) hoặc đúng 12 chữ số (số CCCD làm MST theo quy định mới).")]
        public string? TaxIdNumber { get; set; }

        /// <summary>
        /// Cờ xác nhận người dùng đã hoàn tất thủ tục đăng ký thuế lần đầu với Cơ quan Thuế.
        /// BẮT BUỘC phải là <c>true</c> khi <see cref="TaxIdNumber"/> có đúng 12 chữ số.
        /// Không yêu cầu khi <see cref="TaxIdNumber"/> là 10 chữ số.
        /// </summary>
        public bool? IsTaxRegisteredConfirmed { get; set; }
    }
}
