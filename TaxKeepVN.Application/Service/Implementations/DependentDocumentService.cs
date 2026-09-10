using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

        public DependentDocumentService(IUnitOfWork unitOfWork, IFileStorageService fileStorageService)
        {
            _unitOfWork = unitOfWork;
            _fileStorageService = fileStorageService;
        }

        public async Task<DependentDocument> UploadDocumentAsync(Guid userId, Guid dependentId, string docTypeString, IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                throw new BadRequestException("FILE_MISSING", "Vui lòng chọn tệp chứng từ cần tải lên.");
            }

            // Validate Size <= 10MB
            if (file.Length > 10 * 1024 * 1024)
            {
                throw new BadRequestException("FILE_SIZE_EXCEEDED", "Dung lượng tệp vượt quá giới hạn cho phép (tối đa 10MB).");
            }

            // Validate Extension
            var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                throw new BadRequestException("INVALID_FILE_TYPE", "Định dạng tệp không hợp lệ. Hệ thống chỉ hỗ trợ PDF, JPG, JPEG, PNG.");
            }

            // Validate DocType
            if (!Enum.TryParse<DocumentType>(docTypeString, true, out var docType))
            {
                throw new BadRequestException("INVALID_DOC_TYPE", "Loại giấy tờ minh chứng không hợp lệ theo quy định thuế.");
            }

            // Get Dependent
            var dependentRepo = _unitOfWork.Repository<Dependent>();
            var dependent = await dependentRepo.GetByIdAsync(dependentId);
            
            if (dependent == null)
            {
                throw new NotFoundException("Không tìm thấy hồ sơ người phụ thuộc tương ứng.");
            }

            // Verify Ownership
            if (dependent.TaxpayerId != userId)
            {
                throw new ForbiddenException("Bạn không có quyền cập nhật hồ sơ người phụ thuộc của người khác.");
            }

            try 
            {
                // Upload File
                var fileUrl = await _fileStorageService.SaveFileAsync(file, "documents");

                // Save DB Record
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

                return document;
            }
            catch (Exception)
            {
                // In a real app, log the error
                throw new Exception("Lưu trữ tệp thất bại do sự cố hệ thống. Vui lòng thử lại."); 
                // Global middleware can catch Exception and return 500 STORAGE_UPLOAD_FAILED
            }
        }
    }
}
