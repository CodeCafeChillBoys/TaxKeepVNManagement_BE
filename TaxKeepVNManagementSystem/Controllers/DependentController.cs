using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Requests.Dependent;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVNManagementSystem.Controllers
{
    [ApiController]
    [Route("api/v1/dependents")]
    [Authorize]
    public class DependentController : ControllerBase
    {
        private readonly IDependentService _dependentService;
        private readonly IDependentReminderService _reminderService;
        private readonly IOcrService _ocrService;
        private readonly IDependentRuleService _dependentRuleService;

        public DependentController(
            IDependentService dependentService,
            IDependentReminderService reminderService,
            IOcrService ocrService,
            IDependentRuleService dependentRuleService)
        {
            _dependentService = dependentService;
            _reminderService = reminderService;
            _ocrService = ocrService;
            _dependentRuleService = dependentRuleService;
        }

        /// <summary>
        /// Bóc tách thông tin CCCD hoặc Giấy khai sinh người phụ thuộc để tự động điền Form đăng ký.
        /// Tự động gợi ý nhóm điều kiện (CHILD_UNDER_18,...) và kiểm tra người này đã được đăng ký NPT chưa.
        /// </summary>
        [HttpPost("ocr-extractions", Name = "ExtractDependentOcr")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ExtractDependentOcr([FromForm] TaxKeepVN.Application.DTOs.OcrAI.OcrDocumentUploadRequestDto request)
        {
            var result = await _ocrService.ProcessDependentOcrAsync(request.File, request.BackFile);
            return Ok(ApiResponse<TaxKeepVN.Application.DTOs.OcrAI.DependentOcrResponseDto>.Ok(result, "Bóc tách thông tin người phụ thuộc thành công."));
        }

        /// <summary>
        /// [Public] Lấy danh sách các nhóm điều kiện người phụ thuộc.
        /// Dùng để hiển thị dropdown trên Mobile/Web. Không cần đăng nhập.
        /// Hỗ trợ filter theo Relationship: ?relationship=CHILD | SPOUSE | PARENT | OTHER_DEPENDENT
        /// Nguồn dữ liệu: bảng dependent_document_rules (is_active=true) — được đồng bộ từ TaxAIService sau khi Admin Approve bộ luật.
        /// Fallback: nếu DB chưa có dữ liệu, trả về danh sách cứng mặc định.
        /// GET /api/v1/dependents/groups
        /// </summary>
        [HttpGet("groups", Name = "GetDependentGroups")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetDependentGroups([FromQuery] string? relationship = null)
        {
            // ── Đọc từ DB (dependent_document_rules is_active=true) thông qua DependentRuleService ──
            // Dữ liệu được đồng bộ từ TaxAIService khi Admin Approve bộ luật.
            // IMemoryCache 1 tiếng — cực nhanh và không phụ thuộc TaxAIService nhạt mạng.
            var dbGroups = (await _dependentRuleService.GetActiveGroupsAsync(relationship))?.ToList();

            string[] result;
            if (dbGroups != null && dbGroups.Count > 0)
            {
                result = dbGroups.ToArray();
            }
            else
            {
                // Fallback nếu DB chưa có dữ liệu (chưa sync từ TaxAIService lần nào)
                var defaultGroups = new List<string>
                {
                    "CHILD_UNDER_18", "CHILD_OVER_18_DISABLED", "CHILD_OVER_18_STUDYING",
                    "SPOUSE_DISABLED", "SPOUSE_RETIRED",
                    "PARENT_DISABLED", "PARENT_RETIRED",
                    "OTHER_HELPLESS"
                };

                if (!string.IsNullOrWhiteSpace(relationship))
                {
                    var prefix = relationship.Trim().ToUpper() + "_";
                    var filtered = defaultGroups.Where(g => g.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase)).ToList();
                    if (filtered.Count == 0)
                        return BadRequest(ApiResponse<object>.Fail(
                            "INVALID_RELATIONSHIP",
                            $"Nhóm quan hệ '{relationship.Trim().ToUpper()}' không hợp lệ."));
                    result = filtered.ToArray();
                }
                else
                {
                    result = defaultGroups.ToArray();
                }
            }

            return Ok(ApiResponse<object>.Ok(result, "Lấy danh sách nhóm người phụ thuộc thành công."));
        }

        /// <summary>
        /// Đăng ký người phụ thuộc mới cho người nộp thuế đang đăng nhập.
        /// Sau khi tạo thành công, sử dụng dependentId trong response để upload
        /// giấy tờ minh chứng qua endpoint: POST /api/v1/dependents/{dependentId}/documents
        /// </summary>
        [HttpPost(Name = "CreateDependent")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CreateDependent([FromBody] CreateDependentRequest request)
        {
            var taxpayerId = GetUserIdFromToken();
            var result = await _dependentService.CreateDependentAsync(taxpayerId, request);

            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<object>.Ok(result,
                    "Đăng ký người phụ thuộc thành công. " +
                    "Vui lòng upload giấy tờ minh chứng để hoàn tất hồ sơ."));
        }

        /// <summary>
        /// Lấy danh sách nhắc nhở chuyển nhóm tuổi người phụ thuộc.
        /// GET /api/v1/dependents/reminders/age-transitions?taxYear=2026&page=1&size=10&search=Nguyen&sort=daysRemaining
        /// </summary>
        [HttpGet("reminders/age-transitions", Name = "GetAgeTransitionReminders")]
        public async Task<IActionResult> GetAgeTransitionReminders(
            [FromQuery][Range(2000, 2100, ErrorMessage = "Năm tính thuế phải từ 2000 đến 2100.")] int taxYear = 2026,
            [FromQuery] QueryParameters query = null!)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));

            query ??= new QueryParameters();
            var userId = GetUserIdFromToken();

            var data = await _reminderService.GetAgeTransitionRemindersAsync(userId, taxYear, query);
            return Ok(ApiResponse<object>.Ok(data, "Lấy danh sách nhắc nhở chuyển nhóm tuổi NPT thành công."));
        }

        /// <summary>
        /// Lấy danh sách người phụ thuộc của người nộp thuế đang đăng nhập.
        /// Hỗ trợ phân trang, tìm kiếm theo tên/CCCD, lọc theo trạng thái hồ sơ và nhóm quan hệ.
        /// GET /api/v1/dependents?page=1&amp;size=10&amp;search=Nguyen&amp;status=ACTIVE&amp;relationship=CHILD&amp;sort=fullName
        /// </summary>
        [HttpGet(Name = "GetDependents")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetDependents([FromQuery] DependentQueryParameters query)
        {
            query ??= new DependentQueryParameters();
            var taxpayerId = GetUserIdFromToken();

            var result = await _dependentService.GetDependentsAsync(taxpayerId, query);
            return Ok(ApiResponse<object>.Ok(result, "Lấy danh sách người phụ thuộc thành công."));
        }

        /// <summary>
        /// Xem chi tiết một người phụ thuộc, bao gồm thông tin cá nhân và toàn bộ giấy tờ đã upload.
        /// GET /api/v1/dependents/{id}
        /// </summary>
        [HttpGet("{dependentId:guid}", Name = "GetDependentById")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetDependentById([FromRoute] Guid dependentId)
        {
            var taxpayerId = GetUserIdFromToken();
            var result = await _dependentService.GetDependentByIdAsync(taxpayerId, dependentId);
            return Ok(ApiResponse<object>.Ok(result, "Lấy thông tin chi tiết người phụ thuộc thành công."));
        }

        // ── Helper ──────────────────────────────────────────────────────────────
        private Guid GetUserIdFromToken()
        {
            var userIdClaim = User.FindFirst("userId")?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                throw new UnauthorizedException("INVALID_TOKEN",
                    "Không thể xác định danh tính người dùng từ token. Vui lòng đăng nhập lại.");
            }
            return userId;
        }

        /// <summary>
        /// Chuyển nhóm điều kiện của người phụ thuộc khi có sự thay đổi (ví dụ: con đủ 18 tuổi).
        /// Giữ nguyên toàn bộ thông tin định danh, chỉ cập nhật CurrentGroup và tùy chọn Note.
        /// Tự động reset trạng thái hồ sơ về PENDING_DOCUMENTS và gửi thông báo yêu cầu bổ sung giấy tờ.
        /// Response trả về đủ thông tin để FE điều hướng thẳng tới trang upload giấy tờ mới.
        /// PATCH /api/v1/dependents/{dependentId}/group
        /// </summary>
        [HttpPatch("{dependentId:guid}/group", Name = "UpdateDependentGroup")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateDependentGroup(
            [FromRoute] Guid dependentId,
            [FromBody] UpdateDependentGroupRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));

            var taxpayerId = GetUserIdFromToken();
            var result = await _dependentService.UpdateGroupAsync(taxpayerId, dependentId, request);

            return Ok(ApiResponse<object>.Ok(result,
                $"Chuyển nhóm người phụ thuộc thành công. " +
                $"Vui lòng bổ sung giấy tờ minh chứng cho nhóm mới '{result.CurrentGroup}'."));
        }

        /// <summary>
        /// Vô hiệu hóa (xóa mềm) người phụ thuộc khi không còn đủ điều kiện hoặc đã mất.
        /// Dữ liệu vẫn được lưu trong hệ thống phục vụ tra cứu lịch sử.
        /// Có thể cung cấp lý do trong body: "Không còn là người phụ thuộc", "Đã mất", v.v.
        /// DELETE /api/v1/dependents/{dependentId}
        /// </summary>
        [HttpDelete("{dependentId:guid}", Name = "DeleteDependent")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteDependent(
            [FromRoute] Guid dependentId,
            [FromBody] DeleteDependentRequest? request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));

            var taxpayerId = GetUserIdFromToken();
            var result = await _dependentService.DeleteDependentAsync(taxpayerId, dependentId, request ?? new DeleteDependentRequest());

            return Ok(ApiResponse<object>.Ok(result,
                $"Người phụ thuộc '{result.FullName}' đã được vô hiệu hóa thành công."));
        }
    }
}
