using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TaxKeepVN.Application.DTOs.TaxAI;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVNManagementSystem.Controllers
{
    [ApiController]
    [Route("api/admin/tax-rules")]
    [Authorize]
    public class TaxAdminController : ControllerBase
    {
        private readonly ITaxAIProducerService _producerService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IDependentRuleService _dependentRuleService;
        private readonly ILogger<TaxAdminController> _logger;

        public TaxAdminController(
            ITaxAIProducerService producerService,
            IHttpClientFactory httpClientFactory,
            IDependentRuleService dependentRuleService,
            ILogger<TaxAdminController> logger)
        {
            _producerService = producerService;
            _httpClientFactory = httpClientFactory;
            _dependentRuleService = dependentRuleService;
            _logger = logger;
        }

        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UploadTaxDocument([FromForm] TaxDocumentUploadRequest request)
        {
            if (request.File == null || request.File.Length == 0)
                return BadRequest("Vui long chon file PDF luat thue.");

            var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("userId")?. Value;
            Guid? adminId = Guid.TryParse(adminIdClaim, out var parsedGuid) ? parsedGuid : null;

            string fileBase64;
            using (var memoryStream = new MemoryStream())
            {
                await request.File.CopyToAsync(memoryStream);
                fileBase64 = Convert.ToBase64String(memoryStream.ToArray());
            }

            var taskId = Guid.NewGuid();
            var message = new TaxRuleExtractRequestMessage
            {
                TaskId    = taskId,
                AdminId   = adminId,
                FileName  = request.File.FileName,
                FileBase64 = fileBase64,
                TaxYear   = request.TaxYear,
                Name      = request.Name,
                SourceUrl = request.SourceUrl
            };
            _producerService.PublishExtractionTask(message);
            return Accepted(new { message = "Tai lieu dang duoc AI phan tich.", taskId, adminId });
        }

        [HttpPost("{id:guid}/approve")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> ApproveTaxRule([FromRoute] Guid id)
        {
            var adminIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                               ?? User.FindFirstValue("sub")
                               ?? User.FindFirstValue("adminId");
            if (string.IsNullOrWhiteSpace(adminIdClaim) || !Guid.TryParse(adminIdClaim, out var adminGuid))
                return Unauthorized(new { message = "Khong xac dinh duoc danh tinh Admin." });

            var httpClient = _httpClientFactory.CreateClient("TaxAIService");
            var payload = new { adminId = adminGuid };

            try
            {
                var approveResponse = await httpClient.PostAsJsonAsync($"/api/tax-rules/{id}/approve", payload);
                if (!approveResponse.IsSuccessStatusCode)
                {
                    var error = await approveResponse.Content.ReadFromJsonAsync<object>();
                    return StatusCode((int)approveResponse.StatusCode, error);
                }
                var approveResult = await approveResponse.Content.ReadFromJsonAsync<TaxRuleApproveResponse>();

                try
                {
                    var detailResponse = await httpClient.GetAsync($"/api/tax-rules/{id}");
                    if (detailResponse.IsSuccessStatusCode)
                    {
                        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var detail = await detailResponse.Content.ReadFromJsonAsync<TaxRuleDetailFromAiDto>(jsonOptions);
                        var depRules = detail?.Data?.DependentRules;
                        if (depRules != null && depRules.Count > 0)
                        {
                            var (added, updated) = await _dependentRuleService.SyncFromAiAsync(depRules);
                            _logger.LogInformation("[TaxAdmin] Auto-sync sau Approve ruleSetId={Id}: +{Added} moi, {Updated} cap nhat.", id, added, updated);
                        }
                        else
                        {
                            _logger.LogWarning("[TaxAdmin] Approve ruleSetId={Id} OK nhung dependentRules rong.", id);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("[TaxAdmin] Approve OK, khong lay duoc detail ruleSetId={Id}.", id);
                    }
                }
                catch (Exception syncEx)
                {
                    _logger.LogWarning(syncEx, "[TaxAdmin] Auto-sync that bai sau Approve ruleSetId={Id}.", id);
                }

                return Ok(approveResult);
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { message = "Khong the ket noi TaxAIService.", detail = ex.Message });
            }
        }

        [HttpPost("sync-active")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> SyncActiveTaxRules()
        {
            var httpClient = _httpClientFactory.CreateClient("TaxAIService");
            try
            {
                var listResponse = await httpClient.GetAsync("/api/tax-rules");
                if (!listResponse.IsSuccessStatusCode)
                    return StatusCode((int)listResponse.StatusCode, new { message = "Khong lay duoc danh sach bo luat tu TaxAIService." });

                var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var ruleSets = await listResponse.Content
                    .ReadFromJsonAsync<System.Collections.Generic.List<TaxRuleSetFromAiDto>>(jsonOptions);

                if (ruleSets == null || ruleSets.Count == 0)
                    return NotFound(new { message = "TaxAIService chua co bo luat nao." });

                var activeSets = ruleSets.FindAll(r =>
                    string.Equals(r.Status, "Active", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(r.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase));

                if (activeSets.Count == 0)
                    return NotFound(new { message = "Khong co bo luat nao dang Active trong TaxAIService." });

                int totalAdded = 0, totalUpdated = 0;
                foreach (var ruleSet in activeSets)
                {
                    if (ruleSet.RuleSetId == null) continue;
                    var detailResp = await httpClient.GetAsync($"/api/tax-rules/{ruleSet.RuleSetId}");
                    if (!detailResp.IsSuccessStatusCode) continue;
                    var detail = await detailResp.Content.ReadFromJsonAsync<TaxRuleDetailFromAiDto>(jsonOptions);
                    var depRules = detail?.Data?.DependentRules;
                    if (depRules == null || depRules.Count == 0) continue;
                    var (added, updated) = await _dependentRuleService.SyncFromAiAsync(depRules);
                    totalAdded += added;
                    totalUpdated += updated;
                }

                _logger.LogInformation("[TaxAdmin] sync-active hoan tat: +{Added} moi, {Updated} cap nhat.", totalAdded, totalUpdated);

                return Ok(new
                {
                    message = "Dong bo bo luat Active tu TaxAIService thanh cong.",
                    activeSetsCount = activeSets.Count,
                    added = totalAdded,
                    updated = totalUpdated
                });
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { message = "Khong the ket noi TaxAIService.", detail = ex.Message });
            }
        }
    }
}
