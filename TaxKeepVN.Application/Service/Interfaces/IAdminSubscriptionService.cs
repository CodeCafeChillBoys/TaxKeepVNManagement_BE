using System;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Subscriptions;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface IAdminSubscriptionService
    {
        /// <summary>
        /// Xem danh sách ai đang dùng gói nào, kèm bộ lọc phân trang, tìm kiếm, gói, trạng thái, sắp hết hạn.
        /// </summary>
        Task<PagedResult<AdminUserSubscriptionResponse>> GetUserSubscriptionsAsync(AdminSubscriptionFilterRequest filter);

        /// <summary>
        /// Xem chi tiết 1 lượt đăng ký gói của người dùng kèm tình hình sử dụng quota AI, OCR trong tháng.
        /// </summary>
        Task<AdminUserSubscriptionResponse> GetSubscriptionByIdAsync(Guid subscriptionId);

        /// <summary>
        /// Thống kê doanh thu theo gói, theo chu kỳ, phân bổ người dùng và xu hướng theo tháng.
        /// </summary>
        Task<SubscriptionAnalyticsResponse> GetAnalyticsAsync(DateTimeOffset? fromDate, DateTimeOffset? toDate);

        /// <summary>
        /// Tặng / gán gói cho một người dùng cụ thể.
        /// </summary>
        Task<AdminUserSubscriptionResponse> GiftSubscriptionAsync(GiftSubscriptionRequest request, Guid adminId);

        /// <summary>
        /// Gia hạn thêm ngày sử dụng cho gói hiện tại của người dùng.
        /// </summary>
        Task<AdminUserSubscriptionResponse> ExtendSubscriptionAsync(Guid subscriptionId, ExtendSubscriptionRequest request, Guid adminId);

        /// <summary>
        /// Hoàn tiền gói đăng ký vào Ví cá nhân của người dùng và chuyển về gói Basic mặc định.
        /// </summary>
        Task<bool> RefundSubscriptionAsync(Guid subscriptionId, RefundSubscriptionRequest request, Guid adminId);
    }
}

