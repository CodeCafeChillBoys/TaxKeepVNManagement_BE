using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaxKeepVN.Application.DTOs.Experts;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVNManagementSystem.Controllers
{
    /// <summary>
    /// Controller Quản lý lịch rảnh làm việc dành cho Chuyên gia (CRUD Expert Slots)
    /// </summary>
    [ApiController]
    [Route("api/v1/expert/slots")]
    [Authorize]
    public class ExpertSlotController : ControllerBase
    {
        private readonly IExpertSlotService _expertSlotService;

        public ExpertSlotController(IExpertSlotService expertSlotService)
        {
            _expertSlotService = expertSlotService;
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst("userId")?.Value 
                     ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }

        /// <summary>
        /// Lấy danh sách toàn bộ khung giờ làm việc của chuyên gia hiện tại
        /// Hỗ trợ lọc theo khoảng ngày (fromDate, toDate), hình thức tư vấn (sessionType), trạng thái đã đặt (isBooked), trạng thái hoạt động (isActive).
        /// GET /api/v1/expert/slots?fromDate=2026-10-15&amp;toDate=2026-10-30&amp;isBooked=false
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<List<ExpertSlotDetailResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetMySlots([FromQuery] ExpertSlotFilterParameters filters)
        {
            var expertUserId = GetCurrentUserId();
            var result = await _expertSlotService.GetMySlotsAsync(expertUserId, filters);
            return Ok(ApiResponse<List<ExpertSlotDetailResponse>>.Ok(result, "Lấy danh sách khung giờ làm việc thành công."));
        }

        /// <summary>
        /// Xem thông tin chi tiết 1 khung giờ theo ID
        /// GET /api/v1/expert/slots/{id}
        /// </summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<ExpertSlotDetailResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetSlotById([FromRoute] Guid id)
        {
            var expertUserId = GetCurrentUserId();
            var result = await _expertSlotService.GetSlotByIdAsync(expertUserId, id);
            return Ok(ApiResponse<ExpertSlotDetailResponse>.Ok(result, "Lấy thông tin chi tiết khung giờ thành công."));
        }

        /// <summary>
        /// Tạo mới 1 khung giờ làm việc
        /// Kiểm tra chống trùng giờ với các slot hiện có và đảm bảo thời gian ở tương lai.
        /// POST /api/v1/expert/slots
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<ExpertSlotDetailResponse>), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateSlot([FromBody] CreateExpertSlotRequest request)
        {
            var expertUserId = GetCurrentUserId();
            var result = await _expertSlotService.CreateSlotAsync(expertUserId, request);
            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<ExpertSlotDetailResponse>.Ok(result, "Tạo khung giờ làm việc mới thành công."));
        }


        /// <summary>
        /// Cập nhật thông tin khung giờ làm việc
        /// Chỉ cho phép cập nhật khi khung giờ chưa có người đặt (IsBooked = false) và không bị tạm giữ thanh toán.
        /// PUT /api/v1/expert/slots/{id}
        /// </summary>
        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<ExpertSlotDetailResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdateSlot([FromRoute] Guid id, [FromBody] UpdateExpertSlotRequest request)
        {
            var expertUserId = GetCurrentUserId();
            var result = await _expertSlotService.UpdateSlotAsync(expertUserId, id, request);
            return Ok(ApiResponse<ExpertSlotDetailResponse>.Ok(result, "Cập nhật khung giờ làm việc thành công."));
        }

        /// <summary>
        /// Bật hoặc tắt nhanh trạng thái nhận hẹn của khung giờ (IsActive)
        /// PATCH /api/v1/expert/slots/{id}/toggle-active
        /// </summary>
        [HttpPatch("{id:guid}/toggle-active")]
        [ProducesResponseType(typeof(ApiResponse<ExpertSlotDetailResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> ToggleSlotActive([FromRoute] Guid id)
        {
            var expertUserId = GetCurrentUserId();
            var result = await _expertSlotService.ToggleSlotActiveAsync(expertUserId, id);
            return Ok(ApiResponse<ExpertSlotDetailResponse>.Ok(result, "Thay đổi trạng thái khung giờ thành công."));
        }

        /// <summary>
        /// Xóa khung giờ làm việc
        /// Chỉ được phép xóa khi khung giờ chưa được đặt lịch và không trong phiên tạm giữ thanh toán.
        /// DELETE /api/v1/expert/slots/{id}
        /// </summary>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> DeleteSlot([FromRoute] Guid id)
        {
            var expertUserId = GetCurrentUserId();
            await _expertSlotService.DeleteSlotAsync(expertUserId, id);
            return Ok(ApiResponse<object>.Ok(null, "Xóa khung giờ làm việc thành công."));
        }
    }
}
