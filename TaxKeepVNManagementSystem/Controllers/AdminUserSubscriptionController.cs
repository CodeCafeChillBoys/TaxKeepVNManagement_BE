using System;
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
    /// <summary>
    /// Controller dành riêng cho Quản trị viên (Admin) quản lý vận hành gói dịch vụ người dùng:
    /// tra cứu danh sách người dùng, báo cáo doanh thu, tặng gói, gia hạn và hoàn tiền.
    /// </summary>
    [ApiController]
    [Route("api/v1/admin/subscriptions")]
    [Authorize(Roles = "admin")]
    [Produces("application/json")]
    [Tags("Admin - User Subscriptions (Vận hành & Doanh thu gói dịch vụ)")]
    public class AdminUserSubscriptionController : ControllerBase
    {
        private readonly IAdminSubscriptionService _adminSubService;

        public AdminUserSubscriptionController(IAdminSubscriptionService adminSubService)
        {
            _adminSubService = adminSubService;
        }

        private Guid GetAdminId()
        {
            var claim = User.FindFirst("userId")?.Value 
                     ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }

        /// <summary>
        /// Xem danh sách người dùng đang sử dụng gói dịch vụ (kèm phân trang và bộ lọc tìm kiếm)
        /// GET /api/v1/admin/subscriptions
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<AdminUserSubscriptionResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUserSubscriptions([FromQuery] AdminSubscriptionFilterRequest filter)
        {
            var result = await _adminSubService.GetUserSubscriptionsAsync(filter);
            return Ok(ApiResponse<PagedResult<AdminUserSubscriptionResponse>>.Ok(result, "Lấy danh sách gói người dùng thành công."));
        }

        /// <summary>
        /// Báo cáo & Thống kê doanh thu theo gói, theo chu kỳ và phân bổ người dùng
        /// GET /api/v1/admin/subscriptions/analytics/revenue
        /// </summary>
        [HttpGet("analytics/revenue")]
        [ProducesResponseType(typeof(ApiResponse<SubscriptionAnalyticsResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAnalytics([FromQuery] DateTimeOffset? fromDate, [FromQuery] DateTimeOffset? toDate)
        {
            var analytics = await _adminSubService.GetAnalyticsAsync(fromDate, toDate);
            return Ok(ApiResponse<SubscriptionAnalyticsResponse>.Ok(analytics, "Lấy dữ liệu thống kê doanh thu gói thành công."));
        }

        /// <summary>
        /// Xem thông tin chi tiết một lượt đăng ký gói của người dùng (kèm hạn mức quota AI, OCR đã dùng trong tháng)
        /// GET /api/v1/admin/subscriptions/{id}
        /// </summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<AdminUserSubscriptionResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetSubscriptionById(Guid id)
        {
            var result = await _adminSubService.GetSubscriptionByIdAsync(id);
            return Ok(ApiResponse<AdminUserSubscriptionResponse>.Ok(result, "Lấy chi tiết gói đăng ký thành công."));
        }

        /// <summary>
        /// Tặng / gán gói cho một người dùng cụ thể (Áp dụng cho khách VIP, đối tác, sự kiện)
        /// POST /api/v1/admin/subscriptions/gift
        /// </summary>
        [HttpPost("gift")]
        [ProducesResponseType(typeof(ApiResponse<AdminUserSubscriptionResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GiftSubscription([FromBody] GiftSubscriptionRequest request)
        {
            var adminId = GetAdminId();
            var result = await _adminSubService.GiftSubscriptionAsync(request, adminId);
            return Ok(ApiResponse<AdminUserSubscriptionResponse>.Ok(result, $"Tặng gói '{result.PlanName}' cho người dùng thành công."));
        }

        /// <summary>
        /// Gia hạn thêm ngày sử dụng cho gói trả phí hiện tại của người dùng (Áp dụng đền bù sự cố hoặc CSKH)
        /// POST /api/v1/admin/subscriptions/{id}/extend
        /// </summary>
        [HttpPost("{id:guid}/extend")]
        [ProducesResponseType(typeof(ApiResponse<AdminUserSubscriptionResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> ExtendSubscription(Guid id, [FromBody] ExtendSubscriptionRequest request)
        {
            var adminId = GetAdminId();
            var result = await _adminSubService.ExtendSubscriptionAsync(id, request, adminId);
            return Ok(ApiResponse<AdminUserSubscriptionResponse>.Ok(result, $"Gia hạn thêm {request.ExtensionDays} ngày thành công. Hạn sử dụng mới: {result.ExpiresAt:dd/MM/yyyy}."));
        }

        /// <summary>
        /// Hoàn tiền gói đăng ký vào Ví cá nhân của người dùng và chuyển tài khoản về gói Basic mặc định
        /// POST /api/v1/admin/subscriptions/{id}/refund
        /// </summary>
        [HttpPost("{id:guid}/refund")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        public async Task<IActionResult> RefundSubscription(Guid id, [FromBody] RefundSubscriptionRequest request)
        {
            var adminId = GetAdminId();
            var result = await _adminSubService.RefundSubscriptionAsync(id, request, adminId);
            return Ok(ApiResponse<bool>.Ok(result, "Hoàn tiền gói vào ví người dùng thành công. Tài khoản đã được chuyển về gói Basic."));
        }
    }
}

