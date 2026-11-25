using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TaxKeepVN.Application.Constants;
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
        private readonly IDependentRuleService _dependentRuleService;

        public DependentDocumentService(
            IUnitOfWork unitOfWork, 
            IFileStorageService fileStorageService, 
            IMemoryCache cache,
            IOcrService ocrService,
            IRedisCacheService? redisCache = null,
            ILogger<DependentDocumentService>? logger = null,
            ISubscriptionQuotaService? quotaService = null,
            IDependentRuleService? dependentRuleService = null)
        {
            _unitOfWork = unitOfWork;
            _fileStorageService = fileStorageService;
            _cache = cache;
            _ocrService = ocrService;
            _redisCache = redisCache;
            _logger = logger;
            _quotaService = quotaService;
            _dependentRuleService = dependentRuleService ?? new DependentRuleService(_unitOfWork, _cache, new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(), _redisCache);
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
                throw new BadRequestException(ErrorCodes.Dependent.RulesNotConfigured, 
                    $"Nhóm '{dependent.CurrentGroup}' chưa được cấu hình danh mục giấy tờ.");
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

        private async Task<List<DependentDocumentRule>?> GetActiveRulesForGroupAsync(string group)
        {
            return await _dependentRuleService.GetActiveRulesForGroupAsync(group);
        }

        private async Task<(bool isComplete, List<string> missing)> CheckProfileCompletionAsync(string group, List<string> uploadedDocs)
        {
            return await _dependentRuleService.ComputeMissingDocumentsAsync(group, uploadedDocs);
        }
    }
}
