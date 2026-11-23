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

        private static bool IsParentRelationship(Dependent dependent)
        {
            return dependent.Relationship == DependentRelationship.PARENT ||
                   dependent.CurrentGroup == DependentGroup.PARENT_DISABLED ||
                   dependent.CurrentGroup == DependentGroup.PARENT_RETIRED;
        }

        private static bool IsSpouseRelationship(Dependent dependent)
        {
            return dependent.Relationship == DependentRelationship.SPOUSE ||
                   dependent.CurrentGroup == DependentGroup.SPOUSE_DISABLED ||
                   dependent.CurrentGroup == DependentGroup.SPOUSE_RETIRED;
        }

        private static bool IsChildRelationship(Dependent dependent)
        {
            return dependent.Relationship == DependentRelationship.CHILD ||
                   dependent.CurrentGroup == DependentGroup.CHILD_UNDER_18 ||
                   dependent.CurrentGroup == DependentGroup.CHILD_OVER_18_DISABLED ||
                   dependent.CurrentGroup == DependentGroup.CHILD_OVER_18_STUDYING;
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
                isTypeMatched = string.Equals(normDocType, normAiMatched, StringComparison.OrdinalIgnoreCase);
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

            // Giấy tờ chứng minh quan hệ (không áp dụng bước so khớp CitizenId và năm sinh trực tiếp với NPT):
            // - Giấy kết hôn (MARRIAGE_CERTIFICATE)
            // - Giấy tờ cư trú / hộ tịch (RELATIONSHIP_CERTIFICATE: CT07, Sổ hộ tịch...)
            // - Bản cam kết nuôi dưỡng (SUPPORT_COMMITMENT_FORM)
            // - Giấy khai sinh của NNT khi người phụ thuộc là Cha/Mẹ (BIRTH_CERTIFICATE && PARENT)
            bool isRelationshipDoc = normDocType == "MARRIAGE_CERTIFICATE"
                                  || normDocType == "RELATIONSHIP_CERTIFICATE"
                                  || normDocType == "SUPPORT_COMMITMENT_FORM"
                                  || (normDocType == "BIRTH_CERTIFICATE" && isParent);

            // 3. Với giấy tờ nhân thân trực tiếp (CCCD, Thẻ SV, Giấy khuyết tật, Khai sinh của con):
            if (!isRelationshipDoc)
            {
                // Đọc không ra tên thì trả IDENTITY_UNREADABLE thay vì bỏ qua
                if (string.IsNullOrWhiteSpace(ocrData.FullName))
                {
                    throw new BadRequestException("IDENTITY_UNREADABLE",
                        "Không thể nhận diện được họ và tên trên giấy tờ nhân thân của người phụ thuộc.");
                }

                // So khớp Họ và tên với NPT
                bool isNameMatched = StringComparisonHelper.IsNameMatching(dependent.FullName, ocrData.FullName);
                if (!isNameMatched)
                {
                    throw new BadRequestException("IDENTITY_MISMATCH", 
                        $"Họ và tên trên giấy tờ ('{ocrData.FullName}') không trùng khớp với hồ sơ người phụ thuộc ('{dependent.FullName}').");
                }

                // So khớp số CCCD nếu cả 2 bên đều có
                if (!string.IsNullOrWhiteSpace(dependent.CitizenId) && !string.IsNullOrWhiteSpace(ocrData.CitizenId))
                {
                    if (dependent.CitizenId.Trim() != ocrData.CitizenId.Trim())
                    {
                        throw new BadRequestException("IDENTITY_MISMATCH", 
                            $"Số CCCD trên giấy tờ ('{MaskCitizenId(ocrData.CitizenId)}') không khớp với số CCCD người phụ thuộc ('{MaskCitizenId(dependent.CitizenId)}').");
                    }
                }

                // So khớp ngày sinh đầy đủ với người phụ thuộc theo định dạng yyyy-MM-dd
                if (!string.IsNullOrWhiteSpace(ocrData.BirthDate))
                {
                    if (DateTime.TryParseExact(ocrData.BirthDate.Trim(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var exactBirthDate))
                    {
                        if (dependent.BirthDate.Date != exactBirthDate.Date)
                        {
                            throw new BadRequestException("IDENTITY_MISMATCH", 
                                $"Ngày sinh trên giấy tờ ({exactBirthDate:dd/MM/yyyy}) không khớp với ngày sinh đã đăng ký ({dependent.BirthDate:dd/MM/yyyy}).");
                        }
                    }
                    else if (DateTime.TryParse(ocrData.BirthDate, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsedBirthDate))
                    {
                        if (dependent.BirthDate.Date != parsedBirthDate.Date)
                        {
                            throw new BadRequestException("IDENTITY_MISMATCH", 
                                $"Ngày sinh trên giấy tờ ({parsedBirthDate:dd/MM/yyyy}) không khớp với ngày sinh đã đăng ký ({dependent.BirthDate:dd/MM/yyyy}).");
                        }
                    }
                }

                // Khai sinh của con (CHILD): Kiểm tra thêm tên cha/mẹ khớp với người nộp thuế
                if (normDocType == "BIRTH_CERTIFICATE" && taxpayer != null)
                {
                    bool hasParentInfo = !string.IsNullOrWhiteSpace(ocrData.FatherFullName) || !string.IsNullOrWhiteSpace(ocrData.MotherFullName);
                    if (hasParentInfo)
                    {
                        bool isFatherNameMatched = !string.IsNullOrWhiteSpace(ocrData.FatherFullName) &&
                            StringComparisonHelper.IsNameMatching(taxpayer.FullName, ocrData.FatherFullName);

                        bool isMotherNameMatched = !string.IsNullOrWhiteSpace(ocrData.MotherFullName) &&
                            StringComparisonHelper.IsNameMatching(taxpayer.FullName, ocrData.MotherFullName);

                        if (!isFatherNameMatched && !isMotherNameMatched)
                        {
                            throw new BadRequestException("RELATIONSHIP_MISMATCH",
                                $"Họ và tên người nộp thuế ('{taxpayer.FullName}') không trùng khớp với thông tin cha ('{ocrData.FatherFullName}') hoặc mẹ ('{ocrData.MotherFullName}') trên giấy khai sinh của con.");
                        }

                        if (isFatherNameMatched && !string.IsNullOrWhiteSpace(ocrData.FatherIdNumber) && !string.IsNullOrWhiteSpace(taxpayer.CitizenId))
                        {
                            if (ocrData.FatherIdNumber.Trim() != taxpayer.CitizenId.Trim())
                            {
                                throw new BadRequestException("RELATIONSHIP_MISMATCH",
                                    $"Số định danh của cha trên giấy khai sinh ('{MaskCitizenId(ocrData.FatherIdNumber)}') không khớp với CCCD của người nộp thuế ('{MaskCitizenId(taxpayer.CitizenId)}').");
                            }
                        }

                        if (isMotherNameMatched && !string.IsNullOrWhiteSpace(ocrData.MotherIdNumber) && !string.IsNullOrWhiteSpace(taxpayer.CitizenId))
                        {
                            if (ocrData.MotherIdNumber.Trim() != taxpayer.CitizenId.Trim())
                            {
                                throw new BadRequestException("RELATIONSHIP_MISMATCH",
                                    $"Số định danh của mẹ trên giấy khai sinh ('{MaskCitizenId(ocrData.MotherIdNumber)}') không khớp với CCCD của người nộp thuế ('{MaskCitizenId(taxpayer.CitizenId)}').");
                            }
                        }
                    }
                }
            }
            // 4. Với giấy chứng minh quan hệ (KHÔNG áp dụng bước so CitizenId và năm sinh trực tiếp với NPT):
            else
            {
                // ── A. Khai sinh của NNT (người phụ thuộc là cha/mẹ) ──
                if (normDocType == "BIRTH_CERTIFICATE" && isParent)
                {
                    if (taxpayer != null)
                    {
                        // FullName trên giấy khớp người nộp thuế
                        if (!string.IsNullOrWhiteSpace(ocrData.FullName))
                        {
                            if (!StringComparisonHelper.IsNameMatching(taxpayer.FullName, ocrData.FullName))
                            {
                                throw new BadRequestException("RELATIONSHIP_MISMATCH",
                                    $"Họ và tên chủ thể trên giấy khai sinh ('{ocrData.FullName}') không khớp với người nộp thuế ('{taxpayer.FullName}'). Để chứng minh quan hệ cha/mẹ, vui lòng tải lên giấy khai sinh của người nộp thuế.");
                            }
                        }

                        // FatherFullName hoặc MotherFullName khớp người phụ thuộc
                        bool hasParentInfo = !string.IsNullOrWhiteSpace(ocrData.FatherFullName) || !string.IsNullOrWhiteSpace(ocrData.MotherFullName);
                        if (hasParentInfo)
                        {
                            bool isFatherMatched = !string.IsNullOrWhiteSpace(ocrData.FatherFullName) &&
                                StringComparisonHelper.IsNameMatching(dependent.FullName, ocrData.FatherFullName);

                            bool isMotherMatched = !string.IsNullOrWhiteSpace(ocrData.MotherFullName) &&
                                StringComparisonHelper.IsNameMatching(dependent.FullName, ocrData.MotherFullName);

                            if (!isFatherMatched && !isMotherMatched)
                            {
                                throw new BadRequestException("RELATIONSHIP_MISMATCH",
                                    $"Họ và tên người phụ thuộc ('{dependent.FullName}') không trùng khớp với tên cha ('{ocrData.FatherFullName}') hoặc mẹ ('{ocrData.MotherFullName}') trên giấy khai sinh của người nộp thuế.");
                            }

                            // Nếu có số định danh thì so thêm với CCCD của người phụ thuộc
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
                    }
                }
                // ── B. Giấy kết hôn (người phụ thuộc là vợ/chồng): cặp tên trên giấy phải là {NNT, người phụ thuộc} ──
                else if (normDocType == "MARRIAGE_CERTIFICATE" && isSpouse)
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
                    }
                }
                // ── C. Giấy kết hôn (người phụ thuộc là cha/mẹ vợ hoặc chồng): giấy kết hôn chỉ chứng minh NNT có vợ/chồng ──
                else if (normDocType == "MARRIAGE_CERTIFICATE" && isParent)
                {
                    if (taxpayer != null)
                    {
                        // Giấy kết hôn nhánh này chỉ chứng minh NNT có vợ/chồng. Cần NNT có mặt trên giấy kết hôn.
                        // Cần thêm khai sinh của vợ/chồng để nối quan hệ, nên nhánh này xử lý riêng.
                        bool hasTaxpayer = StringComparisonHelper.IsNameMatching(taxpayer.FullName, ocrData.FullName ?? "")
                                        || StringComparisonHelper.IsNameMatching(taxpayer.FullName, ocrData.SpouseFullName ?? "");

                        if (!hasTaxpayer)
                        {
                            throw new BadRequestException("RELATIONSHIP_MISMATCH",
                                $"Giấy chứng nhận kết hôn phải có thông tin của người nộp thuế ('{taxpayer.FullName}') để xác nhận quan hệ hôn nhân với con của người phụ thuộc.");
                        }
                    }
                }
                // ── D. Giấy tờ chứng minh quan hệ khác (RELATIONSHIP_CERTIFICATE: CT07, Sổ hộ tịch...) ──
                else if (normDocType == "RELATIONSHIP_CERTIFICATE")
                {
                    if (taxpayer != null)
                    {
                        if (isParent)
                        {
                            bool hasTaxpayer = StringComparisonHelper.IsNameMatching(taxpayer.FullName, ocrData.FullName ?? "");
                            bool hasDependent = (!string.IsNullOrWhiteSpace(ocrData.FatherFullName) && StringComparisonHelper.IsNameMatching(dependent.FullName, ocrData.FatherFullName))
                                             || (!string.IsNullOrWhiteSpace(ocrData.MotherFullName) && StringComparisonHelper.IsNameMatching(dependent.FullName, ocrData.MotherFullName));

                            if (!hasTaxpayer || !hasDependent)
                            {
                                // Fallback: kiểm tra xem cả NNT và NPT có cùng xuất hiện trên văn bản/hộ tịch hay không
                                bool anyTaxpayer = StringComparisonHelper.IsNameMatching(taxpayer.FullName, ocrData.FullName ?? "")
                                                || StringComparisonHelper.IsNameMatching(taxpayer.FullName, ocrData.FatherFullName ?? "")
                                                || StringComparisonHelper.IsNameMatching(taxpayer.FullName, ocrData.MotherFullName ?? "");
                                bool anyDependent = StringComparisonHelper.IsNameMatching(dependent.FullName, ocrData.FullName ?? "")
                                                 || StringComparisonHelper.IsNameMatching(dependent.FullName, ocrData.FatherFullName ?? "")
                                                 || StringComparisonHelper.IsNameMatching(dependent.FullName, ocrData.MotherFullName ?? "");

                                if (!anyTaxpayer || !anyDependent)
                                {
                                    throw new BadRequestException("RELATIONSHIP_MISMATCH",
                                        $"Giấy tờ chứng minh quan hệ ('{ocrData.FullName}') không xác thực được mối quan hệ giữa người nộp thuế ('{taxpayer.FullName}') và người phụ thuộc ('{dependent.FullName}').");
                                }
                            }
                        }
                        else if (isSpouse)
                        {
                            bool hasTaxpayer = StringComparisonHelper.IsNameMatching(taxpayer.FullName, ocrData.FullName ?? "")
                                            || StringComparisonHelper.IsNameMatching(taxpayer.FullName, ocrData.SpouseFullName ?? "");
                            bool hasDependent = StringComparisonHelper.IsNameMatching(dependent.FullName, ocrData.FullName ?? "")
                                             || StringComparisonHelper.IsNameMatching(dependent.FullName, ocrData.SpouseFullName ?? "");

                            if (!hasTaxpayer || !hasDependent)
                            {
                                throw new BadRequestException("RELATIONSHIP_MISMATCH",
                                    $"Giấy tờ chứng minh quan hệ không xác thực được mối quan hệ vợ/chồng giữa người nộp thuế ('{taxpayer.FullName}') và người phụ thuộc ('{dependent.FullName}').");
                            }
                        }
                    }
                }
                // ── E. Bản cam kết nghĩa vụ nuôi dưỡng (SUPPORT_COMMITMENT_FORM) ──
                else if (normDocType == "SUPPORT_COMMITMENT_FORM")
                {
                    if (taxpayer != null)
                    {
                        bool hasDependent = StringComparisonHelper.IsNameMatching(dependent.FullName, ocrData.FullName ?? "");
                        if (!hasDependent && !string.IsNullOrWhiteSpace(ocrData.FullName))
                        {
                            bool hasTaxpayer = StringComparisonHelper.IsNameMatching(taxpayer.FullName, ocrData.FullName);
                            if (!hasTaxpayer)
                            {
                                throw new BadRequestException("RELATIONSHIP_MISMATCH",
                                    $"Bản cam kết nuôi dưỡng ('{ocrData.FullName}') không chứa thông tin người phụ thuộc ('{dependent.FullName}') hoặc người nộp thuế ('{taxpayer.FullName}').");
                            }
                        }
                    }
                }
            }
        }

        public Task ValidateDependentDocumentAsync(Dependent dependent, DocumentType expectedType, IFormFile file, User? taxpayer = null)
        {
            return ValidateDependentDocumentAsync(dependent, expectedType.ToString(), file, taxpayer);
        }
    }
}
