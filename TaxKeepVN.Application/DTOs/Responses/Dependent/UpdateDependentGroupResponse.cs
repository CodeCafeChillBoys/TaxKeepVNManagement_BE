using System;
using System.Collections.Generic;

namespace TaxKeepVN.Application.DTOs.Responses.Dependent
{
    /// <summary>
    /// Response trả về sau khi chuyển nhóm người phụ thuộc thành công.
    /// Toàn bộ thông tin định danh là read-only — FE không cần người dùng nhập lại.
    /// </summary>
    public class UpdateDependentGroupResponse
    {
        // ── Thông tin định danh (read-only, không thể chỉnh sửa) ─────────────────
        public Guid DependentId { get; set; }
        public Guid TaxpayerId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public DateTime BirthDate { get; set; }
        public string? CitizenId { get; set; }
        public string? BirthCertNumber { get; set; }
        public string? TaxIdNumber { get; set; }

        /// <summary>CHILD | SPOUSE | PARENT | OTHER_DEPENDENT</summary>
        public string Relationship { get; set; } = string.Empty;

        public string EffectiveFromMonth { get; set; } = string.Empty;
        public string EffectiveToMonth { get; set; } = string.Empty;

        public string? Note { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }

        // ── Thông tin chuyển nhóm ────────────────────────────────────────────────

        /// <summary>Nhóm điều kiện trước khi chuyển (để FE hiển thị "Trước: …")</summary>
        public string PreviousGroup { get; set; } = string.Empty;

        /// <summary>Nhóm điều kiện mới sau khi chuyển</summary>
        public string CurrentGroup { get; set; } = string.Empty;

        /// <summary>
        /// Trạng thái hồ sơ sau khi chuyển nhóm — luôn là PENDING_DOCUMENTS
        /// vì cần upload lại giấy tờ theo nhóm mới.
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>Luôn false sau khi chuyển nhóm — hồ sơ cần bổ sung giấy tờ mới.</summary>
        public bool IsProfileComplete { get; set; }

        /// <summary>
        /// Danh sách loại giấy tờ cần upload theo nhóm MỚI.
        /// FE dùng để hiển thị checklist trang bổ sung tài liệu ngay sau khi chuyển nhóm.
        /// </summary>
        public string[] RequiredDocuments { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Toàn bộ documents đã upload từ trước.
        /// FE dùng để đánh dấu cái nào vẫn còn hợp lệ cho nhóm mới,
        /// cái nào cần bổ sung thêm.
        /// </summary>
        public List<DependentDocumentDto> Documents { get; set; } = new();
    }
}
