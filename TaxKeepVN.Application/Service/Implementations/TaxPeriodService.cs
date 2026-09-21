using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PdfSharpCore.Pdf.IO;
using TaxKeepVN.Application.Constants;
using TaxKeepVN.Application.DTOs.Common;
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

            TaxDocumentType? docTypeEntity = null;
            if (!string.IsNullOrWhiteSpace(document.DocTypeCode))
            {
                var docTypeRepo = _unitOfWork.Repository<TaxDocumentType>();
                docTypeEntity = (await docTypeRepo.FindAsync(t => t.Code == document.DocTypeCode)).FirstOrDefault();
            }

            return new DocumentReviewResponseDto
            {
                Id = document.Id,
                PeriodId = document.PeriodId,
                DocTypeCode = document.DocTypeCode,
                DocTypeName = docTypeEntity?.Name,
                IsTaxEligible = docTypeEntity?.IsTaxEligible,
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

        public async Task<PagedResult<DocumentReviewResponseDto>> GetDocumentsAsync(Guid userId, Guid periodId, DocumentQueryParameters query)
        {
            var periodRepo = _unitOfWork.Repository<TaxPeriod>();
            var period = await periodRepo.GetByIdAsync(periodId);

            if (period == null || period.UserId != userId)
            {
                throw new NotFoundException(ErrorMessages.TaxPeriodNotFound);
            }

            var docRepo = _unitOfWork.Repository<Document>();
            var docs = (await docRepo.FindAsync(d => d.PeriodId == periodId)).AsEnumerable();

            // 1. Lọc theo trạng thái
            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                var normStatus = query.Status.Trim();
                docs = docs.Where(d => string.Equals(d.Status, normStatus, StringComparison.OrdinalIgnoreCase));
            }

            // 2. Lọc theo mã loại chứng từ
            if (!string.IsNullOrWhiteSpace(query.DocTypeCode))
            {
                var normCode = query.DocTypeCode.Trim();
                docs = docs.Where(d => string.Equals(d.DocTypeCode, normCode, StringComparison.OrdinalIgnoreCase));
            }

            // 3. Tìm kiếm theo tên người bán, MST, số hóa đơn, tên file
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var kw = query.Search.Trim().ToLowerInvariant();
                docs = docs.Where(d =>
                    (d.SellerName != null && d.SellerName.ToLowerInvariant().Contains(kw)) ||
                    (d.SellerTaxCode != null && d.SellerTaxCode.ToLowerInvariant().Contains(kw)) ||
                    (d.InvoiceNumber != null && d.InvoiceNumber.ToLowerInvariant().Contains(kw)) ||
                    (d.OriginalFilename != null && d.OriginalFilename.ToLowerInvariant().Contains(kw))
                );
            }

            // 4. Sắp xếp (mặc định mới nhất lên đầu nếu không chỉ định)
            if (!string.IsNullOrWhiteSpace(query.Sort))
            {
                var isDesc = query.Sort.StartsWith("-");
                var sortField = query.Sort.TrimStart('-', '+').ToLowerInvariant();

                docs = sortField switch
                {
                    "createdat" => isDesc ? docs.OrderByDescending(d => d.CreatedAt) : docs.OrderBy(d => d.CreatedAt),
                    "invoicedate" => isDesc ? docs.OrderByDescending(d => d.InvoiceDate) : docs.OrderBy(d => d.InvoiceDate),
                    "totalamount" => isDesc ? docs.OrderByDescending(d => d.TotalAmount) : docs.OrderBy(d => d.TotalAmount),
                    "sellername" => isDesc ? docs.OrderByDescending(d => d.SellerName) : docs.OrderBy(d => d.SellerName),
                    _ => isDesc ? docs.OrderByDescending(d => d.CreatedAt) : docs.OrderBy(d => d.CreatedAt)
                };
            }
            else
            {
                docs = docs.OrderByDescending(d => d.CreatedAt);
            }

            var totalItems = docs.Count();
            var pagedDocs = docs
                .Skip((query.Page - 1) * query.Size)
                .Take(query.Size)
                .ToList();

            // Tải danh mục loại chứng từ để map tên và tính hợp lệ thuế
            var docTypeRepo = _unitOfWork.Repository<TaxDocumentType>();
            var allTypes = await docTypeRepo.GetAllAsync();
            var docTypesDict = allTypes.ToDictionary(t => t.Code, StringComparer.OrdinalIgnoreCase);

            // Tải chi tiết items cho các chứng từ trong trang
            var docIds = pagedDocs.Select(d => d.Id).ToHashSet();
            var itemRepo = _unitOfWork.Repository<DocumentItem>();
            var allItems = (await itemRepo.FindAsync(i => docIds.Contains(i.DocumentId))).ToList();
            var itemsByDocId = allItems.GroupBy(i => i.DocumentId).ToDictionary(g => g.Key, g => g.OrderBy(i => i.ItemOrder).ToList());

            var dtos = pagedDocs.Select(doc =>
            {
                TaxDocumentType? docType = null;
                if (!string.IsNullOrEmpty(doc.DocTypeCode))
                {
                    docTypesDict.TryGetValue(doc.DocTypeCode, out docType);
                }

                itemsByDocId.TryGetValue(doc.Id, out var itemsList);

                return new DocumentReviewResponseDto
                {
                    Id = doc.Id,
                    PeriodId = doc.PeriodId,
                    DocTypeCode = doc.DocTypeCode,
                    DocTypeName = docType?.Name,
                    IsTaxEligible = docType?.IsTaxEligible,
                    FileUrl = doc.FileUrl,
                    OriginalFilename = doc.OriginalFilename,
                    InvoiceSeries = doc.InvoiceSeries,
                    InvoiceNumber = doc.InvoiceNumber,
                    InvoiceDate = doc.InvoiceDate,
                    SellerName = doc.SellerName,
                    SellerTaxCode = doc.SellerTaxCode,
                    SellerAddress = doc.SellerAddress,
                    SellerPhone = doc.SellerPhone,
                    BuyerName = doc.BuyerName,
                    BuyerTaxCode = doc.BuyerTaxCode,
                    BuyerIdCard = doc.BuyerIdCard,
                    BuyerAddress = doc.BuyerAddress,
                    PaymentMethod = doc.PaymentMethod,
                    TotalAmount = doc.TotalAmount,
                    TotalAmountInWords = doc.TotalAmountInWords,
                    LookupUrl = doc.LookupUrl,
                    LookupCode = doc.LookupCode,
                    ExtractedYear = doc.ExtractedYear,
                    IsYearValid = doc.IsYearValid,
                    IsIdentityValid = doc.IsIdentityValid,
                    Status = doc.Status,
                    CreatedAt = doc.CreatedAt,
                    Items = (itemsList ?? new List<DocumentItem>()).Select(i => new DocumentItemResponseDto
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
            }).ToList();

            return new PagedResult<DocumentReviewResponseDto>
            {
                Items = dtos,
                Pagination = new PaginationMeta
                {
                    Page = query.Page,
                    PageSize = query.Size,
                    TotalItems = totalItems,
                    TotalPages = (int)Math.Ceiling(totalItems / (double)query.Size)
                }
            };
        }

        public async Task<DocumentReviewResponseDto> GetDocumentByIdAsync(Guid userId, Guid periodId, Guid documentId)
        {
            var periodRepo = _unitOfWork.Repository<TaxPeriod>();
            var period = await periodRepo.GetByIdAsync(periodId);

            if (period == null || period.UserId != userId)
            {
                throw new NotFoundException(ErrorMessages.TaxPeriodNotFound);
            }

            var docRepo = _unitOfWork.Repository<Document>();
            var document = await docRepo.GetByIdAsync(documentId);

            if (document == null || document.PeriodId != periodId)
            {
                throw new NotFoundException(ErrorMessages.DocumentNotFound);
            }

            return await MapToReviewDtoAsync(document);
        }

        private async Task<DocumentReviewResponseDto> MapToReviewDtoAsync(Document doc, Dictionary<string, TaxDocumentType>? docTypesDict = null)
        {
            if (docTypesDict == null)
            {
                var docTypeRepo = _unitOfWork.Repository<TaxDocumentType>();
                var allTypes = await docTypeRepo.GetAllAsync();
                docTypesDict = allTypes.ToDictionary(t => t.Code, StringComparer.OrdinalIgnoreCase);
            }

            TaxDocumentType? docType = null;
            if (!string.IsNullOrEmpty(doc.DocTypeCode))
            {
                docTypesDict.TryGetValue(doc.DocTypeCode, out docType);
            }

            var itemRepo = _unitOfWork.Repository<DocumentItem>();
            var items = (await itemRepo.FindAsync(i => i.DocumentId == doc.Id))
                .OrderBy(i => i.ItemOrder)
                .Select(i => new DocumentItemResponseDto
                {
                    Id = i.Id,
                    ItemOrder = i.ItemOrder,
                    ItemName = i.ItemName,
                    Unit = i.Unit,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    TotalPrice = i.TotalPrice
                }).ToList();

            return new DocumentReviewResponseDto
            {
                Id = doc.Id,
                PeriodId = doc.PeriodId,
                DocTypeCode = doc.DocTypeCode,
                DocTypeName = docType?.Name,
                IsTaxEligible = docType?.IsTaxEligible,
                FileUrl = doc.FileUrl,
                OriginalFilename = doc.OriginalFilename,
                InvoiceSeries = doc.InvoiceSeries,
                InvoiceNumber = doc.InvoiceNumber,
                InvoiceDate = doc.InvoiceDate,
                SellerName = doc.SellerName,
                SellerTaxCode = doc.SellerTaxCode,
                SellerAddress = doc.SellerAddress,
                SellerPhone = doc.SellerPhone,
                BuyerName = doc.BuyerName,
                BuyerTaxCode = doc.BuyerTaxCode,
                BuyerIdCard = doc.BuyerIdCard,
                BuyerAddress = doc.BuyerAddress,
                PaymentMethod = doc.PaymentMethod,
                TotalAmount = doc.TotalAmount,
                TotalAmountInWords = doc.TotalAmountInWords,
                LookupUrl = doc.LookupUrl,
                LookupCode = doc.LookupCode,
                ExtractedYear = doc.ExtractedYear,
                IsYearValid = doc.IsYearValid,
                IsIdentityValid = doc.IsIdentityValid,
                Status = doc.Status,
                CreatedAt = doc.CreatedAt,
                Items = items
            };
        }
    }
}