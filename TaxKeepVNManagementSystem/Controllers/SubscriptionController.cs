using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.DTOs.Subscriptions;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVNManagementSystem.Controllers
{
    [ApiController]
    [Produces("application/json")]
    public class SubscriptionController : ControllerBase
    {
        private readonly ISubscriptionPlanService _planService;
        private readonly IUserSubscriptionService _userSubService;
        private readonly ISubscriptionQuotaService _quotaService;

        public SubscriptionController(
            ISubscriptionPlanService planService,
            IUserSubscriptionService userSubService,
            ISubscriptionQuotaService quotaService)
        {
            _planService = planService;
            _userSubService = userSubService;
            _quotaService = quotaService;
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst("userId")?.Value 
                     ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }

        /// <summary>
        /// Lấy danh sách các gói dịch vụ đang kích hoạt (Dành cho người dùng xem và chọn mua)
        /// GET /api/v1/subscription/plans
        /// </summary>
        [HttpGet("api/v1/subscription/plans")]
        [ProducesResponseType(typeof(ApiResponse<List<SubscriptionPlanResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPlans()
        {
            var plans = await _planService.GetAllActivePlansAsync();
            return Ok(ApiResponse<List<SubscriptionPlanResponse>>.Ok(plans, "Lấy danh sách gói dịch vụ thành công."));
        }

        /// <summary>
        /// Lấy thông tin gói dịch vụ hiện tại đang kích hoạt của người dùng
        /// GET /api/v1/subscription/my
        /// </summary>
        [Authorize]
        [HttpGet("api/v1/subscription/my")]
        [ProducesResponseType(typeof(ApiResponse<UserSubscriptionResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMySubscription()
        {
            var userId = GetCurrentUserId();
            var sub = await _userSubService.GetActiveSubscriptionAsync(userId);
            return Ok(ApiResponse<UserSubscriptionResponse>.Ok(sub, "Lấy thông tin gói đang sử dụng thành công."));
        }

        /// <summary>
        /// Xem hạn mức sử dụng và số lượt còn lại trong tháng (AI questions, OCR, nguồn thu nhập, người phụ thuộc...)
        /// GET /api/v1/subscription/quota
        /// </summary>
        [Authorize]
        [HttpGet("api/v1/subscription/quota")]
        [ProducesResponseType(typeof(ApiResponse<SubscriptionQuotaStatusResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetQuotaStatus()
        {
            var userId = GetCurrentUserId();
            var quota = await _quotaService.GetQuotaStatusAsync(userId);
            return Ok(ApiResponse<SubscriptionQuotaStatusResponse>.Ok(quota, "Lấy hạn mức sử dụng gói thành công."));
        }

        /// <summary>
        /// Mua gói hoặc nâng cấp gói dịch vụ (Thanh toán bằng số dư Ví, VNPay, hoặc VietQR)
        /// POST /api/v1/subscription/purchase
        /// </summary>
        [Authorize]
        [HttpPost("api/v1/subscription/purchase")]
        [ProducesResponseType(typeof(ApiResponse<SubscriptionPurchaseResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Purchase([FromBody] PurchaseSubscriptionRequest request)
        {
            var userId = GetCurrentUserId();
            var result = await _userSubService.PurchaseAsync(userId, request);
            return Ok(ApiResponse<SubscriptionPurchaseResponse>.Ok(result, result.Message));
        }

        /// <summary>
        /// Lịch sử đăng ký và thanh toán các gói của người dùng
        /// GET /api/v1/subscription/history
        /// </summary>
        [Authorize]
        [HttpGet("api/v1/subscription/history")]
        [ProducesResponseType(typeof(ApiResponse<List<UserSubscriptionResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetHistory()
        {
            var userId = GetCurrentUserId();
            var history = await _userSubService.GetSubscriptionHistoryAsync(userId);
            return Ok(ApiResponse<List<UserSubscriptionResponse>>.Ok(history, "Lấy lịch sử gói thành công."));
        }

        /// <summary>
        /// Webhook / IPN xử lý kết quả thanh toán từ VNPay
        /// GET /api/v1/subscription/payment-return
        /// </summary>
        [HttpGet("api/v1/subscription/payment-return")]
        public async Task<IActionResult> PaymentReturn([FromQuery] long? orderCode, [FromQuery] string? vnp_ResponseCode, [FromQuery] string? vnp_TxnRef)
        {
            long code = orderCode ?? 0;
            if (code == 0 && !string.IsNullOrEmpty(vnp_TxnRef))
            {
                long.TryParse(vnp_TxnRef, out code);
            }

            bool isSuccess = vnp_ResponseCode == "00";
            if (code > 0)
            {
                await _userSubService.HandlePaymentCallbackAsync(code, isSuccess);
            }

            if (isSuccess)
            {
                return Ok(new { success = true, message = "Thanh toán gói dịch vụ thành công!" });
            }
            return BadRequest(new { success = false, message = "Thanh toán không thành công hoặc đã bị hủy." });
        }
    }
}

