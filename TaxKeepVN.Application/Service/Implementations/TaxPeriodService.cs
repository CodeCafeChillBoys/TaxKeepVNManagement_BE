using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PdfSharpCore.Pdf.IO;
using TaxKeepVN.Application.Constants;
using TaxKeepVN.Application.DTOs.Documents;
using TaxKeepVN.Application.DTOs.TaxAI;
using TaxKeepVN.Application.DTOs.TaxPeriods;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    public class TaxPeriodService : ITaxPeriodService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _fileStorageService;
        private readonly IDocumentOcrProducerService _ocrProducerService;
        private readonly ILogger<TaxPeriodService> _logger;

        public TaxPeriodService(
            IUnitOfWork unitOfWork,
            IFileStorageService fileStorageService,
            IDocumentOcrProducerService ocrProducerService,
            ILogger<TaxPeriodService> logger)
        {
            _unitOfWork = unitOfWork;
            _fileStorageService = fileStorageService;
            _ocrProducerService = ocrProducerService;
            _logger = logger;
        }

        public async Task<TaxPeriodResponseDto> InitOrGetPeriodAsync(Guid userId, int taxYear)
        {
            int currentYear = DateTime.UtcNow.Year;
            if (taxYear < 2015 || taxYear > currentYear)
            {
                throw new BadRequestException(ErrorCodes.InvalidTaxYear, ErrorMessages.InvalidTaxYear(currentYear));
            }
            var repo = _unitOfWork.Repository<TaxPeriod>();
            var periods = await repo.FindAsync(p => p.UserId == userId && p.TaxYear == (short)taxYear);
            var period = periods.FirstOrDefault();
            if (period != null)
            {
                // Kiểm tra trạng thái nếu kỳ kê khai đã nộp / hoàn tất
                if (string.Equals(period.Status, TaxPeriodStatus.SUBMITTED, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ForbiddenException(ErrorMessages.TaxPeriodSubmittedForYear(taxYear));
                }

                return new TaxPeriodResponseDto
                {
                    PeriodId = period.Id,
                    TaxYear = period.TaxYear,
                    Status = period.Status,
                    CreatedAt = period.CreatedAt
                };
            }

            // Nếu chưa tồn tại -> Tạo mới với trạng thái DRAFT
            var newPeriod = new TaxPeriod
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TaxYear = (short)taxYear,
                Status = TaxPeriodStatus.DRAFT,
                CreatedAt = DateTimeOffset.UtcNow
            };
            await repo.AddAsync(newPeriod);
            await _unitOfWork.SaveChangesAsync();

            return new TaxPeriodResponseDto
            {
                PeriodId = newPeriod.Id,
                TaxYear = newPeriod.TaxYear,
                Status = newPeriod.Status,
                CreatedAt = newPeriod.CreatedAt
            };
        }

        public async Task<BatchUploadDocumentsResponseDto> BatchUploadDocumentsAsync(Guid userId, Guid periodId, IList<IFormFile> files)
        {
            // 1. Kiểm tra Tax Period
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

            // 2. Kiểm tra files payload
            if (files == null || files.Count == 0)
            {
                throw new BadRequestException(ErrorCodes.FilesRequired, ErrorMessages.FilesRequired);
            }

            var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".pdf" };
            var allowedMimeTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "image/jpeg", "image/jpg", "image/pjpeg",
                "image/png", "image/x-png",
                "application/pdf"
            };

            // 3. Validate từng file
            foreach (var file in files)
            {
                if (file == null || file.Length == 0)
                {
                    throw new BadRequestException(ErrorCodes.FilesRequired, ErrorMessages.FilesRequired);
                }

                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(ext) || (!string.IsNullOrEmpty(file.ContentType) && !allowedMimeTypes.Contains(file.ContentType)))
                {
                    throw new BadRequestException(ErrorCodes.UnsupportedFormat, ErrorMessages.UnsupportedFormat);
                }

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

            // 7. Trả về kết quả
            return new BatchUploadDocumentsResponseDto
            {
                PeriodId = period.Id,
                TotalUploaded = createdDocuments.Count,
                Documents = createdDocuments.Select(doc => new UploadedDocumentItemDto
                {
                    Id = doc.Id,
                    UserId = period.UserId,
                    PeriodId = doc.PeriodId,
                    DocTypeCode = doc.DocTypeCode,
                    OriginalFilename = doc.OriginalFilename,
                    FileUrl = doc.FileUrl,
                    Status = doc.Status,
                    CreatedAt = doc.CreatedAt
                }).ToList()
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

            if (!string.IsNullOrWhiteSpace(dto.DocTypeCode))
            {
                var normalizedCode = dto.DocTypeCode.Trim().ToUpperInvariant();
                var docTypeRepo = _unitOfWork.Repository<TaxDocumentType>();
                var existingDocType = (await docTypeRepo.FindAsync(t => t.Code == normalizedCode)).FirstOrDefault();

                if (existingDocType == null)
                {
                    throw new BadRequestException(ErrorCodes.InvalidDocType, ErrorMessages.InvalidDocType(normalizedCode));
                }

                document.DocTypeCode = existingDocType.Code;
            }
            document.InvoiceSeries = dto.InvoiceSeries;
            document.InvoiceNumber = dto.InvoiceNumber;
            document.InvoiceDate = dto.InvoiceDate;
            document.SellerName = dto.SellerName;
            document.SellerTaxCode = dto.SellerTaxCode;
            document.SellerAddress = dto.SellerAddress;
            document.SellerPhone = dto.SellerPhone;
            document.BuyerName = dto.BuyerName;
            document.BuyerTaxCode = dto.BuyerTaxCode;
            document.BuyerIdCard = dto.BuyerIdCard;
            document.BuyerAddress = dto.BuyerAddress;
            document.PaymentMethod = dto.PaymentMethod;
            document.TotalAmount = dto.TotalAmount;
            document.TotalAmountInWords = dto.TotalAmountInWords;
            document.LookupUrl = dto.LookupUrl;
            document.LookupCode = dto.LookupCode;
            document.ExtractedYear = dto.ExtractedYear;
            document.IsYearValid = dto.IsYearValid;
            document.IsIdentityValid = dto.IsIdentityValid;

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
                    var item = new DocumentItem
                    {
                        DocumentId = document.Id,
                        ItemOrder = itemDto.ItemOrder,
                        ItemName = itemDto.ItemName,
                        Unit = itemDto.Unit,
                        Quantity = itemDto.Quantity,
                        UnitPrice = itemDto.UnitPrice,
                        TotalPrice = itemDto.TotalPrice
                    };
                    await itemRepo.AddAsync(item);
                    savedItems.Add(item);
                }
            }

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Document {DocId} has been reviewed and CONFIRMED by user {UserId}", documentId, userId);

            return new DocumentReviewResponseDto
            {
                Id = document.Id,
                PeriodId = document.PeriodId,
                DocTypeCode = document.DocTypeCode,
                FileUrl = document.FileUrl,
                OriginalFilename = document.OriginalFilename,
                InvoiceSeries = document.InvoiceSeries,
                InvoiceNumber = document.InvoiceNumber,
                InvoiceDate = document.InvoiceDate,
                SellerName = document.SellerName,
                SellerTaxCode = document.SellerTaxCode,
                SellerAddress = document.SellerAddress,
                SellerPhone = document.SellerPhone,
                BuyerName = document.BuyerName,
                BuyerTaxCode = document.BuyerTaxCode,
                BuyerIdCard = document.BuyerIdCard,
                BuyerAddress = document.BuyerAddress,
                PaymentMethod = document.PaymentMethod,
                TotalAmount = document.TotalAmount,
                TotalAmountInWords = document.TotalAmountInWords,
                LookupUrl = document.LookupUrl,
                LookupCode = document.LookupCode,
                ExtractedYear = document.ExtractedYear,
                IsYearValid = document.IsYearValid,
                IsIdentityValid = document.IsIdentityValid,
                Status = document.Status,
                CreatedAt = document.CreatedAt,
                Items = savedItems.Select(i => new DocumentItemResponseDto
                {
                    Id = i.Id,
                    ItemOrder = i.ItemOrder,
                    ItemName = i.ItemName,
                    Unit = i.Unit,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    TotalPrice = i.TotalPrice
                }).ToList()
            };
        }
    }
}