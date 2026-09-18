using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.OcrAI;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Helpers;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    public class OcrService : IOcrService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<OcrService> _logger;

        public OcrService(
            IHttpClientFactory httpClientFactory,
            IUnitOfWork unitOfWork,
            ILogger<OcrService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<ExtractedDependentDataDto> ExtractDocumentAsync(IFormFile file, IFormFile? backFile = null)
        {
            if (file == null || file.Length == 0)
                throw new BadRequestException("FILE_MISSING", "Vui lòng cung cấp tệp ảnh giấy tờ.");

            try
            {
                var client = _httpClientFactory.CreateClient("TaxAIService");
                using var content = new MultipartFormDataContent();

                // Mặt trước
                using var frontStream = file.OpenReadStream();
                var frontContent = new StreamContent(frontStream);
                frontContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
                content.Add(frontContent, "file", file.FileName);

                // Mặt sau (nếu có)
                StreamContent? backContent = null;
                Stream? backStream = null;
                if (backFile != null && backFile.Length > 0)
                {
                    backStream = backFile.OpenReadStream();
                    backContent = new StreamContent(backStream);
                    backContent.Headers.ContentType = new MediaTypeHeaderValue(backFile.ContentType);
                    content.Add(backContent, "back_file", backFile.FileName);
                }

                var response = await client.PostAsync("/api/ocr/extract", content);
                var responseJson = await response.Content.ReadAsStringAsync();

                backStream?.Dispose();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("AI Service OCR failed with code {StatusCode}: {ResponseJson}", 
                        response.StatusCode, responseJson);
                    throw new BadRequestException("OCR_SERVICE_ERROR", $"Dịch vụ AI bóc tách thất bại: {responseJson}");
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var ocrResponse = JsonSerializer.Deserialize<OcrExtractResponseMessage>(responseJson, options);

                if (ocrResponse == null || !ocrResponse.Success || ocrResponse.Data == null)
                {
                    var msg = ocrResponse?.Message ?? "Không thể trích xuất dữ liệu từ hình ảnh.";
                    throw new BadRequestException("OCR_EXTRACTION_FAILED", msg);
                }

                return ocrResponse.Data;
            }
            catch (BadRequestException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối tới AI Service OCR");
                throw new BadRequestException("AI_CONNECTION_ERROR", $"Không thể kết nối tới dịch vụ AI Service: {ex.Message}");
            }
        }

        public async Task<UserCccdOcrResponseDto> ProcessUserCccdOcrAsync(IFormFile file, IFormFile? backFile = null)
        {
            var data = await ExtractDocumentAsync(file, backFile);

            if (!data.IsReadable)
            {
                throw new BadRequestException("UNREADABLE_IMAGE", 
                    $"Ảnh giấy tờ không thể đọc được: {data.UnreadableReason ?? "Ảnh mờ hoặc lóa sáng"}");
            }

            var citizenId = data.CitizenId?.Trim();
            if (string.IsNullOrEmpty(citizenId) || citizenId.Length != 12)
            {
                throw new BadRequestException("INVALID_CITIZEN_ID", 
                    "Không thể nhận diện được số CCCD 12 số hợp lệ từ hình ảnh tải lên.");
            }

            // Kiểm tra số CCCD đã được đăng ký tài khoản chưa
            var existingUsers = await _unitOfWork.Repository<User>().FindAsync(u => u.CitizenId == citizenId);
            bool isAlreadyRegistered = existingUsers.Any();

            return new UserCccdOcrResponseDto
            {
                CitizenId = citizenId,
                FullName = data.FullName,
                DateOfBirth = data.BirthDate,
                Gender = data.Gender,
                Address = data.ResidencePlace ?? data.OriginPlace,
                IsAvailable = !isAlreadyRegistered,
                Warning = isAlreadyRegistered ? "Số CCCD này đã được đăng ký tài khoản trong hệ thống." : null
            };
        }

        public async Task<DependentOcrResponseDto> ProcessDependentOcrAsync(IFormFile file, IFormFile? backFile = null)
        {
            var data = await ExtractDocumentAsync(file, backFile);

            if (!data.IsReadable)
            {
                throw new BadRequestException("UNREADABLE_IMAGE", 
                    $"Ảnh giấy tờ không thể đọc được: {data.UnreadableReason ?? "Ảnh mờ hoặc lóa sáng"}");
            }

            var citizenId = data.CitizenId?.Trim();
            bool isRegistered = false;

            if (!string.IsNullOrEmpty(citizenId))
            {
                var existingDependents = await _unitOfWork.Repository<Dependent>()
                    .FindAsync(d => d.CitizenId == citizenId && !d.IsDeleted);
                isRegistered = existingDependents.Any();
            }

            // Tính toán gợi ý mối quan hệ & nhóm
            string suggestedRel = "CHILD";
            string suggestedGroup = data.SuggestedGroup ?? "CHILD_UNDER_18";

            if (DateTime.TryParse(data.BirthDate, out var bDate))
            {
                var age = DateTime.Today.Year - bDate.Year;
                if (bDate > DateTime.Today.AddYears(-age)) age--;

                if (age < 18)
                {
                    suggestedGroup = "CHILD_UNDER_18";
                    suggestedRel = "CHILD";
                }
                else if (age <= 22)
                {
                    suggestedGroup = "CHILD_OVER_18_STUDYING";
                    suggestedRel = "CHILD";
                }
                else if (age >= 60)
                {
                    suggestedGroup = "PARENT_RETIRED";
                    suggestedRel = "PARENT";
                }
            }

            return new DependentOcrResponseDto
            {
                DocumentType = data.DocumentType,
                FullName = data.FullName,
                CitizenId = citizenId,
                BirthCertNumber = data.DocumentNumber,
                BirthDate = data.BirthDate,
                Gender = data.Gender,
                SuggestedRelationship = suggestedRel,
                SuggestedGroup = suggestedGroup,
                Address = data.ResidencePlace ?? data.OriginPlace,
                IsAlreadyRegistered = isRegistered,
                Warning = isRegistered ? "Người phụ thuộc có số CCCD này đã được đăng ký trong hệ thống." : null
            };
        }

        public async Task ValidateDependentDocumentAsync(Dependent dependent, DocumentType expectedType, IFormFile file)
        {
            var ocrData = await ExtractDocumentAsync(file);

            // 1. Kiểm tra độ rõ nét của ảnh
            if (!ocrData.IsReadable)
            {
                throw new BadRequestException("UNREADABLE_DOCUMENT", 
                    $"Ảnh giấy tờ không đạt chuẩn chất lượng (quá mờ hoặc lóa sáng): {ocrData.UnreadableReason}");
            }

            // 2. Kiểm tra Đúng Loại Giấy Tờ (DocType Matching)
            bool isTypeMatched = StringComparisonHelper.IsDocTypeMatching(expectedType, ocrData.DocumentType);
            if (!isTypeMatched)
            {
                throw new BadRequestException("DOC_TYPE_MISMATCH", 
                    $"Loại giấy tờ tải lên không khớp. Bạn đang nộp minh chứng '{expectedType}' nhưng ảnh là '{ocrData.DocumentType}'.");
            }

            // 3. Kiểm tra Đúng Người Phụ Thuộc (Identity Matching)
            if (!string.IsNullOrWhiteSpace(ocrData.FullName))
            {
                bool isNameMatched = StringComparisonHelper.IsNameMatching(dependent.FullName, ocrData.FullName);
                if (!isNameMatched)
                {
                    throw new BadRequestException("IDENTITY_MISMATCH", 
                        $"Họ và tên trên giấy tờ ('{ocrData.FullName}') không trùng khớp với hồ sơ người phụ thuộc ('{dependent.FullName}').");
                }
            }

            // Kiểm tra số CCCD nếu cả 2 bên đều có
            if (!string.IsNullOrWhiteSpace(dependent.CitizenId) && !string.IsNullOrWhiteSpace(ocrData.CitizenId))
            {
                if (dependent.CitizenId.Trim() != ocrData.CitizenId.Trim())
                {
                    throw new BadRequestException("IDENTITY_MISMATCH", 
                        $"Số CCCD trên giấy tờ ('{ocrData.CitizenId}') không khớp với số CCCD người phụ thuộc ('{dependent.CitizenId}').");
                }
            }

            // Kiểm tra năm sinh nếu giấy tờ có ngày sinh
            if (DateTime.TryParse(ocrData.BirthDate, out var ocrBirthDate))
            {
                if (dependent.BirthDate.Year != ocrBirthDate.Year)
                {
                    throw new BadRequestException("IDENTITY_MISMATCH", 
                        $"Năm sinh trên giấy tờ ({ocrBirthDate.Year}) không khớp với ngày sinh đã đăng ký ({dependent.BirthDate.Year}).");
                }
            }
        }
    }
}
