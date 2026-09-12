using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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

        public TaxAdminController(
            ITaxAIProducerService producerService,
            IHttpClientFactory httpClientFactory)
        {
            _producerService = producerService;
            _httpClientFactory = httpClientFactory;
        }

        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UploadTaxDocument([FromForm] TaxDocumentUploadRequest request)
        {
            if (request.File == null || request.File.Length == 0)
                return BadRequest("Vui lòng chọn file PDF luật thuế.");

            // 1. LẤY ADMIN ID TỪ TOKEN ĐĂNG NHẬP
            var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("userId")?.Value;
            Guid? adminId = Guid.TryParse(adminIdClaim, out var parsedGuid) ? parsedGuid : null;

            // 2. Chuyển đổi file sang Base64 để gửi qua RabbitMQ (hoặc upload lên Cloud/S3 lấy URL)
            string fileBase64;
            using (var memoryStream = new MemoryStream())
            {
                await request.File.CopyToAsync(memoryStream);
                fileBase64 = Convert.ToBase64String(memoryStream.ToArray());
            }

            var taskId = Guid.NewGuid();

            // 3. Đóng gói message
            var message = new TaxRuleExtractRequestMessage
            {
                TaskId = taskId,
                AdminId = adminId,
                FileName = request.File.FileName,
                FileBase64 = fileBase64,
                TaxYear = request.TaxYear,
                Name = request.Name,
                SourceUrl = request.SourceUrl
            };

            // 4. Bắn sang RabbitMQ cho Python AI xử lý
            _producerService.PublishExtractionTask(message);

            // 5. Trả về ngay cho Frontend không cần chờ AI
            return Accepted(new
            {
                message = "Tài liệu đang được AI phân tích trong nền.",
                taskId = taskId,
                adminId = adminId
            });
        }

        [HttpPost("{id:guid}/approve")]
        public async Task<IActionResult> ApproveTaxRule([FromRoute] Guid id)
        {
            // Lấy ID Admin từ Claims JWT
            var adminIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                               ?? User.FindFirstValue("sub")
                               ?? User.FindFirstValue("adminId");
            if (string.IsNullOrWhiteSpace(adminIdClaim) || !Guid.TryParse(adminIdClaim, out var adminGuid))
            {
                return Unauthorized(new { message = "Không xác định được danh tính Admin từ token." });
            }
            var httpClient = _httpClientFactory.CreateClient("TaxAIService");
            var payload = new { adminId = adminGuid };
            try
            {
                var response = await httpClient.PostAsJsonAsync($"/api/tax-rules/{id}/approve", payload);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<TaxRuleApproveResponse>();
                    return Ok(result);
                }
                var error = await response.Content.ReadFromJsonAsync<object>();
                return StatusCode((int)response.StatusCode, error);
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = "Không thể kết nối tới Tax AI Service.",
                    detail = ex.Message
                });
            }
        }

    }
}
