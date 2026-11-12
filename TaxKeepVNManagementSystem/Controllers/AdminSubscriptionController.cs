using System;
using System.Collections.Generic;
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
    /// Controller dành riêng cho Quản trị viên (Admin) cấu hình toàn bộ các gói dịch vụ.
    /// ZERO-HARDCODE: Admin có thể điều chỉnh giá, quota, số câu hỏi/tháng, lượt OCR... hoàn toàn linh hoạt.
    /// </summary>
    [ApiController]
    [Route("api/v1/admin/subscription/plans")]
    [Authorize(Roles = "admin")]
    [Produces("application/json")]
    [Tags("Admin - Subscription Plans (Quản lý các gói dịch vụ)")]
    public class AdminSubscriptionController : ControllerBase
    {
        private readonly ISubscriptionPlanService _planService;

        public AdminSubscriptionController(ISubscriptionPlanService planService)
        {
            _planService = planService;
        }

        /// <summary>
        /// Admin lấy danh sách tất cả các gói (bao gồm cả gói ẩn/ngừng kích hoạt)
        /// GET /api/v1/admin/subscription/plans
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<List<SubscriptionPlanResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllPlans()
        {
            var plans = await _planService.GetAllPlansForAdminAsync();
            return Ok(ApiResponse<List<SubscriptionPlanResponse>>.Ok(plans, "Lấy danh sách tất cả các gói dịch vụ thành công."));
        }

        /// <summary>
        /// Admin xem chi tiết 1 gói dịch vụ theo Id
        /// GET /api/v1/admin/subscription/plans/{id}
        /// </summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<SubscriptionPlanResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPlanById(Guid id)
        {
            var plan = await _planService.GetPlanByIdAsync(id);
            return Ok(ApiResponse<SubscriptionPlanResponse>.Ok(plan, "Lấy chi tiết gói thành công."));
        }

        /// <summary>
        /// Admin tạo gói dịch vụ mới
        /// POST /api/v1/admin/subscription/plans
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<SubscriptionPlanResponse>), StatusCodes.Status201Created)]
        public async Task<IActionResult> CreatePlan([FromBody] CreateSubscriptionPlanRequest request)
        {
            var created = await _planService.CreatePlanAsync(request);
            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<SubscriptionPlanResponse>.Ok(created, $"Đã tạo gói dịch vụ '{created.Name}' thành công."));
        }

        /// <summary>
        /// Admin cập nhật cấu hình gói dịch vụ (giá tháng/năm, số câu hỏi AI, OCR, quyền xuất PDF/ZIP, ưu đãi đặt lịch...)
        /// PUT /api/v1/admin/subscription/plans/{id}
        /// </summary>
        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<SubscriptionPlanResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdatePlan(Guid id, [FromBody] UpdateSubscriptionPlanRequest request)
        {
            var updated = await _planService.UpdatePlanAsync(id, request);
            return Ok(ApiResponse<SubscriptionPlanResponse>.Ok(updated, $"Đã cập nhật cấu hình gói '{updated.Name}' thành công."));
        }

        /// <summary>
        /// Admin ngừng kích hoạt (Soft Delete) gói dịch vụ
        /// DELETE /api/v1/admin/subscription/plans/{id}
        /// </summary>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        public async Task<IActionResult> DeletePlan(Guid id)
        {
            var result = await _planService.DeletePlanAsync(id);
            return Ok(ApiResponse<bool>.Ok(result, "Đã ngừng kích hoạt gói dịch vụ thành công."));
        }
    }
}

