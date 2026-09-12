using System;
using System.IO;
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
    [Authorize] // Bắt buộc đăng nhập
    public class TaxAdminController : ControllerBase
    {
        private readonly ITaxAIProducerService _producerService;

        public TaxAdminController(ITaxAIProducerService producerService)
        {
            _producerService = producerService;
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
    }
}
