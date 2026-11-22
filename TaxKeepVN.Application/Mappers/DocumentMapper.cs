using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TaxKeepVN.Application.DTOs.Documents;
using TaxKeepVN.Application.DTOs.TaxAI;
using TaxKeepVN.Domain.Entities;

namespace TaxKeepVN.Application.Mappers
{
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public static class DocumentMapper
    {
        /// <summary>
        /// Map Document entity sang UploadedDocumentItemDto (dùng cho upload batch)
        /// </summary>
        public static UploadedDocumentItemDto ToUploadedDto(this Document doc, Guid userId)
        {
            if (doc == null) return null!;

            return new UploadedDocumentItemDto
            {
                Id = doc.Id,
                UserId = userId,
                PeriodId = doc.PeriodId,
                IncomeYear = doc.IncomeYear,
                DocTypeCode = doc.DocTypeCode,
                OriginalFilename = doc.OriginalFilename,
                FileUrl = doc.FileUrl,
                Status = doc.Status,
                CreatedAt = doc.CreatedAt
            };
        }

        /// <summary>
        /// Map DocumentItem entity sang DocumentItemResponseDto
        /// </summary>
        public static DocumentItemResponseDto ToItemDto(this DocumentItem item)
        {
            if (item == null) return null!;

            return new DocumentItemResponseDto
            {
                Id = item.Id,
                ItemOrder = item.ItemOrder,
                ItemName = item.ItemName,
                Unit = item.Unit,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TotalPrice = item.TotalPrice
            };
        }

        /// <summary>
        /// Map InvoiceLineItemDto từ review request sang DocumentItem entity
        /// </summary>
        public static DocumentItem ToEntity(this InvoiceLineItemDto dto, Guid documentId)
        {
            if (dto == null) return null!;

            return new DocumentItem
            {
                DocumentId = documentId,
                ItemOrder = dto.ItemOrder,
                ItemName = dto.ItemName,
                Unit = dto.Unit,
                Quantity = dto.Quantity,
                UnitPrice = dto.UnitPrice,
                TotalPrice = dto.TotalPrice
            };
        }

        /// <summary>
        /// Map Document entity sang DocumentReviewResponseDto kèm thông tin loại chứng từ và danh sách items
        /// </summary>
        public static DocumentReviewResponseDto ToReviewDto(
            this Document doc,
            TaxDocumentType? docType = null,
            IEnumerable<DocumentItem>? items = null)
        {
            if (doc == null) return null!;

            var itemsList = items ?? doc.Items;
            var statementDto = doc.MedicalStatement.ToDto();

            return new DocumentReviewResponseDto
            {
                Id = doc.Id,
                PeriodId = doc.PeriodId,
                DocTypeCode = doc.DocTypeCode,
                MedicalStatement = statementDto,
                DocTypeName = docType?.Name ?? doc.DocType?.Name,
                IsTaxEligible = docType?.IsTaxEligible ?? doc.DocType?.IsTaxEligible,
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
                IncomeYear = doc.IncomeYear,
                IsYearValid = doc.IsYearValid,
                IsIdentityValid = doc.IsIdentityValid,
                ValidationErrors = BuildValidationErrors(doc),
                IsNotReimbursed = doc.IsNotReimbursed,
                Status = doc.Status,
                CreatedAt = doc.CreatedAt,
                Items = itemsList != null
                    ? itemsList.Select(i => i.ToItemDto()).ToList()
                    : new List<DocumentItemResponseDto>(),
                // Đối với WITHHOLDING_VOUCHER: lấy trực tiếp từ thuộc tính chứng từ đã lưu (fallback TotalAmount)
                TaxWithheld = doc.TaxWithheld,
                TotalIncome = doc.TotalIncome,
                InsuranceDeducted = doc.InsuranceDeducted,
                OverallConfidence = doc.OverallConfidence,
                AppliedThreshold = doc.AppliedThreshold,
                IsPassedThreshold = doc.IsPassedThreshold,
            };
        }

        private static List<string> BuildValidationErrors(Document doc)
        {
            var errors = new List<string>();

            // 1. Kiểm tra ngày lập hóa đơn ở tương lai
            bool isFutureDate = doc.InvoiceDate.HasValue && doc.InvoiceDate.Value > DateOnly.FromDateTime(DateTime.UtcNow);
            if (isFutureDate)
            {
                errors.Add("Ngày lập trên hóa đơn không được lớn hơn ngày hiện tại.");
            }
            if (doc.IsYearValid == false)
            {
                errors.Add("Năm trên hóa đơn không khớp với kỳ tính thuế.");
            }
            if (doc.IsIdentityValid == false)
            {
                errors.Add("Thông tin người mua không khớp với người nộp thuế hoặc người phụ thuộc.");
            }
            // Thêm lỗi danh mục không hợp lệ: khi FAILED nhưng identity và year đều null/true
            // -> lý do FAILED là do loại chứng từ không thuộc diện giảm trừ
            if (string.Equals(doc.Status, "FAILED", StringComparison.OrdinalIgnoreCase)
                && !isFutureDate
                && doc.IsYearValid != false
                && doc.IsIdentityValid != false
                && !string.IsNullOrWhiteSpace(doc.DocTypeCode))
            {
                errors.Add($"Loại chứng từ không thuộc diện được giảm trừ thuế TNCN theo quy định.");
            }
            if (string.Equals(doc.Status, "FAILED", StringComparison.OrdinalIgnoreCase) && errors.Count == 0)
            {
                errors.Add("AI không thể xác nhận tính hợp lệ của chứng từ.");
            }
            return errors;
        }

        public static MedicalStatementResponseDto? ToDto(this MedicalStatement? ms)
        {
            if (ms == null) return null;
            return new MedicalStatementResponseDto
            {
                Id = ms.Id,
                DocumentId = ms.DocumentId,
                FileUrl = ms.FileUrl,
                OriginalFilename = ms.OriginalFilename,
                FormCode = ms.FormCode,
                StatementNumber = ms.StatementNumber,
                PatientName = ms.PatientName,
                PatientDob = ms.PatientDob,
                PatientGender = ms.PatientGender,
                PatientIdCard = ms.PatientIdCard,
                InsuranceNumber = ms.InsuranceNumber,
                BenefitRate = ms.BenefitRate,
                HospitalName = ms.HospitalName,
                HospitalCode = ms.HospitalCode,
                Department = ms.Department,
                AdmissionDate = ms.AdmissionDate,
                DischargeDate = ms.DischargeDate,
                TreatmentType = ms.TreatmentType,
                TotalHospitalAmount = ms.TotalHospitalAmount,
                InsurancePaidAmount = ms.InsurancePaidAmount,
                PatientPaidAmount = ms.PatientPaidAmount,
                PatientSelfPayAmount = ms.PatientSelfPayAmount,
                TotalEligibleAmount = ms.TotalEligibleAmount,
                OverallConfidence = ms.OverallConfidence,
                AppliedThreshold = ms.AppliedThreshold,
                IsPassedThreshold = ms.IsPassedThreshold,
                Status = ms.Status,
                ValidationErrors = !string.IsNullOrWhiteSpace(ms.ValidationErrors)
                    ? (JsonSerializer.Deserialize<List<string>>(ms.ValidationErrors) ?? new List<string>())
                    : new List<string>(),
                Lines = ms.Lines?.Select(l => new MedicalStatementLineResponseDto
                {
                    Id = l.Id,
                    LineOrder = l.LineOrder,
                    SectionCode = l.SectionCode,
                    SectionName = l.SectionName,
                    ItemName = l.ItemName,
                    Unit = l.Unit,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    TotalAmount = l.TotalAmount,
                    InsurancePaid = l.InsurancePaid,
                    PatientCoPay = l.PatientCoPay,
                    PatientSelfPay = l.PatientSelfPay,
                    DeductionStatus = l.DeductionStatus,
                    IsEligibleDeduction = l.IsEligibleDeduction,
                    DeductionReason = l.DeductionReason,
                    EligibleAmount = l.EligibleAmount
                }).ToList() ?? new List<MedicalStatementLineResponseDto>()
            };
        }
    }
}
