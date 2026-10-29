using System;
using System.Collections.Generic;

namespace TaxKeepVN.Domain.Entities
{
    public class TaxPeriod
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid UserId { get; set; }

        /// <summary>Năm thuế (Ví dụ: 2026)</summary>
        public short TaxYear { get; set; }

        /// <summary>DRAFT, SUBMITTED</summary>
        public string Status { get; set; } = "DRAFT";

        /// <summary>Thời điểm tạo kỳ kê khai</summary>
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>Thời điểm cập nhật kỳ kê khai gần nhất</summary>
        public DateTimeOffset? UpdatedAt { get; set; }

        // Navigation properties
        public User? User { get; set; }
        public ICollection<Document> Documents { get; set; } = new List<Document>();
    }
}