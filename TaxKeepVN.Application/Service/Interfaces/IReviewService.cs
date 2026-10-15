using System;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Reviews;

namespace TaxKeepVN.Application.Service.Interfaces
{
    /// <summary>
    /// Giao diện Service xử lý nghiệp vụ Đánh giá và Phản hồi sau phiên tư vấn.
    /// </summary>
    public interface IReviewService
    {
        // ── CHUYÊN GIA ────────────────────────────────────────────────────────────

        /// <summary>
        /// Chuyên gia đánh dấu phiên tư vấn đã kết thúc.
        /// Chuyển trạng thái Booking từ CONFIRMED → COMPLETED, ghi nhận CompletedAt và tính ReviewDeadline.
        /// Đồng thời gửi thông báo nhắc nhở khách hàng gửi đánh giá.
        /// </summary>
        Task CompleteSessionAsync(Guid expertUserId, Guid bookingId);

        /// <summary>
        /// Chuyên gia gửi phản hồi công khai (Reply) cho một đánh giá của khách hàng (chỉ 1 lần).
        /// </summary>
        Task<ReviewDetailResponse> ReplyToReviewAsync(Guid expertUserId, Guid reviewId, ExpertReplyRequest request);

        /// <summary>
        /// Lấy danh sách đánh giá mà chuyên gia đã nhận (view dành cho chính chuyên gia, bao gồm các review bị flag).
        /// </summary>
        Task<PagedResult<ReviewDetailResponse>> GetMyReceivedReviewsAsync(Guid expertUserId, int page = 1, int size = 10);

        // ── KHÁCH HÀNG / CÔNG KHAI ───────────────────────────────────────────────

        /// <summary>
        /// Lấy danh sách đánh giá công khai của một chuyên gia (chỉ các review isPublished = true).
        /// </summary>
        Task<PagedResult<ReviewListItemResponse>> GetExpertPublicReviewsAsync(Guid expertProfileId, int page = 1, int size = 10);

        /// <summary>
        /// Kiểm tra khách hàng có quyền đánh giá một booking chưa:
        /// - Booking phải COMPLETED
        /// - Chưa đánh giá booking này
        /// - Chưa quá ReviewDeadline
        /// </summary>
        Task<ReviewEligibilityResponse> CheckReviewEligibilityAsync(Guid userId, Guid bookingId);

        /// <summary>
        /// Khách hàng gửi đánh giá sau phiên tư vấn.
        /// Tự động cập nhật Rating và TotalReviews của ExpertProfile.
        /// Tự động flag review tiêu cực nếu rating dưới ngưỡng cấu hình và gửi notification.
        /// </summary>
        Task<ReviewDetailResponse> SubmitReviewAsync(Guid userId, Guid bookingId, CreateReviewRequest request);

        // ── ADMIN ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Admin ẩn/hiện một đánh giá vi phạm tiêu chuẩn cộng đồng (isPublished = false/true).
        /// </summary>
        Task<ReviewDetailResponse> TogglePublishReviewAsync(Guid reviewId, bool isPublished);

        /// <summary>
        /// Lấy danh sách đánh giá đang bị flag cần kiểm duyệt (chỉ dành cho Admin/CSKH).
        /// </summary>
        Task<PagedResult<ReviewDetailResponse>> GetFlaggedReviewsAsync(int page = 1, int size = 20);
    }
}
