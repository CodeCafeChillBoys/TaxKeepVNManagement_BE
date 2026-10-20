using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.DTOs.Wallets;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVNManagementSystem.Controllers
{
    /// <summary>
    /// Controller Quản lý Ví điện tử, Lịch sử biến động số dư và Rút tiền (Đặc tả 7)
    /// </summary>
    [ApiController]
    [Authorize]
    [Produces("application/json")]
    public class WalletController : ControllerBase
    {
        private readonly IDisbursementDisputeService _service;
        private readonly IPaymentEscrowService _paymentEscrowService;

        public WalletController(
            IDisbursementDisputeService service,
            IPaymentEscrowService paymentEscrowService)
        {
            _service = service;
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

        // ════════════════════════════════════════════════════════════════════════
        //  1. THÔNG TIN VÍ & SỐ DƯ CÁ NHÂN
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Xem thông tin số dư ví điện tử cá nhân và 10 giao dịch gần nhất (Đặc tả 7 - Mục 5.2)
        /// Chuyên gia: Xem Ví thu nhập (E-wallet balance)
        /// Khách hàng: Xem Ví hoàn tiền
        /// GET /api/v1/wallets/me?walletType=EXPERT
        /// </summary>
        [HttpGet("api/v1/wallets/me")]
        [ProducesResponseType(typeof(ApiResponse<WalletResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyWallet([FromQuery] string? walletType = null)
        {
            var userId = GetCurrentUserId();
            var result = await _service.GetMyWalletAsync(userId, walletType);
            return Ok(ApiResponse<WalletResponse>.Ok(result, "Lấy thông tin ví điện tử thành công."));
        }

        /// <summary>
        /// Xem toàn bộ lịch sử biến động số dư ví (phân trang)
        /// GET /api/v1/wallets/me/transactions?page=1&size=10
        /// </summary>
        [HttpGet("api/v1/wallets/me/transactions")]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<WalletTransactionResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyWalletTransactions(
            [FromQuery] int page = 1,
            [FromQuery] int size = 10)
        {
            var userId = GetCurrentUserId();
            var result = await _service.GetMyWalletTransactionsAsync(userId, page, size);
            return Ok(ApiResponse<PagedResult<WalletTransactionResponse>>.Ok(result, "Lấy lịch sử biến động số dư thành công."));
        }

        /// <summary>
        /// Nạp tiền vào ví cá nhân thông qua cổng thanh toán VNPay / VietQR
        /// POST /api/v1/wallets/top-up
        /// </summary>
        [HttpPost("api/v1/wallets/top-up")]
        [ProducesResponseType(typeof(ApiResponse<WalletTopUpResponse>), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> TopUpWallet([FromBody] WalletTopUpRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));
            }

            var userId = GetCurrentUserId();
            var rawIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var clientIp = (string.IsNullOrWhiteSpace(rawIp) || rawIp == "::1" || rawIp.Contains(':')) ? "127.0.0.1" : rawIp.Trim();
            var result = await _paymentEscrowService.CreateWalletTopUpPaymentUrlAsync(userId, request, clientIp);
            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<WalletTopUpResponse>.Ok(result, "Khởi tạo link nạp tiền vào ví thành công."));
        }

        // ════════════════════════════════════════════════════════════════════════
        //  2. CHUYÊN GIA: LỆNH RÚT TIỀN VỀ NGÂN HÀNG (MỤC 5.3)
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Chuyên gia tạo lệnh rút tiền từ Ví thu nhập về tài khoản ngân hàng cá nhân (Đặc tả 7 - Mục 5.3)
        /// Điều kiện kiểm tra:
        /// 1. Tài khoản đã hoàn tất định danh công dân eKYC (IsVerified = true)
        /// 2. Đã cung cấp Mã số thuế cá nhân hợp lệ (TaxIdNumber)
        /// 3. Số tiền rút >= EXPERT_MIN_WITHDRAWAL_VND (mặc định 500.000 VNĐ, cấu hình động trong system_configs)
        /// POST /api/v1/expert/wallets/withdraw
        /// </summary>
        [HttpPost("api/v1/expert/wallets/withdraw")]
        [ProducesResponseType(typeof(ApiResponse<WithdrawalResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> RequestWithdrawal([FromBody] CreateWithdrawalRequest request)
        {
            var expertUserId = GetCurrentUserId();
            var result = await _service.RequestWithdrawalAsync(expertUserId, request);
            return Ok(ApiResponse<WithdrawalResponse>.Ok(result, "Tạo yêu cầu rút tiền thành công. Tiền đã được tạm giữ để xử lý chuyển khoản."));
        }

        /// <summary>
        /// Chuyên gia xem danh sách các lệnh rút tiền của mình
        /// GET /api/v1/expert/wallets/withdrawals?status=PENDING&page=1&size=10
        /// </summary>
        [HttpGet("api/v1/expert/wallets/withdrawals")]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<WithdrawalResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyWithdrawals(
            [FromQuery] string? status = null,
            [FromQuery] int page = 1,
            [FromQuery] int size = 10)
        {
            var expertUserId = GetCurrentUserId();
            var result = await _service.GetWithdrawalsAsync(expertUserId, "expert", status, page, size);
            return Ok(ApiResponse<PagedResult<WithdrawalResponse>>.Ok(result, "Lấy danh sách lệnh rút tiền thành công."));
        }

        // ════════════════════════════════════════════════════════════════════════
        //  2.1 KHÁCH HÀNG: LỆNH RÚT TIỀN HOÀN VỀ NGÂN HÀNG (REFUND WITHDRAWAL)
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Khách hàng tạo lệnh rút tiền hoàn (Refund) từ ví cá nhân về tài khoản ngân hàng
        /// POST /api/v1/user/wallets/withdraw
        /// </summary>
        [HttpPost("api/v1/user/wallets/withdraw")]
        [ProducesResponseType(typeof(ApiResponse<WithdrawalResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RequestUserRefundWithdrawal([FromBody] CreateWithdrawalRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));
            }

            var userId = GetCurrentUserId();
            var result = await _service.RequestUserRefundWithdrawalAsync(userId, request);
            return Ok(ApiResponse<WithdrawalResponse>.Ok(result, "Tạo yêu cầu rút tiền hoàn thành công. Tiền đã được tạm giữ để Admin chuyển khoản trả lại STK của bạn."));
        }

        /// <summary>
        /// Khách hàng xem danh sách các yêu cầu rút tiền hoàn của mình
        /// GET /api/v1/user/wallets/withdrawals?status=PENDING&page=1&size=10
        /// </summary>
        [HttpGet("api/v1/user/wallets/withdrawals")]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<WithdrawalResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUserWithdrawals(
            [FromQuery] string? status = null,
            [FromQuery] int page = 1,
            [FromQuery] int size = 10)
        {
            var userId = GetCurrentUserId();
            var result = await _service.GetWithdrawalsAsync(userId, "taxpayer", status, page, size);
            return Ok(ApiResponse<PagedResult<WithdrawalResponse>>.Ok(result, "Lấy danh sách yêu cầu rút tiền hoàn thành công."));
        }

        // ════════════════════════════════════════════════════════════════════════
        //  3. ADMIN: XÉT DUYỆT & QUẢN LÝ LỆNH RÚT TIỀN
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Quản trị viên xem toàn bộ danh sách lệnh rút tiền trên hệ thống
        /// GET /api/v1/admin/withdrawals?status=PENDING&page=1&size=10
        /// </summary>
        [HttpGet("api/v1/admin/withdrawals")]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<WithdrawalResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAdminWithdrawals(
            [FromQuery] string? status = null,
            [FromQuery] int page = 1,
            [FromQuery] int size = 10)
        {
            var role = GetCurrentUserRole();
            if (!string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase))
            {
                throw new ForbiddenException("FORBIDDEN", "Chỉ quản trị viên mới có quyền xem toàn bộ lệnh rút tiền.");
            }

            var adminId = GetCurrentUserId();
            var result = await _service.GetWithdrawalsAsync(adminId, role, status, page, size);
            return Ok(ApiResponse<PagedResult<WithdrawalResponse>>.Ok(result, "Lấy danh sách lệnh rút tiền thành công."));
        }

        /// <summary>
        /// Quản trị viên xét duyệt hoặc từ chối lệnh rút tiền của chuyên gia
        /// APPROVE: Ghi nhận đã chuyển khoản thành công, khấu trừ chính thức số dư đóng băng
        /// REJECT: Bác bỏ yêu cầu (kèm lý do) và hoàn trả ngay số dư về ví khả dụng của chuyên gia
        /// POST /api/v1/admin/withdrawals/{id}/process
        /// </summary>
        [HttpPost("api/v1/admin/withdrawals/{id:guid}/process")]
        [ProducesResponseType(typeof(ApiResponse<WithdrawalResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AdminProcessWithdrawal(
            [FromRoute] Guid id,
            [FromBody] AdminProcessWithdrawalRequest request)
        {
            var role = GetCurrentUserRole();
            if (!string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase))
            {
                throw new ForbiddenException("FORBIDDEN", "Chỉ quản trị viên mới có quyền xử lý lệnh rút tiền.");
            }

            var adminId = GetCurrentUserId();
            var result = await _service.AdminProcessWithdrawalAsync(adminId, id, request);
            var actionMsg = request.Action.Equals("APPROVE", StringComparison.OrdinalIgnoreCase) ? "duyệt và chuyển tiền" : "từ chối";
            return Ok(ApiResponse<WithdrawalResponse>.Ok(result, $"Đã {actionMsg} lệnh rút tiền thành công."));
        }
    }
}
