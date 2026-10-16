using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TaxKeepVN.Application.DTOs.Reviews
{
    /// <summary>
    /// Điểm đánh giá chi tiết theo từng tiêu chí. Giá trị từ 1 đến 5.
    /// Giải thích tiêu chí để FE hiển thị label phù hợp.
    /// </summary>
    public class ReviewDetailScoresDto
    {
        /// <summary>Độ am hiểu chuyên môn (Expertise)</summary>
        [JsonPropertyName("expertise")]
        public int? Expertise { get; set; }

        /// <summary>Mức độ đúng giờ (Punctuality)</summary>
        [JsonPropertyName("punctuality")]
        public int? Punctuality { get; set; }

        /// <summary>Thái độ giao tiếp (Communication)</summary>
        [JsonPropertyName("communication")]
        public int? Communication { get; set; }

        /// <summary>Khả năng giải quyết bài toán thuế/kế toán (Problem Solving)</summary>
        [JsonPropertyName("problemSolving")]
        public int? ProblemSolving { get; set; }
    }

    /// <summary>
    /// Request body khi khách hàng gửi đánh giá sau phiên tư vấn.
    /// </summary>
    public class CreateReviewRequest
    {
        /// <summary>Số sao tổng quan (1–5)</summary>
        public int Rating { get; set; }

        /// <summary>Nội dung nhận xét (tối thiểu 10 ký tự)</summary>
        public string Comment { get; set; } = string.Empty;

        /// <summary>Điểm chi tiết theo tiêu chí (tùy chọn)</summary>
        public ReviewDetailScoresDto? DetailScores { get; set; }

        /// <summary>Có ẩn danh tên người đánh giá không (mặc định false)</summary>
        public bool IsAnonymous { get; set; } = false;
    }

    /// <summary>
    /// Request body khi chuyên gia phản hồi lại đánh giá (chỉ 1 lần).
    /// </summary>
    public class ExpertReplyRequest
    {
        /// <summary>Nội dung phản hồi của chuyên gia</summary>
        public string Reply { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO phản hồi một đánh giá đầy đủ (dùng cho client/expert/admin).
    /// </summary>
    public class ReviewDetailResponse
    {
        public System.Guid Id { get; set; }
        public System.Guid BookingId { get; set; }
        public System.Guid ExpertProfileId { get; set; }

        // Thông tin người đánh giá
        public System.Guid UserId { get; set; }
        public string ReviewerName { get; set; } = string.Empty;

        // Nội dung đánh giá
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public ReviewDetailScoresDto? DetailScores { get; set; }
        public bool IsAnonymous { get; set; }
        public bool IsPublished { get; set; }
        public bool IsFlaggedForReview { get; set; }

        // Phản hồi của chuyên gia
        public string? ExpertReply { get; set; }
        public System.DateTimeOffset? ExpertRepliedAt { get; set; }

        public System.DateTimeOffset CreatedAt { get; set; }
    }

    /// <summary>
    /// DTO phản hồi gọn (hiển thị trong danh sách công khai trên trang chuyên gia).
    /// </summary>
    public class ReviewListItemResponse
    {
        public System.Guid Id { get; set; }
        public string ReviewerName { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public ReviewDetailScoresDto? DetailScores { get; set; }
        public string? ExpertReply { get; set; }
        public System.DateTimeOffset? ExpertRepliedAt { get; set; }
        public System.DateTimeOffset CreatedAt { get; set; }
    }

    /// <summary>
    /// Kết quả check xem booking có thể đánh giá không (cho FE hiển thị/ẩn nút Đánh giá).
    /// </summary>
    public class ReviewEligibilityResponse
    {
        public bool CanReview { get; set; }
        public bool AlreadyReviewed { get; set; }
        public System.DateTimeOffset? ReviewDeadline { get; set; }
        public int? RemainingSeconds { get; set; }
        public string? Message { get; set; }
    }
}
