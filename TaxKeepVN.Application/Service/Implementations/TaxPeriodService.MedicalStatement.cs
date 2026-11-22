using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TaxKeepVN.Application.Constants;
using TaxKeepVN.Application.DTOs.Documents;
using TaxKeepVN.Application.DTOs.TaxAI;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Mappers;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Application.Service.Implementations
{
    public partial class TaxPeriodService
    {
        /// <summary>
        /// Tải lên Bảng kê chi phí KCB (Mẫu 01/BV) cho Hóa đơn viện phí mẹ,
        /// lưu trực tiếp vào bảng medical_statements và kích hoạt AI OCR bóc tách chi tiết.
        /// </summary>
        public async Task<MedicalStatementResponseDto> UploadMedicalStatementAsync(
            Guid userId, Guid periodId, Guid documentId, IFormFile file)
        {
            // 1. Kiểm tra Tax Period
            var periodRepo = _unitOfWork.Repository<TaxPeriod>();
            var period = await periodRepo.GetByIdAsync(periodId);
            if (period == null || period.UserId != userId)
                throw new NotFoundException(ErrorMessages.TaxPeriodNotFound);

            if (string.Equals(period.Status, TaxPeriodStatus.SUBMITTED, StringComparison.OrdinalIgnoreCase))
                throw new ForbiddenException(ErrorMessages.TaxPeriodSubmitted);

            // 2. Kiểm tra file payload
            if (file == null || file.Length == 0)
                throw new BadRequestException(ErrorCodes.FilesRequired, ErrorMessages.FilesRequired);

            var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".pdf" };
            var allowedMimeTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "image/jpeg", "image/jpg", "image/pjpeg",
                "image/png", "image/x-png",
                "application/pdf"
            };

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext) || (!string.IsNullOrEmpty(file.ContentType) && !allowedMimeTypes.Contains(file.ContentType)))
                throw new BadRequestException(ErrorCodes.UnsupportedFormat, ErrorMessages.UnsupportedFormat);

            if (file.Length > 10 * 1024 * 1024)
                throw new BadRequestException(ErrorCodes.FileSizeExceeded, ErrorMessages.FileSizeExceeded);

            // 3. Kiểm tra Hóa đơn viện phí mẹ (Document mẹ)
            var documents = _unitOfWork.Repository<Document>();
            var receipt = await documents.GetByIdAsync(documentId);
            if (receipt == null || receipt.PeriodId != periodId)
                throw new NotFoundException(ErrorMessages.DocumentNotFound);

            if (string.Equals(receipt.Status, "CONFIRMED", StringComparison.OrdinalIgnoreCase))
                throw new BadRequestException(ErrorCodes.InvalidDocumentStatus, ErrorMessages.DocumentAlreadyVerified);

            var types = _unitOfWork.Repository<TaxDocumentType>();
            var receiptType = (await types.FindAsync(t => t.Code == receipt.DocTypeCode &&
                t.Code == DocumentTypeCodes.MedicalReceipt)).SingleOrDefault();
            if (receiptType == null)
                throw new BadRequestException("ERR_NOT_MEDICAL_RECEIPT", "The parent document must be a medical receipt.");

            var statementType = (await types.FindAsync(t =>
                t.Code == DocumentTypeCodes.MedicalCostStatement && !t.IsTaxEligible)).SingleOrDefault();
            if (statementType == null)
                throw new BadRequestException("ERR_MEDICAL_STATEMENT_TYPE_NOT_CONFIGURED", "Configure the medical statement document type before uploading.");

            var statementRepo = _unitOfWork.Repository<MedicalStatement>();
            var existingStatement = (await statementRepo.FindAsync(d => d.DocumentId == documentId)).FirstOrDefault();

            if (existingStatement != null)
            {
                if (string.Equals(existingStatement.Status, MedicalStatementStatus.CONFIRMED, StringComparison.OrdinalIgnoreCase))
                    throw new BadRequestException(ErrorCodes.InvalidDocumentStatus, ErrorMessages.DocumentAlreadyVerified);

                throw new ConflictException(ErrorCodes.DuplicateDocument,
                    "A medical statement already exists for this receipt. Delete it before uploading a replacement.");
            }

            // 4. Lưu file và tạo record TRỰC TIẾP trong bảng medical_statements
            var fileUrl = await _fileStorageService.SaveFileAsync(file, $"tax-periods/{period.TaxYear}/medical-statements");
            var statement = new MedicalStatement
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                OriginalFilename = file.FileName,
                FileUrl = fileUrl,
                Status = MedicalStatementStatus.UPLOADED,
                CreatedAt = DateTime.UtcNow
            };

            await statementRepo.AddAsync(statement);
            await _unitOfWork.SaveChangesAsync();

            // 5. Publish task OCR sang RabbitMQ để bóc tách chi tiết vào medical_statement_lines
            try
            {
                await PublishMedicalStatementOcrAsync(period, statement);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish medical statement OCR task for receipt {ReceiptId}", documentId);
            }

            return statement.ToDto()!;
        }

        private async Task PublishMedicalStatementOcrAsync(TaxPeriod period, MedicalStatement statement)
        {
            var categories = await GetMedicalCategoriesForAiAsync();
            var ocrMessage = new DocumentOcrExtractRequestMessage
            {
                TaskId = statement.Id,
                PeriodId = period.Id,
                UserId = period.UserId,
                TargetYear = period.TaxYear,
                FileUrl = statement.FileUrl,
                OriginalFilename = statement.OriginalFilename ?? string.Empty,
                Categories = categories,
                DocTypeCode = DocumentTypeCodes.MedicalCostStatement,
                IsMedicalStatement = true,
                ParentDocumentId = statement.DocumentId
            };

            _ocrProducerService.PublishBatchOcrTasks(new[] { ocrMessage });
        }

        private async Task<List<CategoryItemDto>> GetMedicalCategoriesForAiAsync()
        {
            var catRepo = _unitOfWork.Repository<MedicalEligibleCategory>();
            var allCats = (await catRepo.FindAsync(c => !c.IsDeleted))
                .OrderBy(c => c.SectionCode)
                .ToList();

            if (!allCats.Any())
            {
                return await GetEligibleCategoriesForAiAsync();
            }

            return allCats.Select(c => new CategoryItemDto
            {
                Code = c.CategoryCode,
                Name = c.CategoryName,
                IsMedicalStatement = true,
                Description = $"[{(c.IsEligible ? "ĐƯỢC GIẢM TRỪ THUẾ" : "LOẠI TRỪ KHỎI GIẢM TRỪ THUẾ")}] {c.Description} [Từ khóa: {c.Keywords}]"
            }).ToList();
        }

        public async Task DeleteMedicalStatementAsync(Guid userId, Guid periodId, Guid documentId)
        {
            var period = await _unitOfWork.Repository<TaxPeriod>().GetByIdAsync(periodId);
            if (period == null || period.UserId != userId)
                throw new NotFoundException(ErrorMessages.TaxPeriodNotFound);

            if (string.Equals(period.Status, TaxPeriodStatus.SUBMITTED, StringComparison.OrdinalIgnoreCase))
                throw new ForbiddenException(ErrorMessages.TaxPeriodSubmitted);

            var document = await _unitOfWork.Repository<Document>().GetByIdAsync(documentId);
            if (document == null || document.PeriodId != periodId)
                throw new NotFoundException(ErrorMessages.DocumentNotFound);

            if (string.Equals(document.Status, "CONFIRMED", StringComparison.OrdinalIgnoreCase))
                throw new BadRequestException(ErrorCodes.InvalidDocumentStatus, ErrorMessages.DocumentAlreadyVerified);

            var statementRepo = _unitOfWork.Repository<MedicalStatement>();
            var statement = (await statementRepo.FindAsync(s => s.DocumentId == documentId)).FirstOrDefault();
            if (statement == null)
                throw new NotFoundException("Không tìm thấy bảng kê chi phí KCB của hóa đơn này.");

            if (string.Equals(statement.Status, MedicalStatementStatus.CONFIRMED, StringComparison.OrdinalIgnoreCase))
                throw new BadRequestException(ErrorCodes.InvalidDocumentStatus, ErrorMessages.DocumentAlreadyVerified);

            // Xóa các dòng bóc tách chi phí
            var lineRepo = _unitOfWork.Repository<MedicalStatementLine>();
            var lines = await lineRepo.FindAsync(l => l.MedicalStatementId == statement.Id);
            foreach (var l in lines)
            {
                lineRepo.Remove(l);
            }

            // Xóa file storage nếu có
            if (!string.IsNullOrWhiteSpace(statement.FileUrl))
            {
                try
                {
                    await _fileStorageService.DeleteFileAsync(statement.FileUrl);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete medical statement file {FileUrl}", statement.FileUrl);
                }
            }

            statementRepo.Remove(statement);
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Deleted medical statement {StatementId} for document {DocId}", statement.Id, documentId);
        }
    }
}
