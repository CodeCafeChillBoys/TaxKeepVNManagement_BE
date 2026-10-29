using System;
using System.Collections.Generic;

namespace TaxKeepVN.Domain.Entities
{
    /// <summary>
    /// Danh mục Lĩnh vực chuyên môn (Mục 5.3: Thuế TNCN, TNDN, Hoàn thuế, Chuyển giá...)
    /// </summary>
    public class Specialization
    {
        public int Id { get; set; }

        /// <summary>Mã định danh lĩnh vực (VD: PIT, CIT, FINALIZATION, TRANSFER_PRICING...)</summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>Tên lĩnh vực chuyên môn hiển thị</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Mô tả chi tiết</summary>
        public string? Description { get; set; }

        /// <summary>Trạng thái kích hoạt</summary>
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public ICollection<ExpertApplicationSpecialization> ApplicationSpecializations { get; set; } = new List<ExpertApplicationSpecialization>();
        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    }
}
