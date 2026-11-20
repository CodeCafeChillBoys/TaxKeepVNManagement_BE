using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using TaxKeepVN.Application.Constants;
using TaxKeepVN.Application.DTOs.MedicalClaims;
using TaxKeepVN.Application.DTOs.TaxAI;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    public class MedicalClaimService : IMedicalClaimService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _fileStorageService;
        private readonly IMedicalRuleEngineService _ruleEngine;
        private readonly IDocumentOcrProducerService? _ocrProducerService;
        private readonly ILogger<MedicalClaimService> _logger;

        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".pdf"
        };

        public MedicalClaimService(
            IUnitOfWork unitOfWork,
            IFileStorageService fileStorageService,
            IMedicalRuleEngineService ruleEngine,
            ILogger<MedicalClaimService> logger,
            IDocumentOcrProducerService? ocrProducerService = null)
        {
            _unitOfWork = unitOfWork;
            _fileStorageService = fileStorageService;
            _ruleEngine = ruleEngine;
            _logger = logger;
            _ocrProducerService = ocrProducerService;
        }

        public async Task<MedicalClaimDto> GetMedicalClaimAsync(Guid userId, Guid periodId, Guid documentId)
        {
            var (receipt, statement) = await ResolveReceiptAndStatementAsync(userId, periodId, documentId);

            var periodRepo = _unitOfWork.Repository<TaxPeriod>();
            var period = await periodRepo.GetByIdAsync(periodId);
            int taxYear = period?.TaxYear ?? DateTime.UtcNow.Year;

            // Chạy Rule Engine để đồng bộ trạng thái mới nhất
            await _ruleEngine.EvaluateClaimAsync(receipt, statement, userId, taxYear);
            await _unitOfWork.SaveChangesAsync();

            return BuildClaimDto(receipt, statement);
        }

        public async Task<MedicalClaimDto> UploadMedicalStatementAsync(Guid userId, Guid periodId, Guid receiptDocumentId, IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                throw new BadRequestException(ErrorCodes.FilesRequired, ErrorMessages.FilesRequired);
            }

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
            {
                throw new BadRequestException(ErrorCodes.UnsupportedFormat, ErrorMessages.UnsupportedFormat);
            }

            if (file.Length > 10 * 1024 * 1024)
            {
                throw new BadRequestException(ErrorCodes.FileSizeExceeded, ErrorMessages.FileSizeExceeded);
            }

            var periodRepo = _unitOfWork.Repository<TaxPeriod>();
            var period = await periodRepo.GetByIdAsync(periodId);
            if (period == null || period.UserId != userId)
            {
                throw new NotFoundException(ErrorMessages.TaxPeriodNotFound);
            }

            var docRepo = _unitOfWork.Repository<Document>();
            var receipt = await docRepo.GetByIdAsync(receiptDocumentId);
            if (receipt == null || receipt.PeriodId != periodId)
            {
                throw new NotFoundException(MedicalErrorMessages.MedicalReceiptNotFound);
            }

            // Lưu file vào storage
            var fileUrl = await _fileStorageService.SaveFileAsync(file, $"tax-periods/{period.TaxYear}/medical-statements");

            // Tạo Document cho Bảng kê chi phí KCB
            var statement = new Document
            {
                Id = Guid.NewGuid(),
                PeriodId = periodId,
                ParentDocumentId = receipt.Id,
                DocTypeCode = MedicalDocTypes.MedicalCostStatement,
                OriginalFilename = file.FileName,
                FileUrl = fileUrl,
                IncomeYear = period.TaxYear,
                Status = "UPLOADED",
                CreatedAt = DateTime.UtcNow,
                IsNotReimbursed = receipt.IsNotReimbursed ?? false
            };

            await docRepo.AddAsync(statement);
            await _unitOfWork.SaveChangesAsync();

            // Kích hoạt OCR AI nếu có service
            if (_ocrProducerService != null)
            {
                try
                {
                    _ocrProducerService.PublishBatchOcrTasks(new[]
                    {
                        new DocumentOcrExtractRequestMessage
                        {
                            TaskId = statement.Id,
                            PeriodId = periodId,
                            UserId = userId,
                            TargetYear = period.TaxYear,
                            FileUrl = statement.FileUrl,
                            OriginalFilename = statement.OriginalFilename ?? string.Empty
                        }
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to publish OCR message for medical statement {StatementId}", statement.Id);
                }
            }

            // Đánh giá rule engine
            await _ruleEngine.EvaluateClaimAsync(receipt, statement, userId, period.TaxYear);
            await _unitOfWork.SaveChangesAsync();

            return BuildClaimDto(receipt, statement);
        }

        public async Task<MedicalClaimDto> LinkMedicalStatementAsync(Guid userId, Guid periodId, Guid receiptDocumentId, Guid statementDocumentId)
        {
            var periodRepo = _unitOfWork.Repository<TaxPeriod>();
            var period = await periodRepo.GetByIdAsync(periodId);
            if (period == null || period.UserId != userId)
            {
                throw new NotFoundException(ErrorMessages.TaxPeriodNotFound);
            }

            var docRepo = _unitOfWork.Repository<Document>();
            var receipt = await docRepo.GetByIdAsync(receiptDocumentId);
            if (receipt == null || receipt.PeriodId != periodId)
            {
                throw new NotFoundException(MedicalErrorMessages.MedicalReceiptNotFound);
            }

            var statement = await docRepo.GetByIdAsync(statementDocumentId);
            if (statement == null || statement.PeriodId != periodId)
            {
                throw new NotFoundException(MedicalErrorMessages.MedicalStatementNotFound);
            }

            if (!string.Equals(statement.DocTypeCode, MedicalDocTypes.MedicalCostStatement, StringComparison.OrdinalIgnoreCase))
            {
                throw new BadRequestException(MedicalErrorCodes.NotMedicalStatement, MedicalErrorMessages.NotMedicalStatement);
            }

            // Gắn liên kết cha - con
            statement.ParentDocumentId = receipt.Id;
            docRepo.Update(statement);

            // Chạy Rule Engine
            await _ruleEngine.EvaluateClaimAsync(receipt, statement, userId, period.TaxYear);
            await _unitOfWork.SaveChangesAsync();

            return BuildClaimDto(receipt, statement);
        }

        public async Task<MedicalClaimDto> UpdateItemDeductionAsync(Guid userId, Guid periodId, Guid documentId, long itemId, UpdateItemDeductionRequestDto request)
        {
            var (receipt, statement) = await ResolveReceiptAndStatementAsync(userId, periodId, documentId);

            var itemRepo = _unitOfWork.Repository<DocumentItem>();
            var items = await itemRepo.FindAsync(i => i.Id == itemId);
            var item = items.FirstOrDefault();
            if (item == null || (item.DocumentId != documentId && item.DocumentId != statement?.Id && item.DocumentId != receipt.Id))
            {
                throw new NotFoundException("Không tìm thấy dòng chi phí KCB.");
            }

            if (!string.IsNullOrWhiteSpace(request.DeductionStatus))
            {
                var upperStatus = request.DeductionStatus.Trim().ToUpperInvariant();
                if (upperStatus != MedicalDeductionStatus.ELIGIBLE &&
                    upperStatus != MedicalDeductionStatus.EXCLUDED &&
                    upperStatus != MedicalDeductionStatus.NEED_REVIEW)
                {
                    throw new BadRequestException("INVALID_STATUS", "Trạng thái giảm trừ không hợp lệ (chỉ chấp nhận ELIGIBLE, EXCLUDED, NEED_REVIEW).");
                }

                item.DeductionStatus = upperStatus;
                item.DeductionReason = MedicalDeductionReason.USER_OVERRIDE;
                item.IsUserOverridden = true;
            }

            if (request.SelfPayOptIn.HasValue)
            {
                item.SelfPayOptIn = request.SelfPayOptIn.Value;
                item.IsUserOverridden = true;
            }

            itemRepo.Update(item);
            await _unitOfWork.SaveChangesAsync();

            // Tái thẩm định hồ sơ y tế
            var periodRepo = _unitOfWork.Repository<TaxPeriod>();
            var period = await periodRepo.GetByIdAsync(periodId);
            int taxYear = period?.TaxYear ?? DateTime.UtcNow.Year;

            await _ruleEngine.EvaluateClaimAsync(receipt, statement, userId, taxYear);
            await _unitOfWork.SaveChangesAsync();

            return BuildClaimDto(receipt, statement);
        }

        public async Task<List<CandidateReceiptDto>> GetCandidateReceiptsForStatementAsync(Guid userId, Guid periodId, Guid statementDocumentId)
        {
            var docRepo = _unitOfWork.Repository<Document>();
            var receipts = await docRepo.FindAsync(d =>
                d.PeriodId == periodId &&
                d.DocTypeCode == MedicalDocTypes.MedicalReceipt &&
                d.Period != null && d.Period.UserId == userId);

            return receipts.Select(r => new CandidateReceiptDto
            {
                DocumentId = r.Id,
                InvoiceSeries = r.InvoiceSeries,
                InvoiceNumber = r.InvoiceNumber,
                InvoiceDate = r.InvoiceDate,
                SellerName = r.SellerName,
                TotalAmount = r.TotalAmount,
                Status = r.Status,
                PatientName = r.PatientName ?? r.BuyerName
            }).ToList();
        }

        private async Task<(Document receipt, Document? statement)> ResolveReceiptAndStatementAsync(Guid userId, Guid periodId, Guid documentId)
        {
            var periodRepo = _unitOfWork.Repository<TaxPeriod>();
            var period = await periodRepo.GetByIdAsync(periodId);
            if (period == null || period.UserId != userId)
            {
                throw new NotFoundException(ErrorMessages.TaxPeriodNotFound);
            }

            var docRepo = _unitOfWork.Repository<Document>();
            var targetDoc = await docRepo.GetByIdAsync(documentId);
            if (targetDoc == null || targetDoc.PeriodId != periodId)
            {
                throw new NotFoundException(ErrorMessages.DocumentNotFound);
            }

            Document receipt;
            Document? statement = null;

            if (string.Equals(targetDoc.DocTypeCode, MedicalDocTypes.MedicalCostStatement, StringComparison.OrdinalIgnoreCase))
            {
                statement = targetDoc;
                if (statement.ParentDocumentId.HasValue)
                {
                    receipt = await docRepo.GetByIdAsync(statement.ParentDocumentId.Value) ?? statement;
                }
                else
                {
                    receipt = statement;
                }
            }
            else
            {
                receipt = targetDoc;
                // Tìm bảng kê con nếu có
                var childStatements = await docRepo.FindAsync(d => d.ParentDocumentId == receipt.Id && d.DocTypeCode == MedicalDocTypes.MedicalCostStatement);
                statement = childStatements.FirstOrDefault();
            }

            // Load items cho statement
            if (statement != null)
            {
                var itemRepo = _unitOfWork.Repository<DocumentItem>();
                var items = await itemRepo.FindAsync(i => i.DocumentId == statement.Id);
                statement.Items = items.OrderBy(i => i.ItemOrder).ToList();
            }

            return (receipt, statement);
        }

        private static MedicalClaimDto BuildClaimDto(Document receipt, Document? statement)
        {
            var items = (statement?.Items ?? receipt.Items ?? new List<DocumentItem>())
                .Select(i => new MedicalClaimItemDto
                {
                    Id = i.Id,
                    ItemOrder = i.ItemOrder,
                    ItemName = i.ItemName,
                    Unit = i.Unit,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    TotalPrice = i.TotalPrice,
                    SectionIndex = i.SectionIndex,
                    HasInsuranceCard = i.HasInsuranceCard,
                    BenefitRate = i.BenefitRate,
                    GroupCode = i.GroupCode,
                    GroupName = i.GroupName,
                    IsPackageSubLine = i.IsPackageSubLine,
                    UnitPriceInsurance = i.UnitPriceInsurance,
                    ServiceRate = i.ServiceRate,
                    HospitalAmount = i.HospitalAmount,
                    InsuranceRate = i.InsuranceRate,
                    InsuranceAmount = i.InsuranceAmount,
                    InsuranceFundPaid = i.InsuranceFundPaid,
                    PatientCoPay = i.PatientCoPay,
                    OtherSourcePaid = i.OtherSourcePaid,
                    PatientSelfPay = i.PatientSelfPay,
                    DeductionStatus = i.DeductionStatus,
                    DeductionReason = i.DeductionReason,
                    EligibleAmount = i.EligibleAmount,
                    SelfPayAmount = i.SelfPayAmount,
                    SelfPayReason = i.SelfPayReason,
                    SelfPayOptIn = i.SelfPayOptIn,
                    IsUserOverridden = i.IsUserOverridden
                }).ToList();

            var warnings = new List<string>();
            if (receipt.MedicalClaimStatus == MedicalClaimStatus.MISSING_STATEMENT)
            {
                warnings.Add("Chưa có Bảng kê chi phí KCB kèm theo hóa đơn. Theo Nghị định 253/2026/NĐ-CP, hồ sơ y tế bắt buộc phải có Bảng kê mới được giảm trừ.");
            }
            else if (receipt.MedicalClaimStatus == MedicalClaimStatus.NEED_REVIEW)
            {
                warnings.Add("Hồ sơ còn các khoản chi phí hoặc thông tin cần người dùng kiểm tra và xác nhận.");
            }

            var summary = new MedicalStatementSummaryDto
            {
                TotalHospitalAmount = items.Sum(i => i.HospitalAmount ?? i.TotalPrice),
                InsuranceFundPaid = items.Sum(i => i.InsuranceFundPaid ?? 0m),
                CoPayWithinInsurance = items.Where(i => i.DeductionStatus == MedicalDeductionStatus.ELIGIBLE).Sum(i => i.PatientCoPay ?? 0m),
                OtherPatientPayments = items.Sum(i => i.PatientSelfPay ?? 0m),
                OtherSourcePaid = items.Sum(i => i.OtherSourcePaid ?? 0m)
            };

            return new MedicalClaimDto
            {
                ReceiptDocumentId = receipt.Id,
                ReceiptStatus = receipt.Status,
                InvoiceSeries = receipt.InvoiceSeries,
                InvoiceNumber = receipt.InvoiceNumber,
                InvoiceDate = receipt.InvoiceDate,
                SellerName = receipt.SellerName,
                TotalAmount = receipt.TotalAmount,
                IsNotReimbursed = receipt.IsNotReimbursed,

                StatementDocumentId = statement?.Id,
                StatementOriginalFilename = statement?.OriginalFilename,
                StatementFileUrl = statement?.FileUrl,

                PatientName = statement?.PatientName ?? receipt.PatientName,
                PatientDependentId = statement?.PatientDependentId ?? receipt.PatientDependentId,
                FacilityName = statement?.FacilityName ?? receipt.FacilityName ?? receipt.SellerName,
                HasInsuranceCard = statement?.HasInsuranceCard ?? receipt.HasInsuranceCard,
                InsuranceBenefitRate = statement?.InsuranceBenefitRate ?? receipt.InsuranceBenefitRate,
                ServiceDate = statement?.ServiceDate ?? receipt.ServiceDate ?? receipt.InvoiceDate,

                EligibleAmount = receipt.EligibleAmount ?? 0m,
                MedicalClaimStatus = receipt.MedicalClaimStatus ?? MedicalClaimStatus.MISSING_STATEMENT,
                Warnings = warnings,
                Summary = summary,
                Items = items
            };
        }
    }
}
