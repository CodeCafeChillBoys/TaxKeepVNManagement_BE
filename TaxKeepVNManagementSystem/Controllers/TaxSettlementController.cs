using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
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
        private readonly ITaxSettlementPackageService _packageService;
        private readonly IDownloadTokenService _downloadTokenService;
        private readonly IWebHostEnvironment _env;

        public TaxSettlementController(
            ITaxSettlementService service,
            ISystemConfigService configService,
            ITaxSettlementPackageService packageService,
            IDownloadTokenService downloadTokenService,
            IWebHostEnvironment env)
        {
            _service = service;
            _configService = configService;
            _packageService = packageService;
            _downloadTokenService = downloadTokenService;
            _env = env;
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

        /// <summary>
        /// Kết xuất Tờ khai Quyết toán thuế TNCN Mẫu 02/QTT-TNCN và các Phụ lục dưới dạng file PDF (WBS 3.6.T7).
        /// Hỗ trợ truyền thông tin CQT/Tài khoản hoàn thuế qua body hoặc tự động fallback vào Profile người dùng.
        /// </summary>
        // POST /api/v1/tax-settlements/{id}/export-pdf
        [HttpPost("{id:guid}/export-pdf", Name = "ExportTaxSettlementPdf")]
        [EndpointSummary("Kết xuất Tờ khai Quyết toán thuế TNCN Mẫu 02/QTT-TNCN và các Phụ lục dạng PDF")]
        public async Task<IActionResult> ExportPdf(Guid id, [FromBody] TaxSettlementExportPdfRequest? request = null)
        {
            var userId = GetUserIdFromToken();
            var (pdfBytes, fileName) = await _packageService.GeneratePdfAsync(id, userId, request);
            return File(pdfBytes, "application/pdf", fileName);
        }

        /// <summary>
        /// Đóng gói toàn bộ hồ sơ quyết toán (Tờ khai PDF + chứng từ y tế/giáo dục gốc) thành file .ZIP (WBS 3.6.T8).
        /// Trả về đường dẫn tải file (Signed Download URL) có thời hạn 30 phút.
        /// </summary>
        // POST /api/v1/tax-settlements/{id}/export-zip
        [HttpPost("{id:guid}/export-zip", Name = "ExportTaxSettlementZip")]
        [EndpointSummary("Đóng gói toàn bộ hồ sơ quyết toán (PDF + chứng từ gốc) thành file ZIP")]
        public async Task<IActionResult> ExportZip(Guid id, [FromBody] TaxSettlementExportZipRequest? request = null)
        {
            var userId = GetUserIdFromToken();
            var result = await _packageService.CreateZipPackageAsync(id, userId, request);
            return Ok(ApiResponse<TaxSettlementPackageZipResponse>.Ok(result, result.Message));
        }

        /// <summary>
        /// Tải tệp hồ sơ quyết toán (PDF hoặc ZIP) qua Signed Download Token an toàn có thời hạn 30 phút (WBS 3.6.T9).
        /// Endpoint công khai (AllowAnonymous), xác thực thông qua chữ ký HMAC-SHA256 của token.
        /// </summary>
        // GET /api/v1/tax-settlements/download/{token}
        [HttpGet("download/{token}", Name = "DownloadSettlementFile")]
        [AllowAnonymous]
        [EndpointSummary("Tải tệp hồ sơ quyết toán an toàn qua Signed Download Token (30 phút hiệu lực)")]
        public IActionResult DownloadFile(string token)
        {
            var payload = _downloadTokenService.ValidateToken(token);
            if (payload == null)
            {
                return StatusCode(StatusCodes.Status410Gone,
                    ApiResponse<object>.Fail("TOKEN_EXPIRED_OR_INVALID",
                        "Đường dẫn tải tệp không hợp lệ hoặc đã hết hạn hiệu lực (30 phút). Vui lòng yêu cầu kết xuất lại."));
            }

            var rootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var cleanRel = payload.RelativeFilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(rootPath, cleanRel);

            if (!System.IO.File.Exists(fullPath))
            {
                return NotFound(ApiResponse<object>.Fail("FILE_NOT_FOUND",
                    "Tệp tin vật lý không còn tồn tại trên máy chủ. Vui lòng thực hiện kết xuất lại."));
            }

            var contentType = string.Equals(payload.FileType, "pdf", StringComparison.OrdinalIgnoreCase)
                ? "application/pdf"
                : "application/zip";

            var downloadName = !string.IsNullOrWhiteSpace(payload.DownloadFileName)
                ? payload.DownloadFileName
                : Path.GetFileName(fullPath);

            return PhysicalFile(fullPath, contentType, downloadName);
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
