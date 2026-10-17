using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.DTOs.SystemConfigs;
using TaxKeepVN.Application.DTOs.TaxSettlement;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVNManagementSystem.Controllers
{
    [ApiController]
    [Route("api/v1/tax-settlements")]
    [Authorize]
    [Produces("application/json")]
    [Tags("Tax Settlement - Quyết Toán Thuế TNCN")]
    public class TaxSettlementController : ControllerBase
    {
        private readonly ITaxSettlementService _service;
        private readonly ISystemConfigService _configService;
        private readonly ITaxSettlementPackageService _packageService;
        private readonly IDownloadTokenService _downloadTokenService;
        private readonly IWebHostEnvironment _env;
        private readonly IUnitOfWork _unitOfWork;

        public TaxSettlementController(
            ITaxSettlementService service,
            ISystemConfigService configService,
            ITaxSettlementPackageService packageService,
            IDownloadTokenService downloadTokenService,
            IWebHostEnvironment env,
            IUnitOfWork unitOfWork)
        {
            _service = service;
            _configService = configService;
            _packageService = packageService;
            _downloadTokenService = downloadTokenService;
            _env = env;
            _unitOfWork = unitOfWork;
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  USER Endpoints
        // ══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Đếm ngược hạn nộp quyết toán thuế TNCN kèm số ngày còn lại và mức độ cấp bách (Mục 4 Góp ý Hội đồng).
        /// GET /api/v1/tax-settlements/deadline-countdown
        /// </summary>
        // GET /api/v1/tax-settlements/deadline-countdown
        [HttpGet("deadline-countdown", Name = "GetTaxSettlementDeadlineCountdown")]
        [AllowAnonymous]
        [EndpointSummary("Đếm ngược hạn chót nộp hồ sơ quyết toán thuế TNCN cho Dashboard khách hàng")]
        [ProducesResponseType(typeof(ApiResponse<TaxSettlementDeadlineCountdownDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetDeadlineCountdown([FromQuery] int? taxYear = null)
        {
            // 1. Đọc lịch trình từ bảng chuyên biệt tax_settlement_schedules
            var scheduleRepo = _unitOfWork.Repository<TaxSettlementSchedule>();
            var schedules = await scheduleRepo.FindAsync(s => s.IsActive && (taxYear == null || s.TaxYear == null || s.TaxYear == taxYear));
            var schedule = schedules.OrderByDescending(s => s.TaxYear ?? 0).FirstOrDefault();

            int deadlineMonth = schedule?.DeadlineMonth ?? 4;
            int deadlineDay = schedule?.DeadlineDay ?? 30;
            var reminderMilestones = (schedule?.ReminderDays ?? "60,30,15,7,3,1")
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s.Trim(), out var val) ? val : 0)
                .Where(v => v > 0)
                .OrderByDescending(v => v)
                .ToList();

            // 2. Xác định ngày hiện tại theo giờ Việt Nam (UTC+7)
            var vietnamNow = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).Date;
            int currentYear = vietnamNow.Year;

            // Hạn chót của năm hiện tại
            var deadlineThisYear = new DateTime(currentYear, deadlineMonth, deadlineDay);

            // Xác định năm quyết toán và năm hạn nộp tiếp theo (Next Upcoming Deadline)
            int resolvedTaxYear;
            int deadlineYear;

            if (taxYear.HasValue)
            {
                var specifiedDeadline = new DateTime(taxYear.Value + 1, deadlineMonth, deadlineDay);
                if (vietnamNow <= specifiedDeadline)
                {
                    resolvedTaxYear = taxYear.Value;
                    deadlineYear = resolvedTaxYear + 1;
                }
                else
                {
                    // Đã qua hạn của năm chỉ định -> Luôn tự động chuyển sang đếm ngược hạn tiếp theo chứ không ghi quá hạn
                    if (vietnamNow <= deadlineThisYear)
                    {
                        resolvedTaxYear = currentYear - 1;
                        deadlineYear = currentYear;
                    }
                    else
                    {
                        resolvedTaxYear = currentYear;
                        deadlineYear = currentYear + 1;
                    }
                }
            }
            else
            {
                // Mặc định tự động đếm ngược đến hạn nộp tiếp theo:
                // Nếu chưa qua hạn nộp năm nay (<= 30/04): đếm ngược đến 30/04 năm nay cho năm thuế currentYear - 1
                // Nếu đã qua hạn nộp năm nay (> 30/04): chuyển sang đếm ngược đến 30/04 năm sau cho năm thuế currentYear
                if (vietnamNow <= deadlineThisYear)
                {
                    resolvedTaxYear = currentYear - 1;
                    deadlineYear = currentYear;
                }
                else
                {
                    resolvedTaxYear = currentYear;
                    deadlineYear = currentYear + 1;
                }
            }

            var deadlineDate = new DateTime(deadlineYear, deadlineMonth, deadlineDay);
            int daysRemaining = Math.Max(0, (deadlineDate - vietnamNow).Days);

            // 3. Đánh giá mức độ cấp bách và thông điệp nhắc nhở (KHÔNG BAO GIỜ hiển thị OVERDUE)
            string urgencyLevel;
            string reminderMessage;

            if (daysRemaining == 0)
            {
                urgencyLevel = "CRITICAL";
                reminderMessage = $"HÔM NAY là hạn chót nộp hồ sơ quyết toán thuế TNCN năm {resolvedTaxYear} (ngày {deadlineDate:dd/MM/yyyy}). Vui lòng hoàn thiện và nộp hồ sơ ngay!";
            }
            else if (daysRemaining <= 7)
            {
                urgencyLevel = "CRITICAL";
                reminderMessage = $"Hạn chót nộp hồ sơ quyết toán thuế TNCN năm {resolvedTaxYear} là ngày {deadlineDate:dd/MM/yyyy}. Bạn còn {daysRemaining} ngày để hoàn thiện hồ sơ.";
            }
            else if (daysRemaining <= 30)
            {
                urgencyLevel = "WARNING";
                reminderMessage = $"Hạn chót nộp hồ sơ quyết toán thuế TNCN năm {resolvedTaxYear} là ngày {deadlineDate:dd/MM/yyyy}. Bạn còn {daysRemaining} ngày để hoàn thiện hồ sơ.";
            }
            else
            {
                urgencyLevel = "NORMAL";
                reminderMessage = $"Hạn chót nộp hồ sơ quyết toán thuế TNCN năm {resolvedTaxYear} là ngày {deadlineDate:dd/MM/yyyy}. Bạn còn {daysRemaining} ngày để hoàn thiện hồ sơ.";
            }

            // 4. Cơ chế hỏi: "Bạn đã quyết toán thuế chưa?" (1 lần duy nhất)
            // Kích hoạt khi: đúng vào ngày hạn nộp hoặc qua ngày hạn nộp (vietnamNow >= deadlineThisYear)
            bool shouldPromptSettlementCheck = false;
            int? promptTaxYear = null;
            string? promptQuestion = null;

            // Kiểm tra xem request có token của user đã đăng nhập hay không
            Guid? currentUserId = null;
            if (User.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = User.FindFirst("userId")?.Value
                    ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var parsedId))
                {
                    currentUserId = parsedId;
                }
            }

            if (currentUserId.HasValue && vietnamNow >= deadlineThisYear)
            {
                int settlementYearToCheck = currentYear - 1;
                promptTaxYear = settlementYearToCheck;

                // Kiểm tra 1: User đã chốt hồ sơ quyết toán thuế cho năm đó chưa (Status == "LOCKED")
                var dossierRepo = _unitOfWork.Repository<TaxSettlementDossier>();
                var settledDossiers = await dossierRepo.FindAsync(d =>
                    d.TaxpayerId == currentUserId.Value &&
                    d.TaxYear == settlementYearToCheck &&
                    d.Status == "LOCKED");

                bool hasSettled = settledDossiers.Any();

                if (!hasSettled)
                {
                    // Kiểm tra 2: User đã từng được hiển thị câu hỏi nhắc nhở này chưa (1 lần duy nhất)
                    var notifRepo = _unitOfWork.Repository<SystemNotification>();
                    var promptTargetUrl = $"/tax-settlement/{settlementYearToCheck}";
                    var existingPrompts = await notifRepo.FindAsync(n =>
                        n.UserId == currentUserId.Value &&
                        n.NotificationType == "SETTLEMENT_PROMPT_SHOWN" &&
                        (n.TargetActionUrl == settlementYearToCheck.ToString() || n.TargetActionUrl == promptTargetUrl));

                    if (!existingPrompts.Any())
                    {
                        shouldPromptSettlementCheck = true;
                        promptQuestion = vietnamNow == deadlineThisYear
                            ? $"Hôm nay là hạn chót nộp hồ sơ quyết toán thuế TNCN năm {settlementYearToCheck} (ngày {deadlineThisYear:dd/MM/yyyy}). Bạn đã quyết toán thuế chưa?"
                            : $"Hạn chót nộp hồ sơ quyết toán thuế TNCN năm {settlementYearToCheck} đã qua (ngày {deadlineThisYear:dd/MM/yyyy}). Bạn đã quyết toán thuế chưa?";
                    }
                }
            }

            var result = new TaxSettlementDeadlineCountdownDto
            {
                TaxYear = resolvedTaxYear,
                DeadlineDate = deadlineDate.ToString("yyyy-MM-dd"),
                DeadlineFormatted = deadlineDate.ToString("dd/MM/yyyy"),
                DaysRemaining = daysRemaining,
                ReminderMessage = reminderMessage,
                UrgencyLevel = urgencyLevel,
                IsOverdue = false,
                ReminderMilestones = reminderMilestones,
                ShouldPromptSettlementCheck = shouldPromptSettlementCheck,
                PromptTaxYear = shouldPromptSettlementCheck ? promptTaxYear : null,
                PromptQuestion = shouldPromptSettlementCheck ? promptQuestion : null
            };

            return Ok(ApiResponse<TaxSettlementDeadlineCountdownDto>.Ok(result, "Lấy thông tin đếm ngược hạn nộp quyết toán thuế thành công."));
        }

        /// <summary>
        /// Xác nhận đã hiển thị / người dùng đã phản hồi câu hỏi nhắc nhở "Bạn đã quyết toán thuế chưa?".
        /// Lưu vết vào hệ thống để không bao giờ hiển thị lại cho năm quyết toán này nữa (1 lần duy nhất).
        /// </summary>
        // POST /api/v1/tax-settlements/acknowledge-prompt
        [HttpPost("acknowledge-prompt", Name = "AcknowledgeSettlementPrompt")]
        [Authorize]
        [EndpointSummary("Xác nhận đã hiển thị câu hỏi nhắc nhở quyết toán thuế (1 lần duy nhất)")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        public async Task<IActionResult> AcknowledgeSettlementPrompt([FromBody] AcknowledgeSettlementPromptRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));

            var userId = GetUserIdFromToken();
            var notifRepo = _unitOfWork.Repository<SystemNotification>();

            var promptTargetUrl = $"/tax-settlement/{request.TaxYear}";
            var existing = await notifRepo.FindAsync(n =>
                n.UserId == userId &&
                n.NotificationType == "SETTLEMENT_PROMPT_SHOWN" &&
                (n.TargetActionUrl == request.TaxYear.ToString() || n.TargetActionUrl == promptTargetUrl));

            if (!existing.Any())
            {
                string messageText = request.Response?.Trim().ToUpperInvariant() switch
                {
                    "YES" => $"Người dùng đã xác nhận đã hoàn thành quyết toán thuế năm {request.TaxYear}.",
                    "NO" => $"Người dùng xác nhận chưa quyết toán thuế năm {request.TaxYear}.",
                    _ => $"Đã hiển thị popup nhắc nhở quyết toán thuế TNCN năm {request.TaxYear} cho người dùng."
                };

                var notif = new SystemNotification
                {
                    NotificationId = Guid.NewGuid(),
                    UserId = userId,
                    Title = $"Nhắc nhở quyết toán thuế TNCN năm {request.TaxYear}",
                    Message = messageText,
                    NotificationType = "SETTLEMENT_PROMPT_SHOWN",
                    IsRead = true,
                    TargetActionUrl = promptTargetUrl,
                    CreatedAt = DateTime.UtcNow
                };

                await notifRepo.AddAsync(notif);
                await _unitOfWork.SaveChangesAsync();
            }

            return Ok(ApiResponse<object>.Ok(null, "Đã ghi nhận xác nhận nhắc nhở quyết toán thuế thành công."));
        }

        /// <summary>
        /// Kiểm tra trạng thái nhắc nhở "Bạn đã quyết toán thuế chưa?" cho người dùng hiện tại (1 lần duy nhất).
        /// </summary>
        // GET /api/v1/tax-settlements/settlement-prompt
        [HttpGet("settlement-prompt", Name = "GetSettlementPromptCheck")]
        [Authorize]
        [EndpointSummary("Kiểm tra xem người dùng có cần hiển thị popup nhắc nhở quyết toán thuế (1 lần duy nhất) hay không")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetSettlementPromptCheck([FromQuery] int? taxYear = null)
        {
            var userId = GetUserIdFromToken();

            var scheduleRepo = _unitOfWork.Repository<TaxSettlementSchedule>();
            var schedules = await scheduleRepo.FindAsync(s => s.IsActive && (taxYear == null || s.TaxYear == null || s.TaxYear == taxYear));
            var schedule = schedules.OrderByDescending(s => s.TaxYear ?? 0).FirstOrDefault();

            int deadlineMonth = schedule?.DeadlineMonth ?? 4;
            int deadlineDay = schedule?.DeadlineDay ?? 30;

            var vietnamNow = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).Date;
            int currentYear = vietnamNow.Year;
            var deadlineThisYear = new DateTime(currentYear, deadlineMonth, deadlineDay);

            int settlementYearToCheck = taxYear ?? (currentYear - 1);

            bool isPastOrAtDeadline = vietnamNow >= deadlineThisYear;
            if (!isPastOrAtDeadline)
            {
                return Ok(ApiResponse<object>.Ok(new
                {
                    shouldPrompt = false,
                    promptTaxYear = settlementYearToCheck,
                    message = "Chưa đến hạn quyết toán thuế."
                }));
            }

            // Kiểm tra xem đã chốt hồ sơ chưa
            var dossierRepo = _unitOfWork.Repository<TaxSettlementDossier>();
            var settledDossiers = await dossierRepo.FindAsync(d =>
                d.TaxpayerId == userId &&
                d.TaxYear == settlementYearToCheck &&
                d.Status == "LOCKED");

            if (settledDossiers.Any())
            {
                return Ok(ApiResponse<object>.Ok(new
                {
                    shouldPrompt = false,
                    promptTaxYear = settlementYearToCheck,
                    message = "Người dùng đã hoàn thành chốt hồ sơ quyết toán thuế."
                }));
            }

            // Kiểm tra xem đã nhắc trước đó chưa
            var notifRepo = _unitOfWork.Repository<SystemNotification>();
            var promptTargetUrl = $"/tax-settlement/{settlementYearToCheck}";
            var existingPrompts = await notifRepo.FindAsync(n =>
                n.UserId == userId &&
                n.NotificationType == "SETTLEMENT_PROMPT_SHOWN" &&
                (n.TargetActionUrl == settlementYearToCheck.ToString() || n.TargetActionUrl == promptTargetUrl));

            if (existingPrompts.Any())
            {
                return Ok(ApiResponse<object>.Ok(new
                {
                    shouldPrompt = false,
                    promptTaxYear = settlementYearToCheck,
                    message = "Người dùng đã được hỏi nhắc nhở trước đó."
                }));
            }

            string question = vietnamNow == deadlineThisYear
                ? $"Hôm nay là hạn chót nộp hồ sơ quyết toán thuế TNCN năm {settlementYearToCheck} (ngày {deadlineThisYear:dd/MM/yyyy}). Bạn đã quyết toán thuế chưa?"
                : $"Hạn chót nộp hồ sơ quyết toán thuế TNCN năm {settlementYearToCheck} đã qua (ngày {deadlineThisYear:dd/MM/yyyy}). Bạn đã quyết toán thuế chưa?";

            return Ok(ApiResponse<object>.Ok(new
            {
                shouldPrompt = true,
                promptTaxYear = settlementYearToCheck,
                question = question
            }, "Kiểm tra nhắc nhở quyết toán thuế thành công."));
        }

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
        [Produces("application/pdf", "application/zip", "application/octet-stream")]
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
        //  ADMIN Endpoints — Quản lý cấu hình biểu thuế (Bảng riêng chuyên biệt)
        // ══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// [Admin] Lấy danh sách các bậc thuế lũy tiến từng phần theo năm từ bảng chuyên biệt tax_brackets.
        /// </summary>
        // GET /api/v1/tax-settlements/admin/brackets
        [HttpGet("admin/brackets", Name = "GetPitBrackets")]
        [EndpointSummary("[Admin] Lấy danh sách các bậc thuế lũy tiến từng phần theo năm")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetBrackets([FromQuery] int? taxYear = null)
        {
            int year = taxYear ?? DateTime.UtcNow.Year;
            var allBrackets = await _unitOfWork.Repository<TaxBracket>().FindAsync(b => b.IsActive && b.AppliesFromYear <= year);
            int effectiveYear = allBrackets.Any() ? allBrackets.Max(b => b.AppliesFromYear) : year;
            var list = allBrackets
                .Where(b => b.AppliesFromYear == effectiveYear)
                .OrderBy(b => b.BracketNo)
                .ToList();

            if (!list.Any())
            {
                list = (await _unitOfWork.Repository<TaxBracket>().FindAsync(b => b.IsActive))
                    .OrderBy(b => b.AppliesFromYear)
                    .ThenBy(b => b.BracketNo)
                    .ToList();
            }

            return Ok(ApiResponse<object>.Ok(list, $"Lấy danh sách biểu thuế cho năm {effectiveYear} thành công."));
        }

        /// <summary>
        /// [Admin] Lấy chính sách mức giảm trừ gia cảnh và tỷ lệ bảo hiểm theo năm từ bảng chuyên biệt tax_deduction_policies.
        /// </summary>
        // GET /api/v1/tax-settlements/admin/deductions
        [HttpGet("admin/deductions", Name = "GetDeductionRates")]
        [EndpointSummary("[Admin] Lấy chính sách mức giảm trừ gia cảnh và tỷ lệ bảo hiểm theo năm")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetDeductions([FromQuery] int? taxYear = null)
        {
            int year = taxYear ?? DateTime.UtcNow.Year;
            var policies = await _unitOfWork.Repository<TaxDeductionPolicy>().FindAsync(p => p.IsActive && p.AppliesFromYear <= year);
            var policy = policies.OrderByDescending(p => p.AppliesFromYear).FirstOrDefault();

            if (policy == null)
            {
                policy = (await _unitOfWork.Repository<TaxDeductionPolicy>().FindAsync(p => p.IsActive))
                    .OrderBy(p => p.AppliesFromYear)
                    .FirstOrDefault();
            }

            return Ok(ApiResponse<object>.Ok(policy, $"Lấy chính sách giảm trừ cho năm {policy?.AppliesFromYear ?? year} thành công."));
        }

        /// <summary>
        /// [Admin] Cập nhật biểu thuế lũy tiến từng phần theo năm (Lưu trực tiếp vào bảng tax_brackets).
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

            // 1. Cập nhật vào bảng chuyên biệt tax_brackets
            var existingBrackets = (await _unitOfWork.Repository<TaxBracket>()
                .FindAsync(b => b.AppliesFromYear == request.TaxYear))
                .ToList();

            var bracketRepo = _unitOfWork.Repository<TaxBracket>();

            foreach (var item in request.Brackets)
            {
                var existing = existingBrackets.FirstOrDefault(b => b.BracketNo == item.BracketNo);
                if (existing != null)
                {
                    existing.FromMonthly = item.FromMonthly;
                    existing.ToMonthly = item.ToMonthly;
                    existing.Rate = item.Rate;
                    existing.QuickDeduct = item.QuickDeduct;
                    existing.Description = item.Description ?? $"Bậc {item.BracketNo}";
                    existing.IsActive = true;
                    existing.UpdatedAt = DateTimeOffset.UtcNow;
                    bracketRepo.Update(existing);
                }
                else
                {
                    await bracketRepo.AddAsync(new TaxBracket
                    {
                        Id = Guid.NewGuid(),
                        BracketNo = item.BracketNo,
                        FromMonthly = item.FromMonthly,
                        ToMonthly = item.ToMonthly,
                        Rate = item.Rate,
                        QuickDeduct = item.QuickDeduct,
                        AppliesFromYear = request.TaxYear,
                        Description = item.Description ?? $"Bậc {item.BracketNo}",
                        IsActive = true,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    });
                }
            }

            await _unitOfWork.SaveChangesAsync();

            // 2. Đồng bộ sang system_configs để tương thích ngược
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
                $"Cập nhật biểu thuế {request.Brackets.Count} bậc cho năm {request.TaxYear} thành công vào bảng tax_brackets."));
        }

        /// <summary>
        /// [Admin] Cập nhật mức giảm trừ bản thân, người phụ thuộc và tỷ lệ bảo hiểm bắt buộc theo năm (Lưu vào tax_deduction_policies).
        /// </summary>
        // PUT /api/v1/tax-settlements/admin/deductions
        [HttpPut("admin/deductions", Name = "UpdateDeductionRates")]
        [EndpointSummary("[Admin] Cập nhật mức giảm trừ bản thân, người phụ thuộc và tỷ lệ bảo hiểm bắt buộc")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> UpdateDeductions([FromBody] UpdateDeductionRatesRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));

            // 1. Cập nhật vào bảng chuyên biệt tax_deduction_policies
            var policyRepo = _unitOfWork.Repository<TaxDeductionPolicy>();
            var existingPolicies = await policyRepo.FindAsync(p => p.AppliesFromYear == request.TaxYear);
            var existingPolicy = existingPolicies.FirstOrDefault();

            if (existingPolicy != null)
            {
                existingPolicy.PersonalMonthly = request.PersonalMonthly;
                existingPolicy.DependentMonthly = request.DependentMonthly;
                existingPolicy.BhxhRate = request.BhxhRate;
                existingPolicy.BhytRate = request.BhytRate;
                existingPolicy.BhtnRate = request.BhtnRate;
                existingPolicy.SmallAmountExemption = request.SmallAmountExemption;
                existingPolicy.UpdatedAt = DateTimeOffset.UtcNow;
                policyRepo.Update(existingPolicy);
            }
            else
            {
                await policyRepo.AddAsync(new TaxDeductionPolicy
                {
                    Id = Guid.NewGuid(),
                    AppliesFromYear = request.TaxYear,
                    LawName = $"Chính sách giảm trừ thuế TNCN năm {request.TaxYear}",
                    PersonalMonthly = request.PersonalMonthly,
                    DependentMonthly = request.DependentMonthly,
                    BhxhRate = request.BhxhRate,
                    BhytRate = request.BhytRate,
                    BhtnRate = request.BhtnRate,
                    SmallAmountExemption = request.SmallAmountExemption,
                    MedicalMaxYearly = request.TaxYear >= 2026 ? 23000000m : 0m,
                    EducationMaxYearly = request.TaxYear >= 2026 ? 24000000m : 0m,
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
            }

            await _unitOfWork.SaveChangesAsync();

            // 2. Đồng bộ sang system_configs để tương thích ngược
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
                $"Cập nhật mức giảm trừ và tỷ lệ bảo hiểm cho năm {request.TaxYear} thành công vào bảng tax_deduction_policies."));
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
