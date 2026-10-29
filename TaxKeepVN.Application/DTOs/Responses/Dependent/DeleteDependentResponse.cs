using System;

namespace TaxKeepVN.Application.DTOs.Responses.Dependent
{
    /// <summary>
    /// Response trả về sau khi vô hiệu hóa (xóa mềm) người phụ thuộc thành công.
    /// </summary>
    public class DeleteDependentResponse
    {
        /// <summary>ID của người phụ thuộc vừa bị vô hiệu hóa.</summary>
        public Guid DependentId { get; set; }

        /// <summary>Họ và tên đầy đủ của người phụ thuộc.</summary>
        public string FullName { get; set; } = string.Empty;

        /// <summary>Quan hệ với người nộp thuế.</summary>
        public string Relationship { get; set; } = string.Empty;

        /// <summary>Nhóm điều kiện cuối cùng trước khi bị vô hiệu hóa.</summary>
        public string CurrentGroup { get; set; } = string.Empty;

        /// <summary>Trạng thái mới — luôn là INACTIVE sau khi xóa mềm.</summary>
        public string Status { get; set; } = "INACTIVE";

        /// <summary>Cờ xóa mềm — luôn là true sau khi vô hiệu hóa.</summary>
        public bool IsDeleted { get; set; } = true;

        /// <summary>Lý do vô hiệu hóa (nếu có).</summary>
        public string? Reason { get; set; }

        /// <summary>Thời điểm vô hiệu hóa.</summary>
        public DateTimeOffset DeletedAt { get; set; }
    }
}
