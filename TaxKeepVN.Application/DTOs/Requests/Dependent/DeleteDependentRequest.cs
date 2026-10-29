using System.ComponentModel.DataAnnotations;

namespace TaxKeepVN.Application.DTOs.Requests.Dependent
{
    /// <summary>
    /// Request body cho API xóa mềm (vô hiệu hóa) người phụ thuộc.
    /// </summary>
    public class DeleteDependentRequest
    {
        /// <summary>
        /// Lý do vô hiệu hóa người phụ thuộc.
        /// Ví dụ: "Không còn là người phụ thuộc", "Người phụ thuộc đã mất", "Đã đủ 18 tuổi và có thu nhập riêng".
        /// </summary>
        [MaxLength(500, ErrorMessage = "Lý do không được vượt quá 500 ký tự.")]
        public string? Reason { get; set; }
    }
}
