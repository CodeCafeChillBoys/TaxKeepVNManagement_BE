using System;
using System.ComponentModel.DataAnnotations;

namespace TaxKeepVN.Application.DTOs.Requests.Dependent
{
    public class CreateDependentRequest
    {
        [Required(ErrorMessage = "Họ tên người phụ thuộc không được để trống.")]
        [MaxLength(150)]
        public string FullName { get; set; } = string.Empty;

        /// <summary>
        /// Nhóm quan hệ: CHILD | SPOUSE | PARENT | OTHER_DEPENDENT
        /// </summary>
        [Required(ErrorMessage = "Nhóm quan hệ người phụ thuộc không được để trống.")]
        public string Relationship { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ngày sinh người phụ thuộc không được để trống.")]
        public DateTime? BirthDate { get; set; }

        /// <summary>
        /// Số CCCD (12 chữ số). Dùng cho người phụ thuộc đã có CCCD.
        /// Bắt buộc phải có CitizenId HOẶC BirthCertNumber.
        /// </summary>
        [RegularExpression(@"^\d{12}$", ErrorMessage = "Số CCCD của người phụ thuộc phải đúng 12 chữ số.")]
        public string? CitizenId { get; set; }

        /// <summary>
        /// Số Giấy khai sinh — dùng cho trẻ em chưa có CCCD.
        /// </summary>
        [MaxLength(50)]
        public string? BirthCertNumber { get; set; }

        /// <summary>
        /// Mã số thuế của người phụ thuộc (nếu có).
        /// Chỉ được phép là 10 hoặc 12 chữ số.
        /// </summary>
        [RegularExpression(@"^\d{10}$|^\d{12}$",
            ErrorMessage = "Mã số thuế chỉ được phép là 10 chữ số (MST cũ) hoặc 12 chữ số (số CCCD làm MST).")]
        public string? TaxIdNumber { get; set; }

        /// <summary>
        /// Tháng bắt đầu tính giảm trừ gia cảnh, format "YYYY-MM".
        /// Ví dụ: "2026-01"
        /// </summary>
        [Required(ErrorMessage = "Tháng bắt đầu tính giảm trừ không được để trống.")]
        [RegularExpression(@"^\d{4}-(0[1-9]|1[0-2])$",
            ErrorMessage = "Tháng bắt đầu không đúng định dạng (YYYY-MM). Ví dụ: 2026-01")]
        public string EffectiveFromMonth { get; set; } = string.Empty;

        /// <summary>
        /// Tháng kết thúc tính giảm trừ gia cảnh, format "YYYY-MM".
        /// Ví dụ: "2026-12"
        /// </summary>
        [Required(ErrorMessage = "Tháng kết thúc tính giảm trừ không được để trống.")]
        [RegularExpression(@"^\d{4}-(0[1-9]|1[0-2])$",
            ErrorMessage = "Tháng kết thúc không đúng định dạng (YYYY-MM). Ví dụ: 2026-12")]
        public string EffectiveToMonth { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Note { get; set; }
    }
}
