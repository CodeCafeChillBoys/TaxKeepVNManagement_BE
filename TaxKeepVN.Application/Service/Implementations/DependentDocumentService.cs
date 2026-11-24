using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    public class DependentDocumentService : IDependentDocumentService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _fileStorageService;
        private readonly IMemoryCache _cache;
        private readonly IOcrService _ocrService;
        private readonly IRedisCacheService? _redisCache;
        private readonly ILogger<DependentDocumentService>? _logger;
        private readonly ISubscriptionQuotaService? _quotaService;

        // Cấu hình các nhóm OR (giấy tờ có thể thay thế nhau) theo từng nhóm NPT
        private static readonly Dictionary<DependentGroup, List<List<string>>> OrDocumentRequirements = new()
        {
            [DependentGroup.CHILD_UNDER_18] = new()
            {
                new List<string> { "BIRTH_CERTIFICATE", "CITIZEN_ID" }
            },
            [DependentGroup.PARENT_DISABLED] = new()
            {
                new List<string> { "RELATIONSHIP_CERTIFICATE", "BIRTH_CERTIFICATE" }
            },
            [DependentGroup.PARENT_RETIRED] = new()
            {
                new List<string> { "RELATIONSHIP_CERTIFICATE", "BIRTH_CERTIFICATE" }
            }
        };

        // Fallback danh sách giấy tờ hợp lệ khi nhóm chưa có rule cấu hình trong DB
        private static readonly Dictionary<DependentGroup, List<string>> FallbackGroupAllowedDocTypes = new()
        {
            [DependentGroup.CHILD_UNDER_18] = new() { "BIRTH_CERTIFICATE", "CITIZEN_ID" },
            [DependentGroup.CHILD_OVER_18_DISABLED] = new() { "CITIZEN_ID", "DISABILITY_CERTIFICATE" },
            [DependentGroup.CHILD_OVER_18_STUDYING] = new() { "CITIZEN_ID", "STUDENT_CARD" },
            [DependentGroup.SPOUSE_DISABLED] = new() { "CITIZEN_ID", "MARRIAGE_CERTIFICATE", "DISABILITY_CERTIFICATE" },
            [DependentGroup.SPOUSE_RETIRED] = new() { "CITIZEN_ID", "MARRIAGE_CERTIFICATE" },
            [DependentGroup.PARENT_DISABLED] = new() { "CITIZEN_ID", "RELATIONSHIP_CERTIFICATE", "BIRTH_CERTIFICATE", "DISABILITY_CERTIFICATE" },
            [DependentGroup.PARENT_RETIRED] = new() { "CITIZEN_ID", "RELATIONSHIP_CERTIFICATE", "BIRTH_CERTIFICATE" },
            [DependentGroup.OTHER_HELPLESS] = new() { "CITIZEN_ID", "RELATIONSHIP_CERTIFICATE", "SUPPORT_COMMITMENT_FORM" }
        };

        public DependentDocumentService(
            IUnitOfWork unitOfWork, 
            IFileStorageService fileStorageService, 
            IMemoryCache cache,
            IOcrService ocrService,
            IRedisCacheService? redisCache = null,
            ILogger<DependentDocumentService>? logger = null,
            ISubscriptionQuotaService? quotaService = null)
        {
            _unitOfWork = unitOfWork;
            _fileStorageService = fileStorageService;
            _cache = cache;
            _ocrService = ocrService;
            _redisCache = redisCache;
            _logger = logger;
            _quotaService = quotaService;
        }

        public async Task<UploadDocumentResultDto> UploadDocumentAsync(Guid userId, Guid dependentId, string docTypeString, IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new BadRequestException("FILE_MISSING", "Vui lòng chọn tệp chứng từ cần tải lên.");

            if (file.Length > 10 * 1024 * 1024)
                throw new BadRequestException("FILE_SIZE_EXCEEDED", "Dung lượng tệp vượt quá giới hạn cho phép (tối đa 10MB).");

            var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
                throw new BadRequestException("INVALID_FILE_TYPE", "Định dạng tệp không hợp lệ. Hệ thống chỉ hỗ trợ PDF, JPG, JPEG, PNG.");

            if (string.IsNullOrWhiteSpace(docTypeString))
                throw new BadRequestException("DOC_TYPE_MISSING", "Loại giấy tờ không được để trống.");

            var normalizedDocType = DependentRuleService.NormalizeDocType(docTypeString);

            var dependentRepo = _unitOfWork.Repository<Dependent>();
            var dependent = await dependentRepo.GetByIdAsync(dependentId);
            
            if (dependent == null)
                throw new NotFoundException("Không tìm thấy hồ sơ người phụ thuộc tương ứng.");

            if (dependent.TaxpayerId != userId)
                throw new ForbiddenException("Bạn không có quyền cập nhật hồ sơ người phụ thuộc của người khác.");

            if (dependent.Status == DependentStatus.INACTIVE)
                throw new ConflictException("DEPENDENT_INACTIVE", "Người phụ thuộc đã ngừng hoạt động (INACTIVE). Không thể tải lên thêm giấy tờ minh chứng.");

            // ── Lấy danh sách rule active của nhóm dependent.CurrentGroup ──
            var activeRules = await GetActiveRulesForGroupAsync(dependent.CurrentGroup);

            if (activeRules != null && activeRules.Count > 0)
            {
                var matchingRule = activeRules.FirstOrDefault(r => 
                    r.DocType.Equals(normalizedDocType, StringComparison.OrdinalIgnoreCase));

                if (matchingRule == null)
                {
                    var allRuleRepo = _unitOfWork.Repository<DependentDocumentRule>();
                    var anyRule = (await allRuleRepo.FindAsync(r => r.DocType == normalizedDocType && !string.IsNullOrEmpty(r.DocName))).FirstOrDefault();
                    var displayName = anyRule?.DocName ?? (!string.IsNullOrWhiteSpace(docTypeString) ? docTypeString : normalizedDocType);
                    throw new BadRequestException("DOC_TYPE_NOT_REQUIRED_FOR_GROUP", 
                        $"Loại giấy tờ '{displayName}' không thuộc danh mục minh chứng của nhóm này.");
                }
            }
            else
            {
                _logger?.LogWarning("Nhóm '{Group}' chưa cấu hình rule trong database, sử dụng danh sách fallback.", dependent.CurrentGroup);

                if (FallbackGroupAllowedDocTypes.TryGetValue(dependent.CurrentGroup, out var allowedList))
                {
                    if (!allowedList.Contains(normalizedDocType, StringComparer.OrdinalIgnoreCase))
                    {
                        throw new BadRequestException("DOC_TYPE_NOT_REQUIRED_FOR_GROUP", 
                            $"Loại giấy tờ '{docTypeString}' không thuộc danh mục minh chứng của nhóm này.");
                    }
                }
            }

            var userRepo = _unitOfWork.Repository<User>();
            var taxpayer = userRepo != null ? await userRepo.GetByIdAsync(userId) : null;

            // ── Kiểm tra và trừ lượt quét OCR theo gói dịch vụ ──
            if (_quotaService != null)
            {
                await _quotaService.CheckAndConsumeOcrScanAsync(userId, 1);
            }

            // ── AI OCR Cross-check Validation ──
            var rulesJson = activeRules != null && activeRules.Count > 0
                ? JsonSerializer.Serialize(activeRules.Select(r => new
                {
                    doc_type = r.DocType,
                    doc_name = r.DocName,
                    is_mandatory = r.IsMandatory,
                    description = r.Description
                }))
                : null;

            await _ocrService.ValidateDependentDocumentAsync(
                dependent, 
                normalizedDocType, 
                file, 
                taxpayer, 
                dependent.CurrentGroup.ToString(), 
                rulesJson);

            var fileUrl = await _fileStorageService.SaveFileAsync(file, "documents");

            var docRepo = _unitOfWork.Repository<DependentDocument>();
            var existingDocs = (await docRepo.FindAsync(d => d.DependentId == dependentId && d.DocType == normalizedDocType)).ToList();

            DependentDocument document;
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                if (existingDocs.Count > 0)
                {
                    document = existingDocs[0];
                    document.FileUrl = fileUrl;
                    document.FileMimeType = file.ContentType;
                    document.IsReadable = true;
                    document.UploadedAt = DateTime.UtcNow;
                    docRepo.Update(document);

                    for (int i = 1; i < existingDocs.Count; i++)
                    {
                        docRepo.Remove(existingDocs[i]);
                    }
                }
                else
                {
                    document = new DependentDocument
                    {
                        Id = Guid.NewGuid(),
                        DependentId = dependentId,
                        DocType = normalizedDocType,
                        FileUrl = fileUrl,
                        FileMimeType = file.ContentType,
                        IsReadable = true,
                        UploadedAt = DateTime.UtcNow
                    };
                    await docRepo.AddAsync(document);
                }

                await _unitOfWork.SaveChangesAsync();

                var allDocs = await docRepo.FindAsync(d => d.DependentId == dependentId);
                var validDocs = allDocs
                    .Where(d => !dependent.IdentityUpdatedAt.HasValue || d.UploadedAt >= dependent.IdentityUpdatedAt.Value)
                    .ToList();
                var uploadedTypes = validDocs.Select(d => d.DocType).Distinct().ToList();
                
                var (isComplete, missing) = await CheckProfileCompletionAsync(dependent.CurrentGroup, uploadedTypes);

                if (dependent.Status != DependentStatus.INACTIVE)
                {
                    var newStatus = isComplete ? DependentStatus.ACTIVE : DependentStatus.PENDING_DOCUMENTS;
                    if (dependent.IsProfileComplete != isComplete || dependent.Status != newStatus)
                    {
                        dependent.IsProfileComplete = isComplete;
                        dependent.Status = newStatus;
                        dependentRepo.Update(dependent);
                        await _unitOfWork.SaveChangesAsync();
                    }
                }

                await _unitOfWork.CommitTransactionAsync();

                return new UploadDocumentResultDto
                {
                    Document = document,
                    IsProfileComplete = isComplete,
                    MissingDocuments = missing
                };
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync();
                throw;
            }
        }

        private async Task<List<DependentDocumentRule>?> GetActiveRulesForGroupAsync(DependentGroup group)
        {
            var groupString = group.ToString();
            var cacheKey = $"DependentRules_Active_{groupString}";
            var redisKey = $"taxkeep:dependent_rules:active:{groupString}";

            List<DependentDocumentRule>? activeRules = null;
            if (_redisCache != null)
            {
                activeRules = await _redisCache.GetAsync<List<DependentDocumentRule>>(redisKey);
            }

            if (activeRules == null && (!_cache.TryGetValue(cacheKey, out activeRules) || activeRules == null))
            {
                var ruleRepo = _unitOfWork.Repository<DependentDocumentRule>();
                if (ruleRepo != null)
                {
                    activeRules = (await ruleRepo.FindAsync(r => r.TargetGroup == groupString && r.IsActive)).ToList();

                    var cacheOptions = new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromHours(1));
                    _cache.Set(cacheKey, activeRules, cacheOptions);

                    if (_redisCache != null)
                    {
                        await _redisCache.SetAsync(redisKey, activeRules, TimeSpan.FromHours(24));
                    }
                }
            }

            return activeRules;
        }

        private async Task<(bool isComplete, List<string> missing)> CheckProfileCompletionAsync(DependentGroup group, List<string> uploadedDocs)
        {
            var activeRules = await GetActiveRulesForGroupAsync(group);

            if (activeRules != null && activeRules.Any())
            {
                var missing = new List<string>();
                var uploadedSet = uploadedDocs
                    .Select(DependentRuleService.NormalizeDocType)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var mandatoryRules = activeRules.Where(r => r.IsMandatory).ToList();
                var handledRuleTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (OrDocumentRequirements.TryGetValue(group, out var orGroups))
                {
                    foreach (var orList in orGroups)
                    {
                        var relevantMandatory = mandatoryRules.Where(r => orList.Contains(r.DocType, StringComparer.OrdinalIgnoreCase)).ToList();
                        if (relevantMandatory.Count > 0)
                        {
                            foreach (var r in relevantMandatory)
                            {
                                handledRuleTypes.Add(r.DocType);
                            }

                            bool hasAnyUploaded = orList.Any(doc => uploadedSet.Contains(doc));
                            if (!hasAnyUploaded)
                            {
                                if (group == DependentGroup.CHILD_UNDER_18)
                                {
                                    missing.Add("BIRTH_CERTIFICATE_OR_CITIZEN_ID");
                                }
                                else
                                {
                                    var preferred = relevantMandatory.FirstOrDefault(r => r.DocType.Equals("RELATIONSHIP_CERTIFICATE", StringComparison.OrdinalIgnoreCase))
                                                 ?? relevantMandatory.First();
                                    missing.Add(preferred.DocType);
                                }
                            }
                        }
                    }
                }

                foreach (var rule in mandatoryRules)
                {
                    if (handledRuleTypes.Contains(rule.DocType))
                        continue;

                    if (!uploadedSet.Contains(rule.DocType))
                    {
                        missing.Add(rule.DocType);
                    }
                }

                return (missing.Count == 0, missing);
            }

            return FallbackCheckProfileCompletion(group, uploadedDocs);
        }

        private static (bool isComplete, List<string> missing) FallbackCheckProfileCompletion(DependentGroup group, List<string> uploadedDocs)
        {
            var missing = new List<string>();
            var uploadedSet = uploadedDocs
                .Select(DependentRuleService.NormalizeDocType)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            switch (group)
            {
                case DependentGroup.CHILD_UNDER_18:
                    if (!uploadedSet.Contains("BIRTH_CERTIFICATE") && !uploadedSet.Contains("CITIZEN_ID"))
                        missing.Add("BIRTH_CERTIFICATE_OR_CITIZEN_ID");
                    break;
                    
                case DependentGroup.CHILD_OVER_18_DISABLED:
                    if (!uploadedSet.Contains("CITIZEN_ID")) missing.Add("CITIZEN_ID");
                    if (!uploadedSet.Contains("DISABILITY_CERTIFICATE")) missing.Add("DISABILITY_CERTIFICATE");
                    break;
                    
                case DependentGroup.CHILD_OVER_18_STUDYING:
                    if (!uploadedSet.Contains("CITIZEN_ID")) missing.Add("CITIZEN_ID");
                    if (!uploadedSet.Contains("STUDENT_CARD")) missing.Add("STUDENT_CARD");
                    break;
                    
                case DependentGroup.SPOUSE_DISABLED:
                    if (!uploadedSet.Contains("CITIZEN_ID")) missing.Add("CITIZEN_ID");
                    if (!uploadedSet.Contains("MARRIAGE_CERTIFICATE")) missing.Add("MARRIAGE_CERTIFICATE");
                    if (!uploadedSet.Contains("DISABILITY_CERTIFICATE")) missing.Add("DISABILITY_CERTIFICATE");
                    break;
                    
                case DependentGroup.SPOUSE_RETIRED:
                    if (!uploadedSet.Contains("CITIZEN_ID")) missing.Add("CITIZEN_ID");
                    if (!uploadedSet.Contains("MARRIAGE_CERTIFICATE")) missing.Add("MARRIAGE_CERTIFICATE");
                    break;
                    
                case DependentGroup.PARENT_DISABLED:
                    if (!uploadedSet.Contains("CITIZEN_ID")) missing.Add("CITIZEN_ID");
                    if (!uploadedSet.Contains("RELATIONSHIP_CERTIFICATE") && !uploadedSet.Contains("BIRTH_CERTIFICATE"))
                        missing.Add("RELATIONSHIP_CERTIFICATE");
                    if (!uploadedSet.Contains("DISABILITY_CERTIFICATE")) missing.Add("DISABILITY_CERTIFICATE");
                    break;
                    
                case DependentGroup.PARENT_RETIRED:
                    if (!uploadedSet.Contains("CITIZEN_ID")) missing.Add("CITIZEN_ID");
                    if (!uploadedSet.Contains("RELATIONSHIP_CERTIFICATE") && !uploadedSet.Contains("BIRTH_CERTIFICATE"))
                        missing.Add("RELATIONSHIP_CERTIFICATE");
                    break;
                    
                case DependentGroup.OTHER_HELPLESS:
                    if (!uploadedSet.Contains("CITIZEN_ID")) missing.Add("CITIZEN_ID");
                    if (!uploadedSet.Contains("RELATIONSHIP_CERTIFICATE")) missing.Add("RELATIONSHIP_CERTIFICATE");
                    if (!uploadedSet.Contains("SUPPORT_COMMITMENT_FORM")) missing.Add("SUPPORT_COMMITMENT_FORM");
                    break;
            }

            return (missing.Count == 0, missing);
        }
    }
}
