using System;
using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Domain.Entities
{
    /// <summary>
    /// Chứng chỉ / Bằng cấp đính kèm trong hồ sơ đăng ký chuyên gia (Mục 5.4, 10, 11)
    /// </summary>
    public class ExpertApplicationCertificate
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid ApplicationId { get; set; }

        /// <summary>Loại chứng chỉ (VD: CPA, TAX_AGENT, UNIVERSITY_DEGREE, LAW_DEGREE, OTHER)</summary>
        public string CertificateType { get; set; } = string.Empty;

        /// <summary>Tên chứng chỉ</summary>
        public string CertificateName { get; set; } = string.Empty;

        /// <summary>Số hiệu chứng chỉ</summary>
        public string CertificateNumber { get; set; } = string.Empty;

        /// <summary>Đơn vị cấp (Bộ Tài chính, Tổng cục Thuế...)</summary>
        public string IssuingAuthority { get; set; } = string.Empty;

        /// <summary>Ngày cấp</summary>
        public DateOnly IssueDate { get; set; }

        /// <summary>
        /// Ngày hết hạn nếu có (BR-06: Bắt buộc đối với chứng chỉ có thời hạn, null nếu vĩnh viễn)
        /// </summary>
        public DateOnly? ExpiryDate { get; set; }

        /// <summary>Đánh dấu chứng chỉ có thời hạn hay không</summary>
        public bool HasExpiry { get; set; } = false;

        /// <summary>Đường dẫn file ảnh/PDF chứng chỉ (Supabase Storage)</summary>
        public string FileUrl { get; set; } = string.Empty;

        /// <summary>Tên file gốc</summary>
        public string? FileName { get; set; }

        /// <summary>MIME type của file</summary>
        public string? FileMimeType { get; set; }

        // ── 11 Thẩm định chứng chỉ ──────────────────────────────────────────
        /// <summary>PendingVerification | Verified | Rejected | Expired</summary>
        public CertificateVerificationStatus VerificationStatus { get; set; } = CertificateVerificationStatus.PendingVerification;

        /// <summary>Nguồn tra cứu/xác minh (Cổng tra cứu chính thức, đối chiếu nội bộ...)</summary>
        public string? VerificationSource { get; set; }

        /// <summary>Ghi chú kết quả thẩm định</summary>
        public string? VerificationNote { get; set; }

        /// <summary>Người thẩm định chứng chỉ (Admin/Reviewer)</summary>
        public Guid? VerifiedBy { get; set; }

        public DateTimeOffset? VerifiedAt { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        // Navigation properties
        public ExpertApplication? Application { get; set; }
        public User? Verifier { get; set; }
    }
}
