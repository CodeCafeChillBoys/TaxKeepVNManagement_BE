using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public DependentDocumentService(IUnitOfWork unitOfWork, IFileStorageService fileStorageService, IMemoryCache cache)
        {
            _unitOfWork = unitOfWork;
            _fileStorageService = fileStorageService;
            _cache = cache;
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

            if (!Enum.TryParse<DocumentType>(docTypeString, true, out var docType))
                throw new BadRequestException("INVALID_DOC_TYPE", "Loại giấy tờ minh chứng không hợp lệ theo quy định thuế.");

            var dependentRepo = _unitOfWork.Repository<Dependent>();
            // Include Documents to check completion later
            // Note: Since GenericRepository FindAsync might not eagerly load, we can query documents separately
            var dependent = await dependentRepo.GetByIdAsync(dependentId);
            
            if (dependent == null)
                throw new NotFoundException("Không tìm thấy hồ sơ người phụ thuộc tương ứng.");

            if (dependent.TaxpayerId != userId)
                throw new ForbiddenException("Bạn không có quyền cập nhật hồ sơ người phụ thuộc của người khác.");

            var fileUrl = await _fileStorageService.SaveFileAsync(file, "documents");

            var document = new DependentDocument
            {
                Id = Guid.NewGuid(),
                DependentId = dependentId,
                DocType = docType,
                FileUrl = fileUrl,
                FileMimeType = file.ContentType,
                IsReadable = true,
                UploadedAt = DateTime.UtcNow
            };

            await _unitOfWork.Repository<DependentDocument>().AddAsync(document);
            await _unitOfWork.SaveChangesAsync();

            // Logic: Kiểm tra đủ danh sách giấy tờ bắt buộc dựa trên dynamic rules trong DB
            var allDocs = await _unitOfWork.Repository<DependentDocument>().FindAsync(d => d.DependentId == dependentId);
            var uploadedTypes = allDocs.Select(d => d.DocType).ToList();
            
            var (isComplete, missing) = await CheckProfileCompletionAsync(dependent.CurrentGroup, uploadedTypes);

            // Update Dependent status in DB
            if (dependent.IsProfileComplete != isComplete)
            {
                dependent.IsProfileComplete = isComplete;
                dependentRepo.Update(dependent);
                await _unitOfWork.SaveChangesAsync();
            }

            return new UploadDocumentResultDto
            {
                Document = document,
                IsProfileComplete = isComplete,
                MissingDocuments = missing
            };
        }

        private async Task<(bool isComplete, List<string> missing)> CheckProfileCompletionAsync(DependentGroup group, List<DocumentType> uploadedDocs)
        {
            var groupString = group.ToString();
            var cacheKey = $"DependentRules_{groupString}";

            // Lấy từ IMemoryCache (hết hạn sau 1 tiếng) để tối ưu hiệu năng mili-giây, tránh query DB liên tục
            if (!_cache.TryGetValue(cacheKey, out List<DependentDocumentRule>? activeRules) || activeRules == null)
            {
                var ruleRepo = _unitOfWork.Repository<DependentDocumentRule>();
                activeRules = (await ruleRepo.FindAsync(r => r.TargetGroup == groupString && r.IsActive)).ToList();

                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromHours(1));

                _cache.Set(cacheKey, activeRules, cacheOptions);
            }

            if (activeRules.Any())
            {
                var missing = new List<string>();
                var uploadedNames = uploadedDocs.Select(d => d.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (group == DependentGroup.CHILD_UNDER_18)
                {
                    // Theo Thông tư 87: Con dưới 18 tuổi cần Giấy khai sinh HOẶC Căn cước công dân
                    bool hasBirthCert = uploadedNames.Contains("BIRTH_CERTIFICATE");
                    bool hasCitizenId = uploadedNames.Contains("CITIZEN_ID");
                    if (!hasBirthCert && !hasCitizenId)
                    {
                        missing.Add("BIRTH_CERTIFICATE_OR_CITIZEN_ID");
                    }

                    // Kiểm tra các giấy tờ bắt buộc khác ngoài 2 loại trên nếu có cấu hình
                    var otherMandatory = activeRules.Where(r => r.IsMandatory &&
                        !r.DocType.Equals("BIRTH_CERTIFICATE", StringComparison.OrdinalIgnoreCase) &&
                        !r.DocType.Equals("CITIZEN_ID", StringComparison.OrdinalIgnoreCase));

                    foreach (var r in otherMandatory)
                    {
                        if (!uploadedNames.Contains(r.DocType))
                            missing.Add(r.DocType);
                    }
                }
                else
                {
                    var mandatoryRules = activeRules.Where(r => r.IsMandatory);
                    foreach (var rule in mandatoryRules)
                    {
                        if (!uploadedNames.Contains(rule.DocType))
                            missing.Add(rule.DocType);
                    }
                }

                return (missing.Count == 0, missing);
            }

            // Fallback khi chưa cấu hình rule trong database
            return FallbackCheckProfileCompletion(group, uploadedDocs);
        }

        private static (bool isComplete, List<string> missing) FallbackCheckProfileCompletion(DependentGroup group, List<DocumentType> uploadedDocs)
        {
            var missing = new List<string>();

            switch (group)
            {
                case DependentGroup.CHILD_UNDER_18:
                    if (!uploadedDocs.Contains(DocumentType.BIRTH_CERTIFICATE) && !uploadedDocs.Contains(DocumentType.CITIZEN_ID))
                        missing.Add("BIRTH_CERTIFICATE_OR_CITIZEN_ID");
                    break;
                    
                case DependentGroup.CHILD_OVER_18_DISABLED:
                    if (!uploadedDocs.Contains(DocumentType.CITIZEN_ID)) missing.Add("CITIZEN_ID");
                    if (!uploadedDocs.Contains(DocumentType.DISABILITY_CERTIFICATE)) missing.Add("DISABILITY_CERTIFICATE");
                    break;
                    
                case DependentGroup.CHILD_OVER_18_STUDYING:
                    if (!uploadedDocs.Contains(DocumentType.CITIZEN_ID)) missing.Add("CITIZEN_ID");
                    if (!uploadedDocs.Contains(DocumentType.STUDENT_CARD)) missing.Add("STUDENT_CARD");
                    break;
                    
                case DependentGroup.SPOUSE_DISABLED:
                    if (!uploadedDocs.Contains(DocumentType.CITIZEN_ID)) missing.Add("CITIZEN_ID");
                    if (!uploadedDocs.Contains(DocumentType.MARRIAGE_CERTIFICATE)) missing.Add("MARRIAGE_CERTIFICATE");
                    if (!uploadedDocs.Contains(DocumentType.DISABILITY_CERTIFICATE)) missing.Add("DISABILITY_CERTIFICATE");
                    break;
                    
                case DependentGroup.SPOUSE_RETIRED:
                    if (!uploadedDocs.Contains(DocumentType.CITIZEN_ID)) missing.Add("CITIZEN_ID");
                    if (!uploadedDocs.Contains(DocumentType.MARRIAGE_CERTIFICATE)) missing.Add("MARRIAGE_CERTIFICATE");
                    break;
                    
                case DependentGroup.PARENT_DISABLED:
                    if (!uploadedDocs.Contains(DocumentType.CITIZEN_ID)) missing.Add("CITIZEN_ID");
                    if (!uploadedDocs.Contains(DocumentType.RELATIONSHIP_CERTIFICATE)) missing.Add("RELATIONSHIP_CERTIFICATE");
                    if (!uploadedDocs.Contains(DocumentType.DISABILITY_CERTIFICATE)) missing.Add("DISABILITY_CERTIFICATE");
                    break;
                    
                case DependentGroup.PARENT_RETIRED:
                    if (!uploadedDocs.Contains(DocumentType.CITIZEN_ID)) missing.Add("CITIZEN_ID");
                    if (!uploadedDocs.Contains(DocumentType.RELATIONSHIP_CERTIFICATE)) missing.Add("RELATIONSHIP_CERTIFICATE");
                    break;
                    
                case DependentGroup.OTHER_HELPLESS:
                    if (!uploadedDocs.Contains(DocumentType.CITIZEN_ID)) missing.Add("CITIZEN_ID");
                    if (!uploadedDocs.Contains(DocumentType.RELATIONSHIP_CERTIFICATE)) missing.Add("RELATIONSHIP_CERTIFICATE");
                    if (!uploadedDocs.Contains(DocumentType.SUPPORT_COMMITMENT_FORM)) missing.Add("SUPPORT_COMMITMENT_FORM");
                    break;
            }

            return (missing.Count == 0, missing);
        }
    }
}
