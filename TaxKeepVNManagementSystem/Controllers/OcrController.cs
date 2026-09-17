using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using TaxKeepVN.Application.DTOs.OcrAI;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.Service.Interfaces;

namespace TaxKeepVNManagementSystem.Controllers
{
    [ApiController]
    [Route("api/v1/ocr")]
    public class OcrController : ControllerBase
    {
        private readonly IOcrAIProducerService _ocrProducerService;
        private readonly IMemoryCache _cache;
        private readonly IHttpClientFactory _httpClientFactory;

        public OcrController(
            IOcrAIProducerService ocrProducerService,
            IMemoryCache cache,
            IHttpClientFactory httpClientFactory)
        {
            _ocrProducerService = ocrProducerService;
            _cache = cache;
            _httpClientFactory = httpClientFactory;
        }

        /// <summary>
        /// Gửi yêu cầu OCR bóc tách giấy tờ người phụ thuộc (CCCD 2 mặt, khai sinh, kết hôn, CT07, PDF)
        /// vào hàng đợi RabbitMQ không nghẽn luồng (Bất đồng bộ).
        /// </summary>
        [HttpPost("dependents", Name = "CreateDependentOcrTask")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ExtractDependentDocumentAsync([FromForm] OcrDocumentUploadRequestDto request)
        {
            if (request.File == null || request.File.Length == 0)
            {
                return BadRequest(ApiResponse<object>.Fail("INVALID_FILE", "Vui lòng chọn file ảnh giấy tờ hợp lệ."));
            }

            // 1. Lấy userId từ Claims token nếu có
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? User.FindFirst("userId")?.Value;
            Guid? userId = Guid.TryParse(userIdClaim, out var parsedId) ? parsedId : null;

            // 2. Chuyển đổi file mặt trước sang Base64
            string frontBase64;
            using (var ms = new MemoryStream())
            {
                await request.File.CopyToAsync(ms);
                frontBase64 = Convert.ToBase64String(ms.ToArray());
            }

            // 3. Chuyển đổi file mặt sau sang Base64 (nếu có)
            string? backBase64 = null;
            if (request.BackFile != null && request.BackFile.Length > 0)
            {
                using (var msBack = new MemoryStream())
                {
                    await request.BackFile.CopyToAsync(msBack);
                    backBase64 = Convert.ToBase64String(msBack.ToArray());
                }
            }

            var taskId = Guid.NewGuid();

            // 4. Đóng gói message hợp đồng chuẩn của RabbitMQ
            var message = new OcrExtractRequestMessage
            {
                TaskId = taskId,
                UserId = userId,
                FileName = request.File.FileName,
                FileBase64 = frontBase64,
                BackFileBase64 = backBase64
            };

            // 5. Bắn vào hàng đợi ocr.ai.request.queue
            _ocrProducerService.PublishOcrTask(message);

            var responseData = new
            {
                taskId = taskId,
                hubUrl = "/hubs/tax-ai",
                signalRGroup = $"task_{taskId}",
                listenEvent = "OnOcrExtractionCompleted",
                checkStatusUrl = $"/api/v1/ocr/tasks/{taskId}",
                message = "Yêu cầu bóc tách OCR đã được gửi vào hàng đợi xử lý ngầm thành công."
            };

            return StatusCode(StatusCodes.Status202Accepted,
                ApiResponse<object>.Ok(responseData, "Yêu cầu đã được tiếp nhận vào hàng đợi."));
        }

        /// <summary>
        /// Lấy kết quả bóc tách OCR theo TaskId từ Cache (Dành cho FE polling hoặc kiểm tra kết quả)
        /// </summary>
        [HttpGet("tasks/{taskId:guid}", Name = "GetOcrTaskResult")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult GetOcrTaskResult([FromRoute] Guid taskId)
        {
            if (_cache.TryGetValue($"ocr_task_{taskId}", out OcrExtractResponseMessage? result) && result != null)
            {
                return Ok(ApiResponse<OcrExtractResponseMessage>.Ok(result, "Lấy kết quả bóc tách thành công."));
            }

            return NotFound(ApiResponse<object>.Fail("TASK_NOT_FOUND_OR_PROCESSING", 
                "Tác vụ đang được AI xử lý trong hàng đợi hoặc đã hết hạn lưu trữ (30 phút). Vui lòng thử lại sau vài giây."));
        }

        /// <summary>
        /// Bóc tách OCR trực tiếp đồng bộ (Synchronous) - Gọi thẳng sang AI Service và nhận kết quả ngay lập tức
        /// Dùng khi người dùng muốn nhận dữ liệu tức thì trên cùng 1 request mà không qua Queue.
        /// </summary>
        [HttpPost("direct-extractions", Name = "ExtractDocumentDirectSync")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ExtractDocumentSync([FromForm] OcrDocumentUploadRequestDto request)
        {
            if (request.File == null || request.File.Length == 0)
            {
                return BadRequest(ApiResponse<object>.Fail("INVALID_FILE", "Vui lòng chọn file ảnh giấy tờ hợp lệ."));
            }

            try
            {
                var client = _httpClientFactory.CreateClient("TaxAIService");
                using var content = new MultipartFormDataContent();

                // Thêm file mặt trước
                using var frontStream = request.File.OpenReadStream();
                var frontContent = new StreamContent(frontStream);
                frontContent.Headers.ContentType = new MediaTypeHeaderValue(request.File.ContentType);
                content.Add(frontContent, "file", request.File.FileName);

                // Thêm file mặt sau nếu có
                StreamContent? backContent = null;
                Stream? backStream = null;
                if (request.BackFile != null && request.BackFile.Length > 0)
                {
                    backStream = request.BackFile.OpenReadStream();
                    backContent = new StreamContent(backStream);
                    backContent.Headers.ContentType = new MediaTypeHeaderValue(request.BackFile.ContentType);
                    content.Add(backContent, "back_file", request.BackFile.FileName);
                }

                var response = await client.PostAsync("/api/ocr/extract", content);
                var responseJson = await response.Content.ReadAsStringAsync();

                backStream?.Dispose();

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, 
                        ApiResponse<object>.Fail("AI_SERVICE_ERROR", $"AI Service trả về lỗi: {responseJson}"));
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var ocrResult = JsonSerializer.Deserialize<OcrExtractResponseMessage>(responseJson, options);

                return Ok(ApiResponse<OcrExtractResponseMessage>.Ok(ocrResult, "Trích xuất OCR thành công."));
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Fail("INTERNAL_SERVER_ERROR", $"Lỗi khi kết nối AI Service: {ex.Message}"));
            }
        }
    }
}
