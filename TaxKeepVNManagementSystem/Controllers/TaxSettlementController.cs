using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.DTOs.SystemConfigs;
using TaxKeepVN.Application.DTOs.TaxSettlement;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVNManagementSystem.Controllers
{
    [ApiController]
    [Route("api/v1/tax-settlements")]
    [Authorize]
    [Tags("Tax Settlement - Quyết Toán Thuế TNCN")]
    public class TaxSettlementController : ControllerBase
    {
        private readonly ITaxSettlementService _service;
        private readonly ISystemConfigService _configService;

        public TaxSettlementController(ITaxSettlementService service, ISystemConfigService configService)
        {
            _service = service;
            _configService = configService;
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  USER Endpoints
        // ══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Xem trước kết quả quyết toán thuế TNCN theo năm và ngày chốt (không lưu DB).
        /// FE gọi API này để hiển thị toàn bộ số liệu và các bậc tính thuế cho người dùng xem trước.
        /// </summary>
        // POST /api/v1/tax-settlements/preview
        [HttpPost("preview", Name = "PreviewTaxSettlement")]
        [EndpointSummary("Xem trước kết quả quyết toán thuế TNCN theo năm và ngày chốt (không lưu DB)")]
        public async Task<IActionResult> Preview([FromBody] TaxSettlementPreviewRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));

            var userId = GetUserIdFromToken();
            var result = await _service.PreviewAsync(userId, request);
            return Ok(ApiResponse<object>.Ok(result, "Tính toán xem trước quyết toán thuế TNCN thành công."));
        }

        /// <summary>
        /// Chốt và lưu hồ sơ quyết toán thuế TNCN chính thức (Lưu DB - Trạng thái LOCKED).
        /// Chỉ gọi khi người dùng đã xác nhận số liệu xem trước (preview) để chốt hồ sơ và xuất file.
        /// </summary>
        // POST /api/v1/tax-settlements/export
        [HttpPost("export", Name = "ExportTaxSettlement")]
        [EndpointSummary("Chốt và lưu hồ sơ quyết toán thuế TNCN chính thức (Lưu DB - Trạng thái LOCKED)")]
        public async Task<IActionResult> Export([FromBody] TaxSettlementExportRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));

            var userId = GetUserIdFromToken();
            var result = await _service.ExportAsync(userId, request);
            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<object>.Ok(result, "Hồ sơ quyết toán thuế TNCN đã được chốt thành công."));
        }

        /// <summary>
        /// Lấy danh sách tất cả các hồ sơ quyết toán thuế TNCN đã chốt của người nộp thuế hiện tại.
        /// </summary>
        // GET /api/v1/tax-settlements
        [HttpGet(Name = "GetTaxSettlements")]
        [EndpointSummary("Lấy danh sách tất cả các hồ sơ quyết toán thuế đã chốt của người nộp thuế")]
        public async Task<IActionResult> GetList()
        {
            var userId = GetUserIdFromToken();
            var result = await _service.GetListAsync(userId);
            return Ok(ApiResponse<object>.Ok(result, "Lấy danh sách hồ sơ quyết toán thành công."));
        }

        /// <summary>
        /// Xem chi tiết một hồ sơ quyết toán thuế TNCN theo UUID (kèm snapshot biểu thuế và số liệu đầy đủ).
        /// </summary>
        // GET /api/v1/tax-settlements/{id}
        [HttpGet("{id:guid}", Name = "GetTaxSettlementById")]
        [EndpointSummary("Xem chi tiết một hồ sơ quyết toán thuế TNCN theo UUID (kèm snapshot biểu thuế)")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var userId = GetUserIdFromToken();
            var result = await _service.GetByIdAsync(id, userId);
            return Ok(ApiResponse<object>.Ok(result, "Lấy chi tiết hồ sơ quyết toán thành công."));
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  ADMIN Endpoints — Quản lý cấu hình biểu thuế
        // ══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// [Admin] Cập nhật biểu thuế lũy tiến từng phần theo năm (số bậc, ngưỡng, thuế suất, khấu trừ nhanh).
        /// </summary>
        // PUT /api/v1/tax-settlements/admin/brackets
        [HttpPut("admin/brackets", Name = "UpdatePitBrackets")]
        [EndpointSummary("[Admin] Cập nhật biểu thuế lũy tiến từng phần theo năm (5 bậc hoặc 7 bậc)")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> UpdateBrackets([FromBody] UpdatePitBracketsRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));

            if (request.Brackets == null || request.Brackets.Count == 0)
                return BadRequest(ApiResponse<object>.Fail("VALIDATION_ERROR", "Phải cung cấp ít nhất 1 bậc thuế."));

            for (int i = 0; i < request.Brackets.Count - 1; i++)
            {
                var b = request.Brackets[i];
                if (b.ToMonthly.HasValue && b.FromMonthly >= b.ToMonthly)
                    return BadRequest(ApiResponse<object>.Fail("INVALID_BRACKET",
                        $"Bậc {b.BracketNo}: FromMonthly phải nhỏ hơn ToMonthly."));
            }

            var configKey = "PIT_BRACKETS_JSON";
            var jsonValue = System.Text.Json.JsonSerializer.Serialize(request.Brackets);

            await _configService.UpdateAsync(configKey, new SystemConfigUpdateDto
            {
                ConfigValue = jsonValue,
                AppliesFromYear = request.TaxYear,
                Description = $"Biểu thuế TNCN lũy tiến — {request.Brackets.Count} bậc — áp dụng từ năm {request.TaxYear}"
            });

            return Ok(ApiResponse<object>.Ok(
                new { configKey, bracketCount = request.Brackets.Count, appliesFromYear = request.TaxYear },
                $"Cập nhật biểu thuế {request.Brackets.Count} bậc cho năm {request.TaxYear} thành công."));
        }

        /// <summary>
        /// [Admin] Cập nhật mức giảm trừ bản thân, người phụ thuộc và tỷ lệ bảo hiểm bắt buộc theo năm.
        /// </summary>
        // PUT /api/v1/tax-settlements/admin/deductions
        [HttpPut("admin/deductions", Name = "UpdateDeductionRates")]
        [EndpointSummary("[Admin] Cập nhật mức giảm trừ bản thân, người phụ thuộc và tỷ lệ bảo hiểm bắt buộc")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> UpdateDeductions([FromBody] UpdateDeductionRatesRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));

            await _configService.UpdateAsync("PIT_DEDUCTION_PERSONAL_MONTHLY",
                new SystemConfigUpdateDto
                {
                    ConfigValue = request.PersonalMonthly.ToString(),
                    AppliesFromYear = request.TaxYear,
                    Description = $"Giảm trừ bản thân {request.PersonalMonthly:N0} VNĐ/tháng — từ năm {request.TaxYear}"
                });

            await _configService.UpdateAsync("PIT_DEDUCTION_DEPENDENT_MONTHLY",
                new SystemConfigUpdateDto
                {
                    ConfigValue = request.DependentMonthly.ToString(),
                    AppliesFromYear = request.TaxYear,
                    Description = $"Giảm trừ NPT {request.DependentMonthly:N0} VNĐ/người/tháng — từ năm {request.TaxYear}"
                });

            await _configService.UpdateAsync("PIT_INSURANCE_BHXH_RATE",
                new SystemConfigUpdateDto
                {
                    ConfigValue = request.BhxhRate.ToString(),
                    Description = $"Tỷ lệ BHXH {request.BhxhRate:P1}"
                });

            await _configService.UpdateAsync("PIT_INSURANCE_BHYT_RATE",
                new SystemConfigUpdateDto
                {
                    ConfigValue = request.BhytRate.ToString(),
                    Description = $"Tỷ lệ BHYT {request.BhytRate:P1}"
                });

            await _configService.UpdateAsync("PIT_INSURANCE_BHTN_RATE",
                new SystemConfigUpdateDto
                {
                    ConfigValue = request.BhtnRate.ToString(),
                    Description = $"Tỷ lệ BHTN {request.BhtnRate:P1}"
                });

            await _configService.UpdateAsync("PIT_SMALL_AMOUNT_EXEMPTION",
                new SystemConfigUpdateDto
                {
                    ConfigValue = request.SmallAmountExemption.ToString(),
                    Description = $"Ngưỡng miễn phạt nộp thiếu ≤ {request.SmallAmountExemption:N0} VNĐ"
                });

            return Ok(ApiResponse<object>.Ok(null,
                $"Cập nhật mức giảm trừ và tỷ lệ bảo hiểm cho năm {request.TaxYear} thành công."));
        }

        // ── Helper ───────────────────────────────────────────────────────────────

        private Guid GetUserIdFromToken()
        {
            var userIdClaim = User.FindFirst("userId")?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                throw new UnauthorizedException("INVALID_TOKEN",
                    "Không thể xác định danh tính người dùng từ token.");
            }
            return userId;
        }
    }
}
