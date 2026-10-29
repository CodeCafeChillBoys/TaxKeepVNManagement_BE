using System;

namespace TaxKeepVN.Domain.Entities
{
    /// <summary>
    /// Bảng liên kết Hồ sơ đăng ký và Lĩnh vực chuyên môn (Quan hệ N-N)
    /// </summary>
    public class ExpertApplicationSpecialization
    {
        public Guid ApplicationId { get; set; }
        public int SpecializationId { get; set; }

        // Navigation properties
        public ExpertApplication? Application { get; set; }
        public Specialization? Specialization { get; set; }
    }
}
