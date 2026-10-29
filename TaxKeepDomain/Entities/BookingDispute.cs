using System;

namespace TaxKeepVN.Domain.Entities
{
    /// <summary>
    /// Bản ghi khiếu nại chất lượng dịch vụ / Tranh chấp phiên tư vấn giữa Khách hàng và Chuyên gia (Đặc tả 7 - Mục 6.B)
    /// </summary>
    public class BookingDispute
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>FK → Booking.Id (Unique: Mỗi booking chỉ có 1 vụ việc khiếu nại)</summary>
        public Guid BookingId { get; set; }

        /// <summary>FK → User.UserId (Khách hàng tạo khiếu nại)</summary>
        public Guid UserId { get; set; }

        /// <summary>FK → User.UserId (Chuyên gia phụ trách ca tư vấn)</summary>
        public Guid ExpertUserId { get; set; }

        /// <summary>
        /// Phân loại lý do khiếu nại:
        /// WRONG_TAX_LAW_ADVICE: Tư vấn sai quy định pháp luật rõ ràng
        /// UNPROFESSIONAL_BEHAVIOR: Thái độ thiếu chuẩn mực
        /// EARLY_DISCONNECT: Chuyên gia tự ý kết thúc sớm
        /// NETWORK_ISSUE: Sự cố đường truyền mạng nghiêm trọng
        /// OTHER: Lý do khác
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>Mô tả chi tiết nội dung khiếu nại của khách hàng</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Danh sách URL ảnh / tài liệu bằng chứng khách hàng cung cấp (lưu dưới dạng JSON array)</summary>
        public string? EvidenceUrlsJson { get; set; }

        /// <summary>
        /// Trạng thái giải quyết tranh chấp:
        /// PENDING_EXPERT_RESPONSE: Đang chờ chuyên gia gửi giải trình (trong vòng 24 giờ)
        /// AWAITING_ADMIN_RESOLUTION: Chuyên gia đã phản hồi hoặc hết hạn 24h, đang chờ Admin phán quyết
        /// RESOLVED_FULL_REFUND: Khiếu nại hợp lệ hoàn toàn (lỗi chuyên gia) -> Hoàn tiền 100% cho User
        /// RESOLVED_SETTLE_EXPERT: Khiếu nại không có căn cứ -> Bác bỏ khiếu nại, giải ngân cho Chuyên gia
        /// RESOLVED_PARTIAL: Lỗi một phần / sự cố khách quan -> Phân chia tỷ lệ theo quyết định của Admin
        /// CANCELLED: Khách hàng chủ động rút khiếu nại
        /// </summary>
        public string Status { get; set; } = "PENDING_EXPERT_RESPONSE";

        /// <summary>Nội dung giải trình / phản biện của Chuyên gia</summary>
        public string? ExpertResponse { get; set; }

        /// <summary>Bằng chứng phản biện của Chuyên gia (JSON array)</summary>
        public string? ExpertEvidenceUrlsJson { get; set; }

        /// <summary>Thời điểm Chuyên gia gửi phản biện</summary>
        public DateTimeOffset? ExpertRespondedAt { get; set; }

        /// <summary>Hạn chót để Chuyên gia gửi phản hồi (24 giờ kể từ khi khiếu nại được tạo)</summary>
        public DateTimeOffset? ExpertResponseDeadline { get; set; }

        /// <summary>Loại quyết định của Admin: FULL_REFUND, SETTLE_EXPERT, PARTIAL</summary>
        public string? AdminDecisionType { get; set; }

        /// <summary>Nội dung phán quyết giải thích của Admin</summary>
        public string? AdminResolution { get; set; }

        /// <summary>Tỷ lệ hoàn tiền cho User nếu là quyết định PARTIAL (%)</summary>
        public decimal? RefundRatePercent { get; set; }

        /// <summary>Số tiền thực tế hoàn lại cho Người nộp thuế (VNĐ)</summary>
        public decimal UserRefundAmount { get; set; } = 0m;

        /// <summary>Số tiền thực tế giải ngân cho Chuyên gia (VNĐ)</summary>
        public decimal ExpertSettlementAmount { get; set; } = 0m;

        /// <summary>Số tiền phí hoa hồng Sàn giữ lại (VNĐ)</summary>
        public decimal PlatformFeeAmount { get; set; } = 0m;

        /// <summary>Ghi chú nội bộ của Admin</summary>
        public string? AdminNotes { get; set; }

        /// <summary>Admin trực tiếp đưa ra phán quyết</summary>
        public Guid? ResolvedBy { get; set; }

        public DateTimeOffset? ResolvedAt { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        // Navigation
        public Booking? Booking { get; set; }
        public User? User { get; set; }
        public User? ExpertUser { get; set; }
    }
}
