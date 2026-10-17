using System;
using System.Collections.Generic;

namespace TaxKeepVN.Application.DTOs.Disputes
{
    /// <summary>
    /// Request gửi khiếu nại dịch vụ tư vấn (Đặc tả 7 - Mục 6.B)
    /// </summary>
    public class CreateDisputeRequest
    {
        /// <summary>
        /// Lý do khiếu nại: WRONG_TAX_LAW_ADVICE | UNPROFESSIONAL_BEHAVIOR | EARLY_DISCONNECT | NETWORK_ISSUE | OTHER
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>Nội dung mô tả chi tiết sự việc</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Danh sách link hình ảnh / tài liệu bằng chứng</summary>
        public List<string>? EvidenceUrls { get; set; }
    }

    /// <summary>
    /// Request Chuyên gia gửi phản hồi / bằng chứng phản biện khiếu nại (Đặc tả 7 - Mục 6.B Bước 3)
    /// </summary>
    public class ExpertRespondDisputeRequest
    {
        /// <summary>Nội dung giải trình của chuyên gia</summary>
        public string Response { get; set; } = string.Empty;

        /// <summary>Danh sách link hình ảnh / tài liệu bằng chứng phản biện</summary>
        public List<string>? EvidenceUrls { get; set; }
    }

    /// <summary>
    /// Request Admin đưa ra phán quyết xử lý tranh chấp (Đặc tả 7 - Mục 6.B Bước 5)
    /// </summary>
    public class AdminResolveDisputeRequest
    {
        /// <summary>
        /// Loại phán quyết:
        /// FULL_REFUND: Khiếu nại hợp lệ hoàn toàn -> Hoàn tiền 100% cho User, không giải ngân cho Chuyên gia
        /// SETTLE_EXPERT: Khiếu nại không có căn cứ -> Bác bỏ khiếu nại, giải ngân đầy đủ cho Chuyên gia
        /// PARTIAL: Lỗi một phần / sự cố khách quan -> Phân chia tỷ lệ theo % hoàn tiền
        /// </summary>
        public string DecisionType { get; set; } = "FULL_REFUND";

        /// <summary>
        /// Tỷ lệ hoàn tiền cho User (%) — Bắt buộc nếu DecisionType là PARTIAL (Ví dụ: 50%)
        /// </summary>
        public decimal? RefundRatePercent { get; set; }

        /// <summary>Nội dung phán quyết giải thích quyết định của Admin</summary>
        public string Resolution { get; set; } = string.Empty;

        /// <summary>Ghi chú nội bộ dành cho đội ngũ Admin/CSKH</summary>
        public string? AdminNotes { get; set; }
    }

    /// <summary>
    /// Response chi tiết một vụ việc khiếu nại tranh chấp
    /// </summary>
    public class DisputeDetailResponse
    {
        public Guid Id { get; set; }
        public Guid BookingId { get; set; }
        public string BookingCode { get; set; } = string.Empty;
        public decimal TotalFee { get; set; }

        // Khách hàng
        public Guid UserId { get; set; }
        public string TaxpayerFullName { get; set; } = string.Empty;
        public string? TaxpayerPhone { get; set; }

        // Chuyên gia
        public Guid ExpertUserId { get; set; }
        public string ExpertFullName { get; set; } = string.Empty;

        // Nội dung khiếu nại
        public string Reason { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> EvidenceUrls { get; set; } = new();
        public string Status { get; set; } = string.Empty;

        // Phản hồi chuyên gia
        public string? ExpertResponse { get; set; }
        public List<string> ExpertEvidenceUrls { get; set; } = new();
        public DateTimeOffset? ExpertRespondedAt { get; set; }
        public DateTimeOffset? ExpertResponseDeadline { get; set; }
        public int? ExpertRemainingSeconds { get; set; }

        // Phán quyết Admin
        public string? AdminDecisionType { get; set; }
        public string? AdminResolution { get; set; }
        public decimal? RefundRatePercent { get; set; }
        public decimal UserRefundAmount { get; set; }
        public decimal ExpertSettlementAmount { get; set; }
        public decimal PlatformFeeAmount { get; set; }
        public string? AdminNotes { get; set; }
        public Guid? ResolvedBy { get; set; }
        public DateTimeOffset? ResolvedAt { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    /// <summary>
    /// DTO rút gọn cho danh sách khiếu nại
    /// </summary>
    public class DisputeListItemResponse
    {
        public Guid Id { get; set; }
        public Guid BookingId { get; set; }
        public string BookingCode { get; set; } = string.Empty;
        public string TaxpayerFullName { get; set; } = string.Empty;
        public string ExpertFullName { get; set; } = string.Empty;
        public decimal TotalFee { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? ExpertResponseDeadline { get; set; }
    }

    /// <summary>
    /// Kiểm tra quyền và thời hạn gửi khiếu nại (Eligibility)
    /// </summary>
    public class DisputeEligibilityResponse
    {
        public bool CanDispute { get; set; }
        public bool AlreadyDisputed { get; set; }
        public DateTimeOffset? DisputeDeadline { get; set; }
        public int? RemainingSeconds { get; set; }
        public string? Message { get; set; }
    }
}
