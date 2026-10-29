using System.ComponentModel.DataAnnotations;

namespace TaxKeepVN.Application.DTOs.Requests.Dependent
{
    /// <summary>
    /// Request body để chuyển nhóm điều kiện của người phụ thuộc.
    /// Chỉ cần cung cấp nhóm mới và (tùy chọn) ghi chú cập nhật.
    /// Toàn bộ thông tin định danh (FullName, BirthDate, CitizenId, ...) được giữ nguyên.
    /// </summary>
    public class UpdateDependentGroupRequest
    {
        /// <summary>
        /// Nhóm điều kiện mới cần chuyển sang:
        /// - CHILD_UNDER_18 | CHILD_OVER_18_DISABLED | CHILD_OVER_18_STUDYING
        /// - SPOUSE_DISABLED | SPOUSE_RETIRED
        /// - PARENT_DISABLED | PARENT_RETIRED
        /// - OTHER_HELPLESS
        /// Nhóm mới phải cùng Relationship với nhóm hiện tại và phải khác nhóm hiện tại.
        /// </summary>
        [Required(ErrorMessage = "Nhóm điều kiện mới không được để trống.")]
        public string NewGroup { get; set; } = string.Empty;

        /// <summary>
        /// Ghi chú cập nhật (tùy chọn). Nếu để null thì giữ nguyên ghi chú cũ.
        /// </summary>
        [MaxLength(500, ErrorMessage = "Ghi chú không được vượt quá 500 ký tự.")]
        public string? Note { get; set; }
    }
}
