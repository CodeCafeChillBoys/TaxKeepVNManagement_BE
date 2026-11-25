using System;
using System.ComponentModel.DataAnnotations;

namespace TaxKeepVN.Application.DTOs.Requests.Dependent
{
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class UpdateDependentRequest
    {
        [MaxLength(150)]
        public string? FullName { get; set; }

        /// <summary>
        /// Nhóm quan hệ: CHILD | SPOUSE | PARENT | OTHER_DEPENDENT (Tùy chọn)
        /// </summary>
        public string? Relationship { get; set; }

        /// <summary>
        /// Nhóm điều kiện cụ thể (Tùy chọn)
        /// </summary>
        public string? CurrentGroup { get; set; }

        public DateTime? BirthDate { get; set; }

        /// <summary>
        /// Số CCCD (12 chữ số). Tùy chọn.
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
        /// </summary>
        [RegularExpression(@"^\d{10}$|^\d{12}$",
            ErrorMessage = "Mã số thuế chỉ được phép là 10 chữ số (MST cũ) hoặc 12 chữ số (số CCCD làm MST).")]
        public string? TaxIdNumber { get; set; }

        /// <summary>
        /// Tháng bắt đầu tính giảm trừ gia cảnh, format "YYYY-MM".
        /// </summary>
        [RegularExpression(@"^\d{4}-(0[1-9]|1[0-2])$",
            ErrorMessage = "Tháng bắt đầu không đúng định dạng (YYYY-MM). Ví dụ: 2026-01")]
        public string? EffectiveFromMonth { get; set; }

        /// <summary>
        /// Tháng kết thúc tính giảm trừ gia cảnh, format "YYYY-MM".
        /// </summary>
        [RegularExpression(@"^\d{4}-(0[1-9]|1[0-2])$",
            ErrorMessage = "Tháng kết thúc không đúng định dạng (YYYY-MM). Ví dụ: 2026-12")]
        public string? EffectiveToMonth { get; set; }

        [MaxLength(500)]
        public string? Note { get; set; }

        /// <summary>
        /// Xác nhận thay đổi thông tin nhân thân khi hồ sơ đang ở trạng thái ACTIVE (mặc định false).
        /// </summary>
        public bool ConfirmIdentityChange { get; set; } = false;
    }
}
