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

            // 6. Publish sang RabbitMQ để kích hoạt AI OCR bất đồng bộ (kèm danh mục động từ Admin)
            try
            {
                var categories = await GetEligibleCategoriesForAiAsync();
                var ocrMessages = createdDocuments.Select(doc => new DocumentOcrExtractRequestMessage
                {
                    TaskId = doc.Id,
                    PeriodId = period.Id,
                    UserId = period.UserId,
                    TargetYear = period.TaxYear,
                    FileUrl = doc.FileUrl,
                    OriginalFilename = doc.OriginalFilename ?? string.Empty,
                    Categories = categories
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
            if (!string.IsNullOrWhiteSpace(dto.SellerTaxCode))
            {
                var normTaxCode = NormalizeTaxCode(dto.SellerTaxCode);
                var periodDocuments = await docRepo.FindAsync(d => d.PeriodId == periodId && d.Id != documentId);
                var confirmedDocs = periodDocuments.Where(d => string.Equals(d.Status, "CONFIRMED", StringComparison.OrdinalIgnoreCase)).ToList();

                if (!string.IsNullOrWhiteSpace(dto.InvoiceNumber))
                {
                    var normInvNum = NormalizeInvoiceNumber(dto.InvoiceNumber);
                    if (confirmedDocs.Any(d => NormalizeTaxCode(d.SellerTaxCode) == normTaxCode && NormalizeInvoiceNumber(d.InvoiceNumber) == normInvNum))
                    {
                        throw new ConflictException(ErrorCodes.DuplicateDocument, ErrorMessages.DuplicateDocumentExists(dto.InvoiceNumber.Trim(), dto.SellerTaxCode.Trim()));
                    }
                }
                else
                {
                    if (confirmedDocs.Any(d => NormalizeTaxCode(d.SellerTaxCode) == normTaxCode 
                        && d.DocumentType == document.DocumentType 
                        && d.TotalIncome == dto.TotalIncome && d.TotalAmount == dto.TotalAmount))
                    {
                        throw new ConflictException(ErrorCodes.DuplicateDocument, "Đã tồn tại chứng từ/hóa đơn có cùng Mã số thuế và Số tiền trong kỳ tính thuế này.");
                    }
                }
            }
            document.TotalIncome = dto.TotalIncome;
            document.TaxWithheld = dto.TaxWithheld;
            document.InsuranceDeducted = dto.InsuranceDeducted;

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

            // Lưu trước các thay đổi của Document để trạng thái CONFIRMED được ghi nhận vào DB
            await _unitOfWork.SaveChangesAsync();

            // =========================================================================
            // NẾU LÀ CHỨNG TỪ KHẤU TRỪ THUẾ (WITHHOLDING_VOUCHER) -> TÍNH TOÁN LẠI TỔNG (SUM) VÀO INCOME_SOURCES
            // =========================================================================
            if (string.Equals(document.DocTypeCode, "WITHHOLDING_VOUCHER", StringComparison.OrdinalIgnoreCase))
            {
                var taxYear = dto.ExtractedYear ?? document.ExtractedYear ?? period.TaxYear;
                var taxCode = dto.SellerTaxCode ?? document.SellerTaxCode ?? string.Empty;
                var companyName = (dto.SellerName ?? document.SellerName ?? "Tổ chức chi trả thu nhập").Trim();

                if (!string.IsNullOrWhiteSpace(taxCode))
                {
                    await SyncWithholdingVoucherIncomeSourceAsync(userId, periodId, taxYear, taxCode, companyName);
                    await _unitOfWork.SaveChangesAsync();
                }
            }

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

            var categories = await GetEligibleCategoriesForAiAsync();
            var message = new DocumentOcrExtractRequestMessage
            {
                TaskId = document.Id,
                PeriodId = period.Id,
                UserId = period.UserId,
                TargetYear = period.TaxYear,
                FileUrl = document.FileUrl,
                OriginalFilename = document.OriginalFilename ?? string.Empty,
                Categories = categories
            };

            _ocrProducerService.PublishBatchOcrTasks(new[] { message });
            _logger.LogInformation("Re-triggered OCR task for document {DocId} in period {PeriodId}", documentId, periodId);
        }

        public async Task DeleteDocumentAsync(Guid userId, Guid periodId, Guid documentId)
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

            var documentRepo = _unitOfWork.Repository<Document>();
            var document = await documentRepo.GetByIdAsync(documentId);

            if (document == null || document.PeriodId != periodId)
            {
                throw new NotFoundException(ErrorMessages.DocumentNotFound);
            }

            if (string.Equals(document.Status, "CONFIRMED", StringComparison.OrdinalIgnoreCase))
            {
                throw new BadRequestException(ErrorCodes.InvalidDocumentStatus, ErrorMessages.DocumentAlreadyVerified);
            }

            await _fileStorageService.DeleteFileAsync(document.FileUrl);

            var itemRepo = _unitOfWork.Repository<DocumentItem>();
            var items = await itemRepo.FindAsync(item => item.DocumentId == documentId);
            foreach (var item in items)
            {
                itemRepo.Remove(item);
            }

            documentRepo.Remove(document);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Document {DocId} deleted by user {UserId}", documentId, userId);
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

        // Bỏ các kí tự khoảng trắng và -
        private static string NormalizeTaxCode(string? taxCode)
        {
            return string.Concat((taxCode ?? string.Empty)
                .Where(character => !char.IsWhiteSpace(character) && character != '-'))
                .ToUpperInvariant();
        }
        // Bỏ các kí tự khoảng trắng và -
        private static string NormalizeInvoiceNumber(string? invoiceNumber)
        {
            return string.Concat((invoiceNumber ?? string.Empty)
                .Where(character => !char.IsWhiteSpace(character)))
                .ToUpperInvariant();
        }
        // Lấy lên tất cả doctype có IsTaxEligible = true
        private async Task<List<CategoryItemDto>> GetEligibleCategoriesForAiAsync()
        {
            var docTypeRepo = _unitOfWork.Repository<TaxDocumentType>();
            var allDocTypes = (await docTypeRepo.GetAllAsync())
                .OrderByDescending(t => t.IsTaxEligible)
                .ThenBy(t => t.Code)
                .ToList();

            return allDocTypes.Select(t =>
            {
                var rawDesc = !string.IsNullOrWhiteSpace(t.Description) ? t.Description : t.Name;
                var prefix = t.IsTaxEligible
                    ? "[ĐƯỢC GIẢM TRỪ THUẾ TNCN]"
                    : "[KHÔNG ĐƯỢC GIẢM TRỪ THUẾ TNCN]";

                return new CategoryItemDto
                {
                    Code = t.Code,
                    Name = t.Name,
                    Description = $"{prefix} {rawDesc}"
                };
            }).ToList();
        }

        /// <summary>
        /// Đồng bộ nguồn thu nhập (income_sources) theo mô hình Master-Detail SUM từ tất cả các chứng từ khấu trừ thuế đã CONFIRMED của cùng 1 công ty trong kỳ tính thuế.
        /// </summary>
        private async Task SyncWithholdingVoucherIncomeSourceAsync(
            Guid userId,
            Guid periodId,
            int taxYear,
            string taxCode,
            string companyName)
        {
            // chuẩn hóa  lại Mã số thuế của Công ty
            var normTaxCode = NormalizeTaxCode(taxCode);
            // rỗng return 
            if (string.IsNullOrWhiteSpace(normTaxCode)) return;

            var incomeSourceRepo = _unitOfWork.Repository<IncomeSource>();
            var docRepo = _unitOfWork.Repository<Document>();

            // 1. Lấy tất cả các chứng từ khấu trừ thuế ĐÃ XÁC NHẬN (CONFIRMED) trong kỳ kê khai này
            var periodDocuments = await docRepo.FindAsync(d =>
                d.PeriodId == periodId &&
                d.DocTypeCode == "WITHHOLDING_VOUCHER" &&
                d.Status == "CONFIRMED");

            // lấy lên tất cả mst của chứng từ thông qua periodDocuments
            var companyDocs = periodDocuments
                .Where(d => NormalizeTaxCode(d.SellerTaxCode) == normTaxCode)
                .ToList();

            // 2. Tìm nguồn thu nhập tương ứng của User theo (TaxpayerId, CompanyTaxCode, TaxYear)
            // tìm kiếm nguồn thu nhập của người nộp thuế mã số thuế ctym, năm chứng từ
            var existingSources = await incomeSourceRepo.FindAsync(s =>
                s.TaxpayerId == userId &&
                s.CompanyTaxCode == normTaxCode &&
                s.TaxYear == taxYear);

            // lấy ra cái đầu tiên
            var incomeSource = existingSources.FirstOrDefault();

            // check xem trong mã số thuế trong documnet còn hay ko nếu ko còn reset lại 0
            if (!companyDocs.Any())
            {
                // Nếu không còn chứng từ nào (ví dụ user đã xóa hết chứng từ của công ty này)
                if (incomeSource != null)
                {
                    incomeSource.TotalIncome = 0;
                    incomeSource.TaxWithheld = 0;
                    incomeSource.InsuranceDeducted = 0;
                    incomeSource.IsActive = false;
                    incomeSource.UpdatedAt = DateTime.UtcNow;
                    incomeSourceRepo.Update(incomeSource);
                    _logger.LogInformation("Deactivated IncomeSource {Id} because no confirmed documents remain for company {TaxCode}",
                        incomeSource.Id, normTaxCode);
                }
                return;
            }

            // 3. TÍNH TỔNG (SUM) TỪ TẤT CẢ CÁC CHỨNG TỪ CỦA CÔNG TY ĐÓ TRONG KỲ
            decimal totalIncomeSum = companyDocs.Sum(d => d.TotalIncome ?? d.TotalAmount ?? 0);
            decimal taxWithheldSum = companyDocs.Sum(d => d.TaxWithheld ?? d.TotalAmount ?? 0);
            decimal insuranceDeductedSum = companyDocs.Sum(d => d.InsuranceDeducted ?? 0);

            var latestCompanyName = companyDocs
                .OrderByDescending(d => d.InvoiceDate ?? DateOnly.MinValue)
                .Select(d => d.SellerName)
                .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? companyName;

            if (incomeSource != null)
            {
                incomeSource.CompanyName = latestCompanyName;
                incomeSource.TotalIncome = totalIncomeSum;
                incomeSource.TaxWithheld = taxWithheldSum;
                incomeSource.InsuranceDeducted = insuranceDeductedSum;
                incomeSource.IsActive = true;
                incomeSource.UpdatedAt = DateTime.UtcNow;

                incomeSourceRepo.Update(incomeSource);
                _logger.LogInformation("Recalculated IncomeSource {Id} for User {UserId}: TotalIncome={TotalIncome}, TaxWithheld={TaxWithheld}, Insurance={Insurance} across {Count} documents",
                    incomeSource.Id, userId, totalIncomeSum, taxWithheldSum, insuranceDeductedSum, companyDocs.Count);
            }
            else
            {
                var newIncomeSource = new IncomeSource
                {
                    Id = Guid.NewGuid(),
                    TaxpayerId = userId,
                    CompanyName = latestCompanyName,
                    CompanyTaxCode = normTaxCode,
                    TaxYear = taxYear,
                    TotalIncome = totalIncomeSum,
                    TaxWithheld = taxWithheldSum,
                    InsuranceDeducted = insuranceDeductedSum,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await incomeSourceRepo.AddAsync(newIncomeSource);
                _logger.LogInformation("Created new IncomeSource {Id} for User {UserId}: TotalIncome={TotalIncome}, TaxWithheld={TaxWithheld}, Insurance={Insurance} across {Count} documents",
                    newIncomeSource.Id, userId, totalIncomeSum, taxWithheldSum, insuranceDeductedSum, companyDocs.Count);
            }
        }
    }
}
