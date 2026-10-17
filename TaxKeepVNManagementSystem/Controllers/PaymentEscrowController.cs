using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Payments;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVNManagementSystem.Controllers
{
    /// <summary>
    /// Controller xử lý Thanh toán VNPay & VietQR và Quản lý tạm giữ tiền bảo đảm (Escrow) (Đặc tả 4)
    /// Tuân thủ API Design Guidelines: RESTful naming, Filtering, Searching, Sorting, Pagination, EndpointSummary.
    /// </summary>
    [ApiController]
    [Tags("Payment & Escrow - Thanh Toán Ký Quỹ & Xác Nhận Lịch Hẹn")]
    [Produces("application/json")]
    public class PaymentEscrowController : ControllerBase
    {
        private readonly IPaymentEscrowService _paymentEscrowService;

        public PaymentEscrowController(IPaymentEscrowService paymentEscrowService)
        {
            _paymentEscrowService = paymentEscrowService;
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst("userId")?.Value 
                     ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }

        private string GetCurrentUserRole()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value 
                ?? User.FindFirst("role")?.Value 
                ?? "taxpayer";
        }

        /// <summary>
        /// Khởi tạo hoặc lấy lại Link thanh toán VNPay và mã VietQR cho Booking đang ở trạng thái PENDING_PAYMENT (Đặc tả 4)
        /// Hỗ trợ cả 2 endpoint:
        /// - POST /api/v1/bookings/{bookingId}/payment-link
        /// - POST /api/v1/payments/create-link (bookingId truyền trong body hoặc URL)
        /// Phương thức thanh toán (PaymentMethod):
        /// - GATEWAY: Cổng VNPay / VietQR
        /// - WALLET: Trừ trực tiếp số dư khả dụng ví User
        /// </summary>
        [HttpPost("api/v1/bookings/{bookingId:guid}/payment-link", Name = "CreateOrGetBookingPaymentLink")]
        [HttpPost("api/v1/payments/create-link", Name = "CreateOrGetPaymentLinkAlternative")]
        [Authorize]
        [EndpointSummary("Khởi tạo hoặc lấy lại Link thanh toán VNPay và mã VietQR cho lịch hẹn tư vấn")]
        [ProducesResponseType(typeof(ApiResponse<PaymentLinkResponse>), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CreateOrGetPaymentLink(
            [FromRoute] Guid? bookingId = null,
            [FromBody] CreatePaymentLinkRequest? request = null)
        {
            var targetBookingId = bookingId ?? request?.BookingId;
            if (!targetBookingId.HasValue || targetBookingId.Value == Guid.Empty)
            {
                return BadRequest(ApiResponse<object>.Fail("MISSING_BOOKING_ID", "Vui lòng cung cấp mã lịch hẹn tư vấn (bookingId)."));
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));
            }

            var userId = GetCurrentUserId();
            var rawIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var clientIp = (string.IsNullOrWhiteSpace(rawIp) || rawIp == "::1" || rawIp.Contains(':')) ? "127.0.0.1" : rawIp.Trim();
            var result = await _paymentEscrowService.CreateOrGetPaymentLinkAsync(userId, targetBookingId.Value, request, clientIp);
            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<PaymentLinkResponse>.Ok(result, "Khởi tạo thanh toán thành công."));
        }

        /// <summary>
        /// Webhook IPN tiếp nhận kết quả thanh toán từ cổng VNPay (Đặc tả 4)
        /// Tự động xác thực chữ ký HMAC-SHA512, ghi nhận khoản tiền vào HeldInEscrow và chuyển Booking sang AWAITING_EXPERT_APPROVAL.
        /// GET /api/v1/payments/vnpay-ipn
        /// </summary>
        [HttpGet("api/v1/payments/vnpay-ipn", Name = "HandleVnPayIpn")]
        [AllowAnonymous]
        [EndpointSummary("Webhook IPN tiếp nhận kết quả thanh toán từ cổng VNPay (Xác thực HMAC-SHA512)")]
        [ProducesResponseType(typeof(VnPayIpnResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> HandleVnPayIpn()
        {
            var queryParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in Request.Query.Keys)
            {
                if (!string.IsNullOrEmpty(key))
                {
                    queryParams[key] = Request.Query[key].ToString();
                }
            }

            var result = await _paymentEscrowService.HandleVnPayIpnAsync(queryParams);
            return Ok(result);
        }

        /// <summary>
        /// Điều hướng trả về (Return URL) từ VNPay sau khi khách hàng thực hiện giao dịch (Đặc tả 4)
        /// GET /api/v1/payments/vnpay-return
        /// </summary>
        [HttpGet("api/v1/payments/vnpay-return", Name = "HandleVnPayReturn")]
        [AllowAnonymous]
        [EndpointSummary("Điều hướng trả về (Return URL) từ cổng thanh toán VNPay")]
        [ProducesResponseType(typeof(ApiResponse<EscrowTransactionResponse?>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> HandleVnPayReturn()
        {
            var queryParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in Request.Query.Keys)
            {
                if (!string.IsNullOrEmpty(key))
                {
                    queryParams[key] = Request.Query[key].ToString();
                }
            }

            var result = await _paymentEscrowService.HandleVnPayReturnAsync(queryParams);
            return Ok(ApiResponse<EscrowTransactionResponse?>.Ok(result, "Xử lý kết quả trả về từ cổng thanh toán VNPay thành công."));
        }

        /// <summary>
        /// Xem chi tiết giao dịch tạm giữ tiền bảo đảm (Escrow) của một Booking (Đặc tả 4)
        /// Chỉ khách hàng đặt lịch, chuyên gia phụ trách hoặc Admin mới có quyền xem.
        /// GET /api/v1/bookings/{bookingId}/escrow
        /// </summary>
        [HttpGet("api/v1/bookings/{bookingId:guid}/escrow", Name = "GetEscrowByBookingId")]
        [Authorize]
        [EndpointSummary("Xem chi tiết giao dịch tạm giữ tiền bảo đảm (Escrow) theo Booking ID")]
        [ProducesResponseType(typeof(ApiResponse<EscrowTransactionResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetEscrowByBookingId([FromRoute] Guid bookingId)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();
            var result = await _paymentEscrowService.GetEscrowByBookingIdAsync(bookingId, userId, role);
            return Ok(ApiResponse<EscrowTransactionResponse>.Ok(result, "Lấy thông tin giao dịch tạm giữ tiền (Escrow) thành công."));
        }

        /// <summary>
        /// Lấy danh sách giao dịch tạm giữ tiền bảo đảm (Escrow) có hỗ trợ Filtering, Searching, Sorting, Pagination theo chuẩn API Guide
        /// GET /api/v1/escrow-transactions?page=1&amp;size=10&amp;status=HeldInEscrow&amp;search=BK-&amp;sort=-createdAt
        /// </summary>
        [HttpGet("api/v1/escrow-transactions", Name = "GetEscrowTransactions")]
        [Authorize]
        [EndpointSummary("Lấy danh sách giao dịch ký quỹ Escrow (Hỗ trợ Lọc, Tìm kiếm, Sắp xếp, Phân trang)")]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<EscrowTransactionResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetEscrowTransactions([FromQuery] EscrowTransactionQueryParameters query)
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();
            var result = await _paymentEscrowService.GetEscrowTransactionsAsync(userId, role, query);
            return Ok(ApiResponse<PagedResult<EscrowTransactionResponse>>.Ok(result, "Lấy danh sách giao dịch tạm giữ tiền (Escrow) thành công."));
        }

        /// <summary>
        /// Xem tổng quan Ví Nền Tảng & Sổ cái tài chính của Admin (Admin Platform Wallet & Escrow Ledger Summary) (Đặc tả 4 & 7)
        /// Trả về tổng tiền tạm giữ trong Escrow, tổng hoa hồng nền tảng thực nhận, tiền chờ giải ngân chuyên gia, thống kê trạng thái và 10 giao dịch mới nhất.
        /// GET /api/v1/admin/platform-wallet
        /// </summary>
        [HttpGet("api/v1/admin/platform-wallet", Name = "GetAdminPlatformWallet")]
        [Authorize]
        [EndpointSummary("Xem tổng quan Ví Nền Tảng & Báo cáo tài chính Admin (Platform Wallet & Escrow Summary)")]
        [ProducesResponseType(typeof(ApiResponse<AdminPlatformWalletResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAdminPlatformWallet()
        {
            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();
            var result = await _paymentEscrowService.GetAdminPlatformWalletAsync(userId, role);
            return Ok(ApiResponse<AdminPlatformWalletResponse>.Ok(result, "Lấy thông tin Ví Nền Tảng (Admin Platform Wallet) thành công."));
        }
    }
}
