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
        private readonly IDependentCatalogService? _catalogService;

        public OcrService(
            IHttpClientFactory httpClientFactory,
            IUnitOfWork unitOfWork,
            ILogger<OcrService> logger,
            IDependentCatalogService? catalogService = null)
        {
            _httpClientFactory = httpClientFactory;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _catalogService = catalogService;
        }

        public async Task<ExtractedDependentDataDto> ExtractDocumentAsync(
            IFormFile file, 
            IFormFile? backFile = null, 
            string? targetGroup = null, 
            string? rulesJson = null)
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

                if (!string.IsNullOrWhiteSpace(targetGroup))
                {
                    content.Add(new StringContent(targetGroup), "target_group");
                }

                if (!string.IsNullOrWhiteSpace(rulesJson))
                {
                    content.Add(new StringContent(rulesJson), "rules_json");
                }

                HttpResponseMessage response;
                try
                {
                    response = await client.PostAsync("/api/ocr/extract", content);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi kết nối tới AI Service OCR");
                    throw new BadGatewayException("AI_SERVICE_UNAVAILABLE", "Dịch vụ AI thẩm định giấy tờ tạm thời không khả dụng. Vui lòng thử lại sau.");
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                backStream?.Dispose();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("AI Service OCR failed with code {StatusCode}: {ResponseJson}", 
                        response.StatusCode, responseJson);
                    throw new BadGatewayException("AI_SERVICE_UNAVAILABLE", "Dịch vụ AI thẩm định giấy tờ tạm thời không khả dụng. Vui lòng thử lại sau.");
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var ocrResponse = JsonSerializer.Deserialize<OcrExtractResponseMessage>(responseJson, options);

                if (ocrResponse == null || !ocrResponse.Success || ocrResponse.Data == null)
                {
                    var msg = ocrResponse?.Message ?? "Không thể trích xuất dữ liệu từ hình ảnh.";
                    _logger.LogWarning("AI Service OCR extraction failed: {Msg}", msg);
                    throw new BadGatewayException("AI_SERVICE_UNAVAILABLE", "Dịch vụ AI thẩm định giấy tờ tạm thời không khả dụng. Vui lòng thử lại sau.");
                }

                return ocrResponse.Data;
            }
            catch (BadGatewayException)
            {
                throw;
            }
            catch (BadRequestException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối tới AI Service OCR");
                throw new BadGatewayException("AI_SERVICE_UNAVAILABLE", "Dịch vụ AI thẩm định giấy tờ tạm thời không khả dụng. Vui lòng thử lại sau.");
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
            string? suggestedGroup = data.SuggestedGroup;

            if (DateTime.TryParse(data.BirthDate, out var bDate))
            {
                var age = DateTime.Today.Year - bDate.Year;
                if (bDate > DateTime.Today.AddYears(-age)) age--;

                if (age < 18)
                {
                    suggestedRel = "CHILD";
                }
                else if (age >= 60)
                {
                    suggestedRel = "PARENT";
                }
                else
                {
                    suggestedRel = "CHILD";
                }

                List<DependentGroupCatalog>? activeGroups = null;
                if (_catalogService != null)
                {
                    activeGroups = await _catalogService.GetActiveGroupsAsync(suggestedRel);
                }
                else
                {
                    var groupRepo = _unitOfWork.Repository<DependentGroupCatalog>();
                    if (groupRepo != null)
                    {
                        activeGroups = (await groupRepo.FindAsync(g => g.IsActive && g.RelationshipCode == suggestedRel))
                            .OrderBy(g => g.DisplayOrder).ToList();
                    }
                }

                if (activeGroups != null && activeGroups.Count > 0)
                {
                    var matchedGroup = activeGroups
                        .Where(g => (!g.MinAge.HasValue || age >= g.MinAge.Value) && (!g.MaxAge.HasValue || age < g.MaxAge.Value))
                        .OrderBy(g => g.RequireDisabled)
                        .ThenBy(g => g.RequireStudying)
                        .ThenBy(g => g.DisplayOrder)
                        .FirstOrDefault();

                    if (matchedGroup != null)
                    {
                        suggestedGroup = matchedGroup.Code;
                    }
                }
            }

            suggestedGroup ??= data.SuggestedGroup ?? string.Empty;

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

        private static bool IsParentRelationship(Dependent dependent)
        {
            return string.Equals(dependent.Relationship, "PARENT", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSpouseRelationship(Dependent dependent)
        {
            return string.Equals(dependent.Relationship, "SPOUSE", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsChildRelationship(Dependent dependent)
        {
            return string.Equals(dependent.Relationship, "CHILD", StringComparison.OrdinalIgnoreCase);
        }

        private static string MaskCitizenId(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return string.Empty;
            var trimmed = id.Trim();
            if (trimmed.Length <= 6) return trimmed;
            var prefix = trimmed.Substring(0, Math.Min(4, trimmed.Length));
            var suffix = trimmed.Length > 8 ? trimmed.Substring(trimmed.Length - 4) : trimmed.Substring(trimmed.Length - 2);
            return $"{prefix}****{suffix}";
        }

        private static DateTime? ParseEffectiveFrom(string? effectiveFromMonth)
        {
            if (string.IsNullOrWhiteSpace(effectiveFromMonth)) return null;
            var parts = effectiveFromMonth.Trim().Split(new[] { '-', '/' });
            if (parts.Length >= 2 && int.TryParse(parts[0], out var year) && int.TryParse(parts[1], out var month))
            {
                if (month >= 1 && month <= 12)
                    return new DateTime(year, month, 1);
            }
            if (DateTime.TryParse(effectiveFromMonth, out var dt))
                return new DateTime(dt.Year, dt.Month, 1);

            return null;
        }

        private static DateTime? ParseDateOnly(string? dateStr)
        {
            if (string.IsNullOrWhiteSpace(dateStr)) return null;
            var trimmed = dateStr.Trim();
            if (DateTime.TryParseExact(trimmed, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var exactDt))
                return exactDt.Date;
            if (DateTime.TryParseExact(trimmed, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dmyDt))
                return dmyDt.Date;
            if (DateTime.TryParse(trimmed, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsedDt))
                return parsedDt.Date;

            return null;
        }

        private async Task<T> GetRuleParamAsync<T>(string paramName, T defaultValue)
        {
            try
            {
                var repo = _unitOfWork.Repository<SystemConfig>();
                if (repo == null) return defaultValue;

                var snakeUpper = paramName.ToUpperInvariant();
                var prefixed = $"DEPENDENT_{snakeUpper}";

                var configs = await repo.FindAsync(c => c.IsActive && 
                    (c.ConfigKey.Equals(paramName, StringComparison.OrdinalIgnoreCase) ||
                     c.ConfigKey.Equals(snakeUpper, StringComparison.OrdinalIgnoreCase) ||
                     c.ConfigKey.Equals(prefixed, StringComparison.OrdinalIgnoreCase)));

                var config = configs.FirstOrDefault();
                if (config == null || string.IsNullOrWhiteSpace(config.ConfigValue))
                {
                    return defaultValue;
                }

                var valStr = config.ConfigValue.Trim();

                if (typeof(T) == typeof(bool))
                {
                    if (bool.TryParse(valStr, out var bVal))
                        return (T)(object)bVal;
                    if (valStr == "1") return (T)(object)true;
                    if (valStr == "0") return (T)(object)false;
                }
                else if (typeof(T) == typeof(int))
                {
                    if (int.TryParse(valStr, out var iVal))
                        return (T)(object)iVal;
                }
                else if (typeof(T) == typeof(double))
                {
                    if (double.TryParse(valStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var dVal))
                        return (T)(object)dVal;
                }
                else if (typeof(T) == typeof(string))
                {
                    return (T)(object)valStr;
                }

                return defaultValue;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi đọc cấu hình tham số rule '{ParamName}' từ DB, sử dụng giá trị mặc định", paramName);
                return defaultValue;
            }
        }

        public async Task ValidateDependentDocumentAsync(
            Dependent dependent, 
            string docType, 
            IFormFile file, 
            User? taxpayer = null, 
            string? targetGroup = null, 
            string? rulesJson = null)
        {
            targetGroup ??= dependent.CurrentGroup.ToString();
            var ocrData = await ExtractDocumentAsync(file, null, targetGroup, rulesJson);

            // 1. Kiểm tra độ rõ nét của ảnh và ngưỡng tin cậy (Từ chối khi isPassedThreshold = false)
            if (!ocrData.IsReadable)
            {
                throw new BadRequestException("UNREADABLE_DOCUMENT", 
                    $"Ảnh giấy tờ không đạt chuẩn chất lượng (quá mờ hoặc lóa sáng): {ocrData.UnreadableReason}");
            }

            if (ocrData.ThresholdValidation?.IsPassedThreshold == false || ocrData.IsPassedThreshold == false)
            {
                var warning = ocrData.ThresholdValidation?.WarningMessage 
                    ?? "Ảnh giấy tờ không đạt ngưỡng tin cậy cho phép (các trường cốt lõi bị mờ hoặc không rõ nét). Vui lòng chụp lại ảnh rõ nét hơn.";
                throw new BadRequestException("LOW_CONFIDENCE_DOCUMENT", warning);
            }

            // 2. Kiểm tra Đúng Loại Giấy Tờ (DocType Matching)
            var normDocType = DependentRuleService.NormalizeDocType(docType);
            bool isTypeMatched;
            if (ocrData.RuleValidation != null && !string.IsNullOrWhiteSpace(ocrData.RuleValidation.MatchedDocType))
            {
                var normAiMatched = DependentRuleService.NormalizeDocType(ocrData.RuleValidation.MatchedDocType);
                isTypeMatched = StringComparisonHelper.IsDocTypeMatching(normDocType, normAiMatched);
            }
            else
            {
                isTypeMatched = StringComparisonHelper.IsDocTypeMatching(normDocType, ocrData.DocumentType);
            }

            if (!isTypeMatched)
            {
                var detectedType = ocrData.RuleValidation?.MatchedDocType ?? ocrData.DocumentType;
                throw new BadRequestException("DOC_TYPE_MISMATCH", 
                    $"Loại giấy tờ tải lên không khớp. Bạn đang nộp minh chứng '{docType}' nhưng ảnh là '{detectedType}'.");
            }

            // Lấy thông tin Người nộp thuế (Taxpayer) nếu chưa truyền vào
            if (taxpayer == null && dependent.TaxpayerId != Guid.Empty)
            {
                var userRepo = _unitOfWork.Repository<User>();
                if (userRepo != null)
                {
                    taxpayer = await userRepo.GetByIdAsync(dependent.TaxpayerId);
                }
            }

            bool isParent = IsParentRelationship(dependent);
            bool isSpouse = IsSpouseRelationship(dependent);
            bool isChild = IsChildRelationship(dependent);

            // Xác định rule key theo đặc tả 11 loại tài liệu:
            string ruleKey = normDocType;
            if (normDocType == "BIRTH_CERTIFICATE")
            {
                ruleKey = isParent ? "TAXPAYER_BIRTH_CERTIFICATE" : "CHILD_BIRTH_CERTIFICATE";
            }
            else if (normDocType == "MARRIAGE_CERTIFICATE")
            {
                ruleKey = isParent ? "TAXPAYER_MARRIAGE_CERTIFICATE" : "MARRIAGE_CERTIFICATE";
            }

            // ── 1. Key: CITIZEN_ID (CCCD/CMND của NPT) ──────────────────────────────────
            if (ruleKey == "CITIZEN_ID")
            {
                var allowLegacy9Digits = await GetRuleParamAsync("allowLegacy9Digits", false);

                if (string.IsNullOrWhiteSpace(ocrData.FullName))
                {
                    throw new BadRequestException("IDENTITY_UNREADABLE",
                        "Không thể nhận diện được họ và tên trên CCCD/CMND của người phụ thuộc.");
                }

                if (!StringComparisonHelper.IsNameMatching(dependent.FullName, ocrData.FullName))
                {
                    throw new BadRequestException("IDENTITY_MISMATCH",
                        $"Họ và tên trên giấy tờ ('{ocrData.FullName}') không trùng khớp với hồ sơ người phụ thuộc ('{dependent.FullName}').");
                }

                var idNum = ocrData.CitizenId?.Trim();
                if (string.IsNullOrEmpty(idNum))
                {
                    throw new BadRequestException("IDENTITY_UNREADABLE",
                        "Không thể nhận diện được số CCCD/CMND trên giấy tờ.");
                }

                if (!allowLegacy9Digits && idNum.Length != 12)
                {
                    throw new BadRequestException("IDENTITY_MISMATCH",
                        $"Số giấy tờ ('{MaskCitizenId(idNum)}') không phải là CCCD 12 số hợp lệ.");
                }
                else if (allowLegacy9Digits && idNum.Length != 12 && idNum.Length != 9)
                {
                    throw new BadRequestException("IDENTITY_MISMATCH",
                        $"Số giấy tờ ('{MaskCitizenId(idNum)}') không phải là CCCD (12 số) hoặc CMND (9 số) hợp lệ.");
                }

                if (!string.IsNullOrWhiteSpace(dependent.CitizenId))
                {
                    if (dependent.CitizenId.Trim() != idNum)
                    {
                        throw new BadRequestException("IDENTITY_MISMATCH",
                            $"Số CCCD trên giấy tờ ('{MaskCitizenId(idNum)}') không khớp với số CCCD người phụ thuộc ('{MaskCitizenId(dependent.CitizenId)}').");
                    }
                }

                var parsedBirth = ParseDateOnly(ocrData.BirthDate);
                if (parsedBirth.HasValue && dependent.BirthDate.Date != parsedBirth.Value)
                {
                    throw new BadRequestException("IDENTITY_MISMATCH",
                        $"Ngày sinh trên giấy tờ ({parsedBirth.Value:dd/MM/yyyy}) không khớp với ngày sinh đã đăng ký ({dependent.BirthDate:dd/MM/yyyy}).");
                }

                var effectiveFrom = ParseEffectiveFrom(dependent.EffectiveFromMonth);
                var expiry = ParseDateOnly(ocrData.ExpiryDate);
                if (expiry.HasValue && effectiveFrom.HasValue && expiry.Value < effectiveFrom.Value)
                {
                    throw new BadRequestException("DOCUMENT_EXPIRED",
                        $"CCCD/CMND đã hết hạn ({expiry.Value:dd/MM/yyyy}) trước thời điểm bắt đầu tính giảm trừ gia cảnh ({effectiveFrom.Value:dd/MM/yyyy}).");
                }
            }
            // ── 2. Key: CHILD_BIRTH_CERTIFICATE (Khai sinh của NPT là con) ─────────────
            else if (ruleKey == "CHILD_BIRTH_CERTIFICATE")
            {
                var requireParentMatch = await GetRuleParamAsync("requireParentMatch", true);

                if (string.IsNullOrWhiteSpace(ocrData.FullName))
                {
                    throw new BadRequestException("IDENTITY_UNREADABLE",
                        "Không thể nhận diện được họ và tên người con trên giấy khai sinh.");
                }

                if (!StringComparisonHelper.IsNameMatching(dependent.FullName, ocrData.FullName))
                {
                    throw new BadRequestException("IDENTITY_MISMATCH",
                        $"Họ và tên con trên giấy khai sinh ('{ocrData.FullName}') không trùng khớp với hồ sơ người phụ thuộc ('{dependent.FullName}').");
                }

                var parsedBirth = ParseDateOnly(ocrData.BirthDate);
                if (parsedBirth.HasValue && dependent.BirthDate.Date != parsedBirth.Value)
                {
                    throw new BadRequestException("IDENTITY_MISMATCH",
                        $"Ngày sinh trên giấy khai sinh ({parsedBirth.Value:dd/MM/yyyy}) không khớp với ngày sinh đã đăng ký ({dependent.BirthDate:dd/MM/yyyy}).");
                }

                var depBirthCert = dependent.BirthCertNumber?.Trim();
                var ocrDocNum = ocrData.DocumentNumber?.Trim();
                var ocrCitId = ocrData.CitizenId?.Trim();

                if (!string.IsNullOrWhiteSpace(depBirthCert) && (!string.IsNullOrWhiteSpace(ocrDocNum) || !string.IsNullOrWhiteSpace(ocrCitId)))
                {
                    bool isMatched = (!string.IsNullOrWhiteSpace(ocrDocNum) && string.Equals(depBirthCert, ocrDocNum, StringComparison.OrdinalIgnoreCase)) ||
                                     (!string.IsNullOrWhiteSpace(ocrCitId) && string.Equals(depBirthCert, ocrCitId, StringComparison.OrdinalIgnoreCase));

                    if (!isMatched)
                    {
                        var displayDoc = !string.IsNullOrWhiteSpace(ocrDocNum) ? ocrDocNum : ocrCitId;
                        throw new BadRequestException("IDENTITY_MISMATCH",
                            $"Số giấy khai sinh trên giấy tờ ('{displayDoc}') không khớp với số khai sinh đã đăng ký ('{depBirthCert}').");
                    }
                }

                if (requireParentMatch && taxpayer != null)
                {
                    bool isFatherMatched = !string.IsNullOrWhiteSpace(ocrData.FatherFullName) &&
                        StringComparisonHelper.IsNameMatching(taxpayer.FullName, ocrData.FatherFullName);

                    bool isMotherMatched = !string.IsNullOrWhiteSpace(ocrData.MotherFullName) &&
                        StringComparisonHelper.IsNameMatching(taxpayer.FullName, ocrData.MotherFullName);

                    if (!isFatherMatched && !isMotherMatched)
                    {
                        throw new BadRequestException("RELATIONSHIP_MISMATCH",
                            $"Họ và tên người nộp thuế ('{taxpayer.FullName}') không trùng khớp với thông tin cha ('{ocrData.FatherFullName}') hoặc mẹ ('{ocrData.MotherFullName}') trên giấy khai sinh của con.");
                    }

                    if (isFatherMatched && !string.IsNullOrWhiteSpace(ocrData.FatherIdNumber) && !string.IsNullOrWhiteSpace(taxpayer.CitizenId))
                    {
                        if (ocrData.FatherIdNumber.Trim() != taxpayer.CitizenId.Trim())
                        {
                            throw new BadRequestException("RELATIONSHIP_MISMATCH",
                                $"Số định danh của cha trên giấy khai sinh ('{MaskCitizenId(ocrData.FatherIdNumber)}') không khớp với CCCD của người nộp thuế ('{MaskCitizenId(taxpayer.CitizenId)}').");
                        }
                    }

                    if (isMotherMatched && !string.IsNullOrWhiteSpace(ocrData.MotherIdNumber) && !string.IsNullOrWhiteSpace(taxpayer.CitizenId))
                    {
                        if (ocrData.MotherIdNumber.Trim() != taxpayer.CitizenId.Trim())
                        {
                            throw new BadRequestException("RELATIONSHIP_MISMATCH",
                                $"Số định danh của mẹ trên giấy khai sinh ('{MaskCitizenId(ocrData.MotherIdNumber)}') không khớp với CCCD của người nộp thuế ('{MaskCitizenId(taxpayer.CitizenId)}').");
                        }
                    }
                }
            }
            // ── 3. Key: TAXPAYER_BIRTH_CERTIFICATE (Khai sinh của NNT khi NPT là cha/mẹ) ─
            else if (ruleKey == "TAXPAYER_BIRTH_CERTIFICATE")
            {
                if (taxpayer != null)
                {
                    if (!string.IsNullOrWhiteSpace(ocrData.FullName))
                    {
                        if (!StringComparisonHelper.IsNameMatching(taxpayer.FullName, ocrData.FullName))
                        {
                            throw new BadRequestException("RELATIONSHIP_MISMATCH",
                                $"Họ và tên chủ thể trên giấy khai sinh ('{ocrData.FullName}') không khớp với người nộp thuế ('{taxpayer.FullName}'). Để chứng minh quan hệ cha/mẹ, vui lòng tải lên giấy khai sinh của người nộp thuế.");
                        }
                    }

                    if (taxpayer.DateOfBirth.HasValue && !string.IsNullOrWhiteSpace(ocrData.BirthDate))
                    {
                        var parsedBirth = ParseDateOnly(ocrData.BirthDate);
                        if (parsedBirth.HasValue && taxpayer.DateOfBirth.Value.ToDateTime(TimeOnly.MinValue).Date != parsedBirth.Value)
                        {
                            throw new BadRequestException("RELATIONSHIP_MISMATCH",
                                $"Ngày sinh của người nộp thuế trên giấy khai sinh ({parsedBirth.Value:dd/MM/yyyy}) không khớp với hồ sơ cá nhân.");
                        }
                    }
                }

                bool isFatherMatched = !string.IsNullOrWhiteSpace(ocrData.FatherFullName) &&
                    StringComparisonHelper.IsNameMatching(dependent.FullName, ocrData.FatherFullName);

                bool isMotherMatched = !string.IsNullOrWhiteSpace(ocrData.MotherFullName) &&
                    StringComparisonHelper.IsNameMatching(dependent.FullName, ocrData.MotherFullName);

                if (!isFatherMatched && !isMotherMatched)
                {
                    throw new BadRequestException("RELATIONSHIP_MISMATCH",
                        $"Họ và tên người phụ thuộc ('{dependent.FullName}') không trùng khớp với tên cha ('{ocrData.FatherFullName}') hoặc mẹ ('{ocrData.MotherFullName}') trên giấy khai sinh của người nộp thuế.");
                }

                if (isFatherMatched && !string.IsNullOrWhiteSpace(ocrData.FatherIdNumber) && !string.IsNullOrWhiteSpace(dependent.CitizenId))
                {
                    if (ocrData.FatherIdNumber.Trim() != dependent.CitizenId.Trim())
                    {
                        throw new BadRequestException("RELATIONSHIP_MISMATCH",
                            $"Số định danh của cha trên giấy khai sinh ('{MaskCitizenId(ocrData.FatherIdNumber)}') không khớp với số CCCD người phụ thuộc ('{MaskCitizenId(dependent.CitizenId)}').");
                    }
                }

                if (isMotherMatched && !string.IsNullOrWhiteSpace(ocrData.MotherIdNumber) && !string.IsNullOrWhiteSpace(dependent.CitizenId))
                {
                    if (ocrData.MotherIdNumber.Trim() != dependent.CitizenId.Trim())
                    {
                        throw new BadRequestException("RELATIONSHIP_MISMATCH",
                            $"Số định danh của mẹ trên giấy khai sinh ('{MaskCitizenId(ocrData.MotherIdNumber)}') không khớp với số CCCD người phụ thuộc ('{MaskCitizenId(dependent.CitizenId)}').");
                    }
                }
            }
            // ── 4. Key: STUDENT_CARD (Thẻ SV / giấy xác nhận trường) ───────────────────
            else if (ruleKey == "STUDENT_CARD")
            {
                var requireSchoolName = await GetRuleParamAsync("requireSchoolName", true);

                if (string.IsNullOrWhiteSpace(ocrData.FullName))
                {
                    throw new BadRequestException("IDENTITY_UNREADABLE",
                        "Không thể nhận diện được họ và tên trên thẻ học sinh/sinh viên.");
                }

                if (!StringComparisonHelper.IsNameMatching(dependent.FullName, ocrData.FullName))
                {
                    throw new BadRequestException("IDENTITY_MISMATCH",
                        $"Họ và tên trên thẻ sinh viên ('{ocrData.FullName}') không trùng khớp với hồ sơ người phụ thuộc ('{dependent.FullName}').");
                }

                var parsedBirth = ParseDateOnly(ocrData.BirthDate);
                if (parsedBirth.HasValue && dependent.BirthDate.Date != parsedBirth.Value)
                {
                    throw new BadRequestException("IDENTITY_MISMATCH",
                        $"Ngày sinh trên thẻ sinh viên ({parsedBirth.Value:dd/MM/yyyy}) không khớp với ngày sinh đã đăng ký ({dependent.BirthDate:dd/MM/yyyy}).");
                }

                if (requireSchoolName)
                {
                    var school = ocrData.SchoolName ?? ocrData.IssuingAuthority;
                    if (string.IsNullOrWhiteSpace(school))
                    {
                        throw new BadRequestException("INVALID_DOCUMENT_DATA",
                            "Không nhận diện được tên trường học hoặc cơ sở đào tạo trên thẻ học sinh/sinh viên hoặc giấy xác nhận.");
                    }
                }

                var effectiveFrom = ParseEffectiveFrom(dependent.EffectiveFromMonth);
                var validTo = ParseDateOnly(ocrData.ValidTo ?? ocrData.ExpiryDate);
                if (validTo.HasValue && effectiveFrom.HasValue && validTo.Value < effectiveFrom.Value)
                {
                    throw new BadRequestException("DOCUMENT_EXPIRED",
                        $"Thẻ học sinh/sinh viên hoặc giấy xác nhận trường đã hết hạn ({validTo.Value:dd/MM/yyyy}) trước thời điểm bắt đầu tính giảm trừ gia cảnh ({effectiveFrom.Value:dd/MM/yyyy}).");
                }
            }
            // ── 5. Key: DISABILITY_CERTIFICATE (Giấy xác nhận khuyết tật / KNLĐ) ──────
            else if (ruleKey == "DISABILITY_CERTIFICATE")
            {
                var minLaborLoss = await GetRuleParamAsync("minLaborLossPercent", 61.0);
                var acceptedLevelsRaw = await GetRuleParamAsync("acceptedLevels", "ĐẶC BIỆT NẶNG,NẶNG,DAC BIET NANG,NANG,SEVERE,EXTREMELY_SEVERE");
                var acceptedList = acceptedLevelsRaw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (string.IsNullOrWhiteSpace(ocrData.FullName))
                {
                    throw new BadRequestException("IDENTITY_UNREADABLE",
                        "Không thể nhận diện được họ và tên trên giấy xác nhận khuyết tật / giám định y khoa.");
                }

                if (!StringComparisonHelper.IsNameMatching(dependent.FullName, ocrData.FullName))
                {
                    throw new BadRequestException("IDENTITY_MISMATCH",
                        $"Họ và tên trên giấy tờ ('{ocrData.FullName}') không trùng khớp với hồ sơ người phụ thuộc ('{dependent.FullName}').");
                }

                bool cccdMatched = !string.IsNullOrWhiteSpace(dependent.CitizenId) && 
                                   !string.IsNullOrWhiteSpace(ocrData.CitizenId) && 
                                   dependent.CitizenId.Trim() == ocrData.CitizenId.Trim();

                var parsedBirth = ParseDateOnly(ocrData.BirthDate);
                bool dobMatched = parsedBirth.HasValue && dependent.BirthDate.Date == parsedBirth.Value;

                bool hasIdOrDob = !string.IsNullOrWhiteSpace(ocrData.CitizenId) || parsedBirth.HasValue;
                if (hasIdOrDob && !cccdMatched && !dobMatched)
                {
                    throw new BadRequestException("IDENTITY_MISMATCH",
                        "Số CCCD hoặc ngày sinh trên giấy xác nhận khuyết tật không trùng khớp với thông tin người phụ thuộc.");
                }

                bool isLevelAccepted = false;
                if (!string.IsNullOrWhiteSpace(ocrData.DisabilityLevel))
                {
                    var cleanLevel = StringComparisonHelper.RemoveDiacritics(ocrData.DisabilityLevel).ToUpperInvariant();
                    isLevelAccepted = acceptedList.Any(l => cleanLevel.Contains(StringComparisonHelper.RemoveDiacritics(l).ToUpperInvariant()));
                }

                bool isLossAccepted = ocrData.LaborCapacityLossPercent.HasValue && ocrData.LaborCapacityLossPercent.Value >= minLaborLoss;

                if (!isLevelAccepted && !isLossAccepted)
                {
                    throw new BadRequestException("DISABILITY_NOT_QUALIFIED",
                        $"Mức độ khuyết tật ('{ocrData.DisabilityLevel}') hoặc tỷ lệ suy giảm KNLĐ ({ocrData.LaborCapacityLossPercent}%) không đủ điều kiện theo quy định (yêu cầu thuộc [{string.Join(", ", acceptedList)}] hoặc suy giảm từ {minLaborLoss}% trở lên).");
                }
            }
            // ── 6. Key: MARRIAGE_CERTIFICATE (Giấy kết hôn - NPT vợ/chồng) ─────────────
            else if (ruleKey == "MARRIAGE_CERTIFICATE")
            {
                if (taxpayer != null)
                {
                    bool case1 = StringComparisonHelper.IsNameMatching(taxpayer.FullName, ocrData.FullName ?? "")
                              && StringComparisonHelper.IsNameMatching(dependent.FullName, ocrData.SpouseFullName ?? "");

                    bool case2 = StringComparisonHelper.IsNameMatching(dependent.FullName, ocrData.FullName ?? "")
                              && StringComparisonHelper.IsNameMatching(taxpayer.FullName, ocrData.SpouseFullName ?? "");

                    if (!case1 && !case2)
                    {
                        throw new BadRequestException("RELATIONSHIP_MISMATCH",
                            $"Cặp tên vợ/chồng trên giấy chứng nhận kết hôn ('{ocrData.FullName}' - '{ocrData.SpouseFullName}') không khớp với người nộp thuế ('{taxpayer.FullName}') và người phụ thuộc ('{dependent.FullName}').");
                    }

                    if (case1)
                    {
                        if (!string.IsNullOrWhiteSpace(ocrData.CitizenId) && !string.IsNullOrWhiteSpace(taxpayer.CitizenId) && ocrData.CitizenId.Trim() != taxpayer.CitizenId.Trim())
                        {
                            throw new BadRequestException("RELATIONSHIP_MISMATCH",
                                $"Số định danh của người nộp thuế trên giấy kết hôn ('{MaskCitizenId(ocrData.CitizenId)}') không khớp với CCCD đã đăng ký ('{MaskCitizenId(taxpayer.CitizenId)}').");
                        }
                        if (!string.IsNullOrWhiteSpace(ocrData.SpouseIdNumber) && !string.IsNullOrWhiteSpace(dependent.CitizenId) && ocrData.SpouseIdNumber.Trim() != dependent.CitizenId.Trim())
                        {
                            throw new BadRequestException("RELATIONSHIP_MISMATCH",
                                $"Số định danh của người phụ thuộc trên giấy kết hôn ('{MaskCitizenId(ocrData.SpouseIdNumber)}') không khớp với CCCD đã đăng ký ('{MaskCitizenId(dependent.CitizenId)}').");
                        }
                    }
                    else if (case2)
                    {
                        if (!string.IsNullOrWhiteSpace(ocrData.CitizenId) && !string.IsNullOrWhiteSpace(dependent.CitizenId) && ocrData.CitizenId.Trim() != dependent.CitizenId.Trim())
                        {
                            throw new BadRequestException("RELATIONSHIP_MISMATCH",
                                $"Số định danh của người phụ thuộc trên giấy kết hôn ('{MaskCitizenId(ocrData.CitizenId)}') không khớp với CCCD đã đăng ký ('{MaskCitizenId(dependent.CitizenId)}').");
                        }
                        if (!string.IsNullOrWhiteSpace(ocrData.SpouseIdNumber) && !string.IsNullOrWhiteSpace(taxpayer.CitizenId) && ocrData.SpouseIdNumber.Trim() != taxpayer.CitizenId.Trim())
                        {
                            throw new BadRequestException("RELATIONSHIP_MISMATCH",
                                $"Số định danh của người nộp thuế trên giấy kết hôn ('{MaskCitizenId(ocrData.SpouseIdNumber)}') không khớp với CCCD đã đăng ký ('{MaskCitizenId(taxpayer.CitizenId)}').");
                        }
                    }
                }

                var effectiveFrom = ParseEffectiveFrom(dependent.EffectiveFromMonth);
                var issue = ParseDateOnly(ocrData.IssueDate);
                if (issue.HasValue && effectiveFrom.HasValue)
                {
                    var endOfMonth = new DateTime(effectiveFrom.Value.Year, effectiveFrom.Value.Month, DateTime.DaysInMonth(effectiveFrom.Value.Year, effectiveFrom.Value.Month));
                    if (issue.Value > endOfMonth)
                    {
                        throw new BadRequestException("MARRIAGE_AFTER_EFFECTIVE",
                            $"Ngày đăng ký kết hôn ({issue.Value:dd/MM/yyyy}) sau thời điểm bắt đầu tính giảm trừ gia cảnh ({endOfMonth:dd/MM/yyyy}).");
                    }
                }
            }
            // ── 7. Key: TAXPAYER_MARRIAGE_CERTIFICATE (Giấy kết hôn NNT - NPT là cha mẹ vợ/chồng) ─
            else if (ruleKey == "TAXPAYER_MARRIAGE_CERTIFICATE")
            {
                if (taxpayer != null)
                {
                    bool hasTaxpayer = StringComparisonHelper.IsNameMatching(taxpayer.FullName, ocrData.FullName ?? "")
                                    || StringComparisonHelper.IsNameMatching(taxpayer.FullName, ocrData.SpouseFullName ?? "");

                    if (!hasTaxpayer)
                    {
                        throw new BadRequestException("RELATIONSHIP_MISMATCH",
                            $"Giấy chứng nhận kết hôn phải có thông tin của người nộp thuế ('{taxpayer.FullName}') để xác nhận quan hệ hôn nhân với con của người phụ thuộc.");
                    }

                    string? spouseName = StringComparisonHelper.IsNameMatching(taxpayer.FullName, ocrData.FullName ?? "")
                        ? ocrData.SpouseFullName
                        : ocrData.FullName;

                    _logger.LogInformation("Xác thực giấy kết hôn của NNT '{Taxpayer}', người phối ngẫu: '{Spouse}'",
                        taxpayer.FullName, spouseName);
                }
            }
            // ── 8. Key: HOUSEHOLD_CERTIFICATE (CT07 / sổ hộ tịch) ─────────────────────
            else if (ruleKey == "HOUSEHOLD_CERTIFICATE" || ruleKey == "RELATIONSHIP_CERTIFICATE")
            {
                var maxIssueAgeDays = await GetRuleParamAsync("maxIssueAgeDays", 365);

                if (taxpayer != null)
                {
                    bool taxpayerFound = false;
                    bool dependentFound = false;

                    if (ocrData.HouseholdMembers != null && ocrData.HouseholdMembers.Count > 0)
                    {
                        foreach (var m in ocrData.HouseholdMembers)
                        {
                            if (!taxpayerFound && StringComparisonHelper.IsNameMatching(taxpayer.FullName, m.FullName ?? ""))
                            {
                                if (string.IsNullOrWhiteSpace(taxpayer.CitizenId) || string.IsNullOrWhiteSpace(m.CitizenId) || taxpayer.CitizenId.Trim() == m.CitizenId.Trim())
                                {
                                    taxpayerFound = true;
                                }
                            }

                            if (!dependentFound && StringComparisonHelper.IsNameMatching(dependent.FullName, m.FullName ?? ""))
                            {
                                if (string.IsNullOrWhiteSpace(dependent.CitizenId) || string.IsNullOrWhiteSpace(m.CitizenId) || dependent.CitizenId.Trim() == m.CitizenId.Trim())
                                {
                                    dependentFound = true;
                                }
                            }
                        }
                    }

                    if (!taxpayerFound)
                    {
                        taxpayerFound = StringComparisonHelper.IsNameMatching(taxpayer.FullName, ocrData.FullName ?? "")
                                     || StringComparisonHelper.IsNameMatching(taxpayer.FullName, ocrData.FatherFullName ?? "")
                                     || StringComparisonHelper.IsNameMatching(taxpayer.FullName, ocrData.MotherFullName ?? "")
                                     || StringComparisonHelper.IsNameMatching(taxpayer.FullName, ocrData.SpouseFullName ?? "");
                    }

                    if (!dependentFound)
                    {
                        dependentFound = StringComparisonHelper.IsNameMatching(dependent.FullName, ocrData.FullName ?? "")
                                      || StringComparisonHelper.IsNameMatching(dependent.FullName, ocrData.FatherFullName ?? "")
                                      || StringComparisonHelper.IsNameMatching(dependent.FullName, ocrData.MotherFullName ?? "")
                                      || StringComparisonHelper.IsNameMatching(dependent.FullName, ocrData.SpouseFullName ?? "");
                    }

                    if (!taxpayerFound || !dependentFound)
                    {
                        throw new BadRequestException("RELATIONSHIP_MISMATCH",
                            $"Giấy tờ cư trú / hộ tịch (CT07) phải chứa thông tin của cả người nộp thuế ('{taxpayer.FullName}') và người phụ thuộc ('{dependent.FullName}').");
                    }
                }

                if (maxIssueAgeDays > 0)
                {
                    var issue = ParseDateOnly(ocrData.IssueDate);
                    if (issue.HasValue)
                    {
                        var ageInDays = (DateTime.UtcNow.Date - issue.Value).TotalDays;
                        if (ageInDays > maxIssueAgeDays)
                        {
                            throw new BadRequestException("DOCUMENT_EXPIRED",
                                $"Giấy xác nhận cư trú (CT07) được cấp ngày {issue.Value:dd/MM/yyyy}, đã quá thời hạn hiệu lực quy định ({maxIssueAgeDays} ngày).");
                        }
                    }
                }
            }
            // ── 9. Key: ADOPTION_CERTIFICATE (Giấy CN / QĐ công nhận nuôi con nuôi) ───
            else if (ruleKey == "ADOPTION_CERTIFICATE")
            {
                if (taxpayer != null)
                {
                    var parentAdopt = ocrData.AdoptiveParentFullName ?? ocrData.FatherFullName ?? ocrData.FullName ?? "";
                    var childAdopt = ocrData.AdoptiveChildFullName ?? ocrData.FullName ?? "";

                    if (isParent)
                    {
                        bool isAdoptiveParentMatch = StringComparisonHelper.IsNameMatching(dependent.FullName, parentAdopt);
                        bool isAdoptedChildMatch = StringComparisonHelper.IsNameMatching(taxpayer.FullName, childAdopt);

                        if (!isAdoptiveParentMatch || !isAdoptedChildMatch)
                        {
                            throw new BadRequestException("RELATIONSHIP_MISMATCH",
                                $"Giấy nhận nuôi con nuôi không xác thực được người phụ thuộc ('{dependent.FullName}') là người nhận nuôi người nộp thuế ('{taxpayer.FullName}').");
                        }
                    }
                    else
                    {
                        bool isAdoptiveParentMatch = StringComparisonHelper.IsNameMatching(taxpayer.FullName, parentAdopt);
                        bool isAdoptedChildMatch = StringComparisonHelper.IsNameMatching(dependent.FullName, childAdopt);

                        if (!isAdoptiveParentMatch || !isAdoptedChildMatch)
                        {
                            throw new BadRequestException("RELATIONSHIP_MISMATCH",
                                $"Giấy nhận nuôi con nuôi không xác thực được người nộp thuế ('{taxpayer.FullName}') là người nhận nuôi người phụ thuộc ('{dependent.FullName}').");
                        }
                    }
                }
            }
            // ── 10. Key: SUPPORT_COMMITMENT (Bản cam kết nuôi dưỡng) ───────────────────
            else if (ruleKey == "SUPPORT_COMMITMENT" || ruleKey == "SUPPORT_COMMITMENT_FORM")
            {
                var requireStamp = await GetRuleParamAsync("requireStamp", true);

                if (taxpayer != null)
                {
                    bool hasTaxpayer = StringComparisonHelper.IsNameMatching(taxpayer.FullName, ocrData.FullName ?? "")
                                    || StringComparisonHelper.IsNameMatching(taxpayer.FullName, ocrData.FatherFullName ?? "")
                                    || StringComparisonHelper.IsNameMatching(taxpayer.FullName, ocrData.MotherFullName ?? "")
                                    || StringComparisonHelper.IsNameMatching(taxpayer.FullName, ocrData.SpouseFullName ?? "");

                    bool hasDependent = StringComparisonHelper.IsNameMatching(dependent.FullName, ocrData.FullName ?? "")
                                     || StringComparisonHelper.IsNameMatching(dependent.FullName, ocrData.FatherFullName ?? "")
                                     || StringComparisonHelper.IsNameMatching(dependent.FullName, ocrData.MotherFullName ?? "")
                                     || StringComparisonHelper.IsNameMatching(dependent.FullName, ocrData.SpouseFullName ?? "");

                    if (!hasTaxpayer || !hasDependent)
                    {
                        throw new BadRequestException("RELATIONSHIP_MISMATCH",
                            $"Bản cam kết nuôi dưỡng phải có đầy đủ thông tin của cả người nộp thuế ('{taxpayer.FullName}') và người phụ thuộc ('{dependent.FullName}').");
                    }
                }

                if (ocrData.HasSignature == false)
                {
                    throw new BadRequestException("INVALID_DOCUMENT_DATA",
                        "Bản cam kết nuôi dưỡng chưa có chữ ký xác nhận của các bên.");
                }

                if (requireStamp && ocrData.HasStamp == false)
                {
                    throw new BadRequestException("COMMITMENT_NOT_CERTIFIED",
                        "Bản cam kết nuôi dưỡng chưa có con dấu xác nhận của Ủy ban nhân dân cấp xã/phường.");
                }
            }
            // ── 11. Key: OTHER (Giấy khác) ───────────────────────────────────────────
            else if (ruleKey == "OTHER")
            {
                // Chỉ bước chung: đọc được (IsReadable), đạt ngưỡng (IsPassedThreshold), đúng loại (isTypeMatched)
            }
        }

        public Task ValidateDependentDocumentAsync(Dependent dependent, DocumentType expectedType, IFormFile file, User? taxpayer = null)
        {
            return ValidateDependentDocumentAsync(dependent, expectedType.ToString(), file, taxpayer);
        }
    }
}
