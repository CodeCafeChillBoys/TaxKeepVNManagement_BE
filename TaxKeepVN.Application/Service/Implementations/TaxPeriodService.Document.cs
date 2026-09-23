using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
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
        public async Task<BatchUploadDocumentsResponseDto> BatchUploadDocumentsAsync(Guid userId, Guid periodId, IList<IFormFile> files)
        {
            // 1. Kiểm tra Tax Period
            var periodRepo = _unitOfWork.Repository<TaxPeriod>();
            var period = await periodRepo.GetByIdAsync(periodId);

            // Kiểm tra kỳ kê khai có tồn tại và thuộc về user hiện tại
            if (period == null || period.UserId != userId)
            {
                throw new NotFoundException(ErrorMessages.TaxPeriodNotFound);
            }
            // Kiểm tra trạng thái kỳ kê khai, nếu đã SUBMITTED thì không cho phép upload thêm
            if (string.Equals(period.Status, TaxPeriodStatus.SUBMITTED, StringComparison.OrdinalIgnoreCase))
            {
                throw new ForbiddenException(ErrorMessages.TaxPeriodSubmitted);
            }

            // 2. Kiểm tra files payload
            if (files == null || files.Count == 0)
            {
                throw new BadRequestException(ErrorCodes.FilesRequired, ErrorMessages.FilesRequired);
            }
            // Chỉ cho phép upload các định dạng file: .jpg, .jpeg, .png, .pdf
            var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".pdf" };
            // Chỉ cho phép các MIME type tương ứng
            var allowedMimeTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "image/jpeg", "image/jpg", "image/pjpeg",
                "image/png", "image/x-png",
                "application/pdf"
            };

            // 3. Validate từng file
            foreach (var file in files)
            {
                // Kiểm tra file null hoặc rỗng
                if (file == null || file.Length == 0)
                {
                    throw new BadRequestException(ErrorCodes.FilesRequired, ErrorMessages.FilesRequired);
                }

                // Kiểm tra định dạng file dựa trên extension và MIME type
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                // Nếu extension không hợp lệ hoặc MIME type không hợp lệ (nếu có) -> ném lỗi
                if (!allowedExtensions.Contains(ext) || (!string.IsNullOrEmpty(file.ContentType) && !allowedMimeTypes.Contains(file.ContentType)))
                {
                    throw new BadRequestException(ErrorCodes.UnsupportedFormat, ErrorMessages.UnsupportedFormat);
                }
                // Kiểm tra kích thước file, giới hạn 10MB
                if (file.Length > 10 * 1024 * 1024)
                {
                    throw new BadRequestException(ErrorCodes.FileSizeExceeded, ErrorMessages.FileSizeExceeded);
                }
            }

            // 4. Lưu files vào Storage & Tạo entities Document
            var createdDocuments = new List<Document>();

            foreach (var file in files)
            {
                var fileUrl = await _fileStorageService.SaveFileAsync(file, $"tax-periods/{period.TaxYear}");

                var document = new Document
                {
                    Id = Guid.NewGuid(),
                    PeriodId = period.Id,
                    DocTypeCode = null,
                    OriginalFilename = file.FileName,
                    FileUrl = fileUrl,
                    Status = "UPLOADED",
                    CreatedAt = DateTime.UtcNow
                };

                createdDocuments.Add(document);
            }

            // 5. Lưu xuống DB trong transaction
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var docRepo = _unitOfWork.Repository<Document>();
                foreach (var doc in createdDocuments)
                {
                    await docRepo.AddAsync(doc);
                }
                await _unitOfWork.SaveChangesAsync();               
                await _unitOfWork.CommitTransactionAsync();
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                _logger.LogError(ex, "Failed to save uploaded documents to database for period {PeriodId}", periodId);
                throw;
            }

            // 6. Publish sang RabbitMQ để kích hoạt AI OCR bất đồng bộ
            try
            {
                var ocrMessages = createdDocuments.Select(doc => new DocumentOcrExtractRequestMessage
                {
                    TaskId = doc.Id,
                    PeriodId = period.Id,
                    UserId = period.UserId,
                    TargetYear = period.TaxYear,
                    FileUrl = doc.FileUrl,
                    OriginalFilename = doc.OriginalFilename ?? string.Empty
                }).ToList();

                _ocrProducerService.PublishBatchOcrTasks(ocrMessages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish OCR tasks to RabbitMQ for period {PeriodId}", periodId);
            }

            // 7. Trả về kết quả (Dùng DocumentMapper)
            return new BatchUploadDocumentsResponseDto
            {
                PeriodId = period.Id,
                TotalUploaded = createdDocuments.Count,
                Documents = createdDocuments.Select(doc => doc.ToUploadedDto(period.UserId)).ToList()
            };
        }

        public async Task<DocumentReviewResponseDto> ConfirmDocumentReviewAsync(Guid userId, Guid periodId, Guid documentId, ConfirmDocumentReviewRequestDto dto)
        {
            var periodRepo = _unitOfWork.Repository<TaxPeriod>();
            var period = await periodRepo.GetByIdAsync(periodId);

            if (period == null || period.UserId != userId)
            {
                throw new NotFoundException(ErrorMessages.TaxPeriodNotFound);
            }

            if (string.Equals(period.Status, TaxPeriodStatus.SUBMITTED, StringComparison.OrdinalIgnoreCase))
            {
                throw new ForbiddenException(ErrorMessages.TaxPeriodSubmitted);
            }

            var docRepo = _unitOfWork.Repository<Document>();
            var document = await docRepo.GetByIdAsync(documentId);

            if (document == null || document.PeriodId != periodId)
            {
                throw new NotFoundException(ErrorMessages.DocumentNotFound);
            }

            // 400: Không cho phép xác nhận nếu document đã ở trạng thái CONFIRMED
            if (string.Equals(document.Status, "CONFIRMED", StringComparison.OrdinalIgnoreCase))
            {
                throw new BadRequestException(ErrorCodes.InvalidDocumentStatus, ErrorMessages.DocumentAlreadyVerified);
            }

            // 409: Trùng số hóa đơn & MST người bán trong cùng kỳ tính thuế
            if (!string.IsNullOrWhiteSpace(dto.SellerTaxCode) && !string.IsNullOrWhiteSpace(dto.InvoiceNumber))
            {
                var normTaxCode = dto.SellerTaxCode.Trim();
                var normInvNum = dto.InvoiceNumber.Trim();
                // Tìm các document khác trong cùng kỳ có cùng MST người bán và số hóa đơn
                var duplicateDocs = await docRepo.FindAsync(d =>
                    d.PeriodId == periodId &&
                    d.Id != documentId &&
                    d.SellerTaxCode == normTaxCode &&
                    d.InvoiceNumber == normInvNum);

                if (duplicateDocs.Any())
                {
                    throw new ConflictException(ErrorCodes.DuplicateDocument,
                        ErrorMessages.DuplicateDocumentExists(normInvNum, normTaxCode));
                }
            }

            // Cập nhật thông tin document dựa trên dữ liệu từ DTO
            if (!string.IsNullOrWhiteSpace(dto.DocTypeCode))
            {
                // Chuẩn hóa mã loại chứng từ trước khi tìm kiếm
                var normalizedCode = dto.DocTypeCode.Trim().ToUpperInvariant();
                var docTypeRepo = _unitOfWork.Repository<TaxDocumentType>();
                // Tìm loại chứng từ trong danh mục dựa trên mã chuẩn hóa
                var existingDocType = (await docTypeRepo.FindAsync(t => t.Code == normalizedCode)).FirstOrDefault();
                // Nếu không tìm thấy loại chứng từ -> ném lỗi
                if (existingDocType == null)
                {
                    throw new BadRequestException(ErrorCodes.InvalidDocType, ErrorMessages.InvalidDocType(normalizedCode));
                }

                document.DocTypeCode = existingDocType.Code;
            }
            // 400: Không cho phép xác nhận nếu thông tin người mua không khớp với Người nộp thuế hoặc Người phụ thuộc
            if (document.IsIdentityValid == false)
            {
                throw new BadRequestException(ErrorCodes.IdentityMismatch,
                    ErrorMessages.IdentityMismatch(document.BuyerName ?? "Không xác định"));
            }

            document.InvoiceSeries = dto.InvoiceSeries;
            document.InvoiceNumber = dto.InvoiceNumber;
            document.InvoiceDate = dto.InvoiceDate;
            document.SellerName = dto.SellerName;
            document.SellerTaxCode = dto.SellerTaxCode;
            document.SellerAddress = dto.SellerAddress;
            document.SellerPhone = dto.SellerPhone;
            // Giữ nguyên thông tin định danh người mua từ bóc tách OCR gốc để đảm bảo tính pháp lý
            // Không cho phép ghi đè thông tin người mua
            document.BuyerAddress = dto.BuyerAddress ?? document.BuyerAddress;
            document.PaymentMethod = dto.PaymentMethod;
            document.TotalAmount = dto.TotalAmount;
            document.TotalAmountInWords = dto.TotalAmountInWords;
            document.LookupUrl = dto.LookupUrl;
            document.LookupCode = dto.LookupCode;
            document.ExtractedYear = dto.ExtractedYear;
            document.IsYearValid = dto.IsYearValid;
            // Giữ nguyên IsIdentityValid đã được hệ thống thẩm định
            document.IsNotReimbursed = dto.IsNotReimbursed;

            // Đánh dấu người dùng đã review và lưu chính thức
            document.Status = "CONFIRMED";

            docRepo.Update(document);

            var savedItems = new List<DocumentItem>();
            // Lưu danh sách chi tiết hàng hóa / dịch vụ / viện phí vào bảng document_items
            if (dto.Items != null && dto.Items.Any())
            {
                var itemRepo = _unitOfWork.Repository<DocumentItem>();
                var existingItems = await itemRepo.FindAsync(i => i.DocumentId == documentId);
                foreach (var oldItem in existingItems)
                {
                    itemRepo.Remove(oldItem);
                }

                foreach (var itemDto in dto.Items)
                {
                    var item = itemDto.ToEntity(document.Id);
                    await itemRepo.AddAsync(item);
                    savedItems.Add(item);
                }
            }

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Document {DocId} has been reviewed and CONFIRMED by user {UserId}", documentId, userId);

            TaxDocumentType? docTypeEntity = null;
            if (!string.IsNullOrWhiteSpace(document.DocTypeCode))
            {
                var docTypeRepo = _unitOfWork.Repository<TaxDocumentType>();
                docTypeEntity = (await docTypeRepo.FindAsync(t => t.Code == document.DocTypeCode)).FirstOrDefault();
            }

            // Trả về DTO thông qua DocumentMapper
            return document.ToReviewDto(docTypeEntity, savedItems);
        }

        public async Task TriggerDocumentOcrAsync(Guid userId, Guid periodId, Guid documentId)
        {
            var periodRepo = _unitOfWork.Repository<TaxPeriod>();
            var period = await periodRepo.GetByIdAsync(periodId);

            if (period == null || period.UserId != userId)
            {
                throw new NotFoundException(ErrorMessages.TaxPeriodNotFound);
            }

            if (string.Equals(period.Status, TaxPeriodStatus.SUBMITTED, StringComparison.OrdinalIgnoreCase))
            {
                throw new ForbiddenException(ErrorMessages.TaxPeriodSubmitted);
            }

            var docRepo = _unitOfWork.Repository<Document>();
            var document = await docRepo.GetByIdAsync(documentId);

            if (document == null || document.PeriodId != periodId)
            {
                throw new NotFoundException(ErrorMessages.DocumentNotFound);
            }

            // 400: Document không ở trạng thái UPLOADED (chặn nếu đã EXTRACTED hoặc CONFIRMED)
            if (!string.Equals(document.Status, "UPLOADED", StringComparison.OrdinalIgnoreCase))
            {
                throw new BadRequestException(ErrorCodes.InvalidDocumentStatus, ErrorMessages.DocumentAlreadyVerified);
            }

            var message = new DocumentOcrExtractRequestMessage
            {
                TaskId = document.Id,
                PeriodId = period.Id,
                UserId = period.UserId,
                TargetYear = period.TaxYear,
                FileUrl = document.FileUrl,
                OriginalFilename = document.OriginalFilename ?? string.Empty
            };

            _ocrProducerService.PublishBatchOcrTasks(new[] { message });
            _logger.LogInformation("Re-triggered OCR task for document {DocId} in period {PeriodId}", documentId, periodId);
        }

        public async Task<List<DocumentReviewResponseDto>> GetDocumentsAsync(
            Guid userId,
            Guid periodId)
        {
            // 1. Kiểm tra Tax Period có tồn tại và thuộc về user hiện tại không
            var periodRepo = _unitOfWork.Repository<TaxPeriod>();
            var period = await periodRepo.GetByIdAsync(periodId);

            if (period == null || period.UserId != userId)
            {
                throw new NotFoundException(ErrorMessages.TaxPeriodNotFound);
            }

            // 2. Lấy tất cả Document thuộc Period
            var docRepo = _unitOfWork.Repository<Document>();
            var documents = (await docRepo.FindAsync(
                d => d.PeriodId == periodId))
                .OrderByDescending(d => d.CreatedAt)
                .ToList();

            // 3. Lấy danh mục loại chứng từ
            var docTypeRepo = _unitOfWork.Repository<TaxDocumentType>();
            var allTypes = await docTypeRepo.GetAllAsync();
            // Tạo dictionary để tra cứu nhanh theo mã loại chứng từ 
            var docTypesDict = allTypes.ToDictionary(
                t => t.Code,
                StringComparer.OrdinalIgnoreCase);

            // 4. Lấy tất cả DocumentItem của các Document
            var docIds = documents.Select(d => d.Id).ToList();

            // Tạo dictionary để tra cứu nhanh theo ID của Document

            var itemsByDocId = new Dictionary<Guid, List<DocumentItem>>();

            if (docIds.Count > 0)
            {
                var itemRepo = _unitOfWork.Repository<DocumentItem>();
                // Lấy tất cả DocumentItem của các Document trong kỳ
                var allItems = (await itemRepo.FindAsync(
                    i => docIds.Contains(i.DocumentId)))
                    .ToList();
                // Nhóm các DocumentItem theo DocumentId và sắp xếp theo ItemOrder
                itemsByDocId = allItems
                    .GroupBy(i => i.DocumentId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.OrderBy(i => i.ItemOrder).ToList());
            }

            // 5. Map Document -> DocumentReviewResponseDto qua DocumentMapper
            var result = documents.Select(doc =>
            {
                TaxDocumentType? docType = null;
                if (!string.IsNullOrEmpty(doc.DocTypeCode))
                {
                    docTypesDict.TryGetValue(doc.DocTypeCode, out docType);
                }

                itemsByDocId.TryGetValue(doc.Id, out var itemsList);

                return doc.ToReviewDto(docType, itemsList);
            }).ToList();

            return result;
        }

        public async Task<DocumentReviewResponseDto> GetDocumentByIdAsync(Guid userId, Guid periodId, Guid documentId)
        {
            // 1. Kiểm tra Tax Period có tồn tại và thuộc về user hiện tại không
            var periodRepo = _unitOfWork.Repository<TaxPeriod>();
            var period = await periodRepo.GetByIdAsync(periodId);

            if (period == null || period.UserId != userId)
            {
                throw new NotFoundException(ErrorMessages.TaxPeriodNotFound);
            }

            // 2. Lấy Document và kiểm tra Document có thuộc Period này không
            var docRepo = _unitOfWork.Repository<Document>();
            var document = await docRepo.GetByIdAsync(documentId);

            if (document == null || document.PeriodId != periodId)
            {
                throw new NotFoundException(ErrorMessages.DocumentNotFound);
            }

            // 3. Lấy thông tin loại chứng từ
            var docTypeRepo = _unitOfWork.Repository<TaxDocumentType>();
            var allTypes = await docTypeRepo.GetAllAsync();

            var docTypesDict = allTypes.ToDictionary(
                t => t.Code,
                StringComparer.OrdinalIgnoreCase);

            TaxDocumentType? docType = null;
            if (!string.IsNullOrEmpty(document.DocTypeCode))
            {
                docTypesDict.TryGetValue(document.DocTypeCode, out docType);
            }

            // 4. Lấy danh sách các item của document
            var itemRepo = _unitOfWork.Repository<DocumentItem>();
            var items = (await itemRepo.FindAsync(i => i.DocumentId == document.Id))
                .OrderBy(i => i.ItemOrder)
                .ToList();

            // 5. Map Document -> DocumentReviewResponseDto qua DocumentMapper
            return document.ToReviewDto(docType, items);
        }
    }
}
