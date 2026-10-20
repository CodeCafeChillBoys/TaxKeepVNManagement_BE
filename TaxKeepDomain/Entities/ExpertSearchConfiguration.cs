using System;
using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Domain.Entities
{
    /// <summary>
    /// Bảng cấu hình tiêu chí tìm kiếm và sắp xếp chuyên gia (Do Admin quản trị riêng)
    /// </summary>
    public class ExpertSearchConfiguration
    {
        public int Id { get; set; } = 1;

        /// <summary>Tiêu chí sắp xếp mặc định (Recommended, RatingDesc, FeeAsc, FeeDesc, ExperienceDesc, CompletedSessionsDesc)</summary>
        public ExpertSortBy DefaultSortBy { get; set; } = ExpertSortBy.Recommended;

        /// <summary>Bắt buộc chuyên gia phải có chứng chỉ đã xác thực (Verified) mới được hiển thị</summary>
        public bool RequireVerifiedCertificate { get; set; } = false;

        // ── CẤU HÌNH CÁC TRƯỜNG QUÉT TỪ KHÓA ──────────────────────────────────
        public bool SearchInFullName { get; set; } = true;
        public bool SearchInJobTitle { get; set; } = true;
        public bool SearchInCompanyName { get; set; } = true;
        public bool SearchInBio { get; set; } = true;
        public bool SearchInSpecializations { get; set; } = true;
        public bool SearchInCertificates { get; set; } = true;

        // ── TRỌNG SỐ THUẬT TOÁN XẾP HẠNG (RANKING WEIGHTS) ──────────────────
        public double RatingWeight { get; set; } = 0.35;
        public double CompletedConsultationsWeight { get; set; } = 0.25;
        public double ReviewsCountWeight { get; set; } = 0.15;
        public double SlotAvailabilityWeight { get; set; } = 0.15;
        public double VerifiedCertificateWeight { get; set; } = 0.10;

        /// <summary>Danh sách ID chuyên môn ưu tiên đẩy lên đầu trong mùa vụ (Dạng JSON: [1, 2])</summary>
        public string? FeaturedSpecializationIds { get; set; } = "[]";

        /// <summary>Danh sách các option sắp xếp cho phép hiển thị trên dropdown giao diện (Dạng JSON)</summary>
        public string? AllowedSortOptions { get; set; } = "[\"Recommended\",\"RatingDesc\",\"CompletedSessionsDesc\",\"FeeAsc\",\"FeeDesc\",\"ExperienceDesc\"]";

        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
