using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Payments;

namespace TaxKeepVN.Application.Service.Interfaces
{
    /// <summary>
    /// Giao diện Service xử lý Thanh toán cổng VNPay / VietQR & Quản lý tạm giữ tiền bảo đảm Escrow (Đặc tả 4)
    /// </summary>
    public interface IPaymentEscrowService
    {
        /// <summary>
        /// Khởi tạo hoặc lấy lại Link thanh toán VNPay và mã VietQR cho Booking đang ở trạng thái PENDING_PAYMENT
        /// </summary>
        Task<PaymentLinkResponse> CreateOrGetPaymentLinkAsync(Guid userId, Guid bookingId, CreatePaymentLinkRequest? request = null, string? clientIp = null);

        /// <summary>
        /// Tiếp nhận và xử lý Webhook IPN từ cổng thanh toán VNPay (Xác thực chữ ký HMAC-SHA512, ghi nhận HeldInEscrow và chuyển Booking sang AWAITING_EXPERT_APPROVAL)
        /// </summary>
        Task<VnPayIpnResponse> HandleVnPayIpnAsync(IDictionary<string, string> queryParams);

        /// <summary>
        /// Xử lý điều hướng trả về (Return URL) từ cổng thanh toán VNPay sau khi người dùng thực hiện giao dịch
        /// </summary>
        Task<EscrowTransactionResponse?> HandleVnPayReturnAsync(IDictionary<string, string> queryParams);

        /// <summary>
        /// Xem chi tiết giao dịch tạm giữ tiền bảo đảm (EscrowTransaction) của một Booking (Dành cho Khách hàng, Chuyên gia phụ trách hoặc Admin)
        /// </summary>
        Task<EscrowTransactionResponse> GetEscrowByBookingIdAsync(Guid bookingId, Guid currentUserId, string currentUserRole);

        /// <summary>
        /// Truy vấn danh sách giao dịch Escrow có hỗ trợ Filtering, Searching, Sorting, Pagination theo chuẩn API Guide
        /// </summary>
        Task<PagedResult<EscrowTransactionResponse>> GetEscrowTransactionsAsync(Guid currentUserId, string currentUserRole, EscrowTransactionQueryParameters query);

        /// <summary>
        /// Xem tổng quan Ví Nền Tảng & Sổ cái tài chính của Admin (Admin Platform Wallet & Escrow Ledger Summary)
        /// Chỉ dành riêng cho quyền Admin.
        /// </summary>
        Task<AdminPlatformWalletResponse> GetAdminPlatformWalletAsync(Guid currentUserId, string currentUserRole);
    }
}
