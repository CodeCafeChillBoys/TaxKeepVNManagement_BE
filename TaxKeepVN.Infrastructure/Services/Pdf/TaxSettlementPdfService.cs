using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using QuestPDF.Fluent;
using TaxKeepVN.Application.DTOs.TaxSettlement;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Domain.IRepositories;
using DocEntity = TaxKeepVN.Domain.Entities.Document;

namespace TaxKeepVN.Infrastructure.Services.Pdf
{
    public class TaxSettlementPdfService : ITaxSettlementPdfService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISystemConfigService _configService;

        public TaxSettlementPdfService(IUnitOfWork unitOfWork, ISystemConfigService configService)
        {
            _unitOfWork = unitOfWork;
            _configService = configService;
        }

        public async Task<TaxSettlementPdfModel> BuildPdfModelAsync(Guid dossierId, Guid userId, TaxSettlementExportPdfRequest? request = null)
        {
            var dossierRepo = _unitOfWork.Repository<TaxSettlementDossier>();
            var userRepo = _unitOfWork.Repository<User>();

            var dossier = await dossierRepo.GetByIdAsync(dossierId)
                ?? throw new NotFoundException($"Không tìm thấy hồ sơ quyết toán '{dossierId}'.");

            if (dossier.TaxpayerId != userId)
                throw new ForbiddenException("Bạn không có quyền truy cập hồ sơ quyết toán này.");

            var user = await userRepo.GetByIdAsync(userId)
                ?? throw new NotFoundException($"Không tìm thấy thông tin người dùng '{userId}'.");

            var taxYear = dossier.TaxYear;

            // 1. Phân rã cấu hình biểu thuế snapshot đã áp dụng khi chốt hồ sơ
            var bracketDetails = new List<ProgressiveBracketDetailDto>();
            if (!string.IsNullOrWhiteSpace(dossier.PitBracketsSnapshot))
            {
                try
                {
                    var brackets = JsonSerializer.Deserialize<List<PitBracketConfigDto>>(dossier.PitBracketsSnapshot) ?? new List<PitBracketConfigDto>();
                    var taxableMonthly = Math.Max(0, dossier.TaxableIncomeMonthly);

                    var applied = brackets
                        .OrderBy(b => b.BracketNo)
                        .Where(b => taxableMonthly > b.FromMonthly && (!b.ToMonthly.HasValue || taxableMonthly <= b.ToMonthly.Value))
                        .FirstOrDefault();

                    bracketDetails = brackets.OrderBy(b => b.BracketNo).Select(b =>
                    {
                        var isApplied = applied != null && b.BracketNo == applied.BracketNo;
                        var taxMonthly = isApplied ? Math.Max(0, Math.Round(taxableMonthly * b.Rate - b.QuickDeduct, 0)) : 0;
                        return new ProgressiveBracketDetailDto
                        {
                            BracketNo = b.BracketNo,
                            FromMonthly = b.FromMonthly,
                            ToMonthly = b.ToMonthly,
                            Rate = b.Rate,
                            QuickDeduct = b.QuickDeduct,
                            TaxableMonthly = isApplied ? taxableMonthly : 0,
                            TaxMonthly = taxMonthly,
                            IsApplied = isApplied
                        };
                    }).ToList();
                }
                catch
                {
                    // Fallback nếu snapshot lỗi
                }
            }

            // 2. Lấy danh sách Người phụ thuộc (Mẫu 02-1/BK-QTT)
            var depRepo = _unitOfWork.Repository<Dependent>();
            var rawDeps = await depRepo.FindAsync(d =>
                d.TaxpayerId == userId &&
                !d.IsDeleted &&
                (d.Status == DependentStatus.ACTIVE || d.IsProfileComplete) &&
                string.Compare(d.EffectiveFromMonth, $"{taxYear}-12", StringComparison.Ordinal) <= 0 &&
                string.Compare(d.EffectiveToMonth, $"{taxYear}-01", StringComparison.Ordinal) >= 0);

            // Mức giảm trừ NPT hàng tháng
            decimal dependentMonthlyRate = 6200000;
            if (!string.IsNullOrWhiteSpace(dossier.DeductionConfigSnapshot))
            {
                try
                {
                    var deductionConfig = JsonSerializer.Deserialize<DeductionConfigSnapshotDto>(dossier.DeductionConfigSnapshot);
                    if (deductionConfig != null && deductionConfig.DependentMonthly > 0)
                    {
                        dependentMonthlyRate = deductionConfig.DependentMonthly;
                    }
                }
                catch { }
            }

            int depIdx = 1;
            var dependentsList = rawDeps.OrderBy(d => d.FullName).Select(d =>
            {
                int startMonth = 1;
                if (!string.IsNullOrEmpty(d.EffectiveFromMonth) && d.EffectiveFromMonth.StartsWith($"{taxYear}-"))
                {
                    int.TryParse(d.EffectiveFromMonth.Split('-')[1], out startMonth);
                }

                int endMonth = 12;
                if (!string.IsNullOrEmpty(d.EffectiveToMonth) && d.EffectiveToMonth.StartsWith($"{taxYear}-"))
                {
                    int.TryParse(d.EffectiveToMonth.Split('-')[1], out endMonth);
                }

                int months = Math.Max(0, endMonth - startMonth + 1);
                var idNum = !string.IsNullOrWhiteSpace(d.CitizenId) 
                    ? d.CitizenId 
                    : (!string.IsNullOrWhiteSpace(d.TaxIdNumber) ? d.TaxIdNumber : d.BirthCertNumber ?? "N/A");

                return new PdfDependentItemDto
                {
                    Index = depIdx++,
                    FullName = d.FullName,
                    DateOfBirth = d.BirthDate.ToString("dd/MM/yyyy"),
                    IdNumber = idNum,
                    Relationship = d.Relationship.ToString(),
                    EligibleMonths = months,
                    DeductionAmount = months * dependentMonthlyRate
                };
            }).ToList();

            // 3. Lấy danh sách Biên lai/Hóa đơn Y tế & Giáo dục đã xác nhận (CONFIRMED)
            var docRepo = _unitOfWork.Repository<DocEntity>();
            var rawDocs = await docRepo.FindAsync(d =>
                d.Period != null &&
                d.Period.UserId == userId &&
                d.Period.TaxYear == (short)taxYear &&
                d.Status == "CONFIRMED" &&
                (d.DocTypeCode == "MEDICAL_RECEIPT" || d.DocTypeCode == "EDUCATION_RECEIPT") &&
                d.TotalAmount != null);

            int docIdx = 1;
            var receiptsList = rawDocs.OrderBy(d => d.InvoiceDate ?? DateOnly.FromDateTime(d.CreatedAt)).Select(d =>
            {
                var category = d.DocTypeCode == "MEDICAL_RECEIPT" ? "Y Tế" : "Giáo Dục";
                var dateStr = d.InvoiceDate.HasValue 
                    ? d.InvoiceDate.Value.ToString("dd/MM/yyyy") 
                    : d.CreatedAt.ToString("dd/MM/yyyy");

                return new PdfReceiptItemDto
                {
                    Index = docIdx++,
                    Category = category,
                    IssuerName = d.SellerName ?? "Đơn vị cung cấp dịch vụ",
                    DocumentNumber = d.InvoiceNumber ?? d.OriginalFilename ?? "N/A",
                    IssueDate = dateStr,
                    TotalAmount = d.TotalAmount ?? 0,
                    TaxEligibleAmount = d.TotalAmount ?? 0
                };
            }).ToList();

            // 4. Nhãn nhóm luật
            string lawGroup = taxYear >= 2026
                ? "Luật 109/2025/QH15 (Biểu thuế 5 bậc, trần Y tế 23tr, Giáo dục 24tr)"
                : "Luật Thuế TNCN sửa đổi (Biểu thuế lũy tiến 7 bậc cũ)";

            // 5. Chuẩn bị model hoàn chỉnh (Ưu tiên Request Body, fallback vào Profile)
            return new TaxSettlementPdfModel
            {
                DossierId = dossier.Id,
                TaxYear = dossier.TaxYear,
                CutoffDate = dossier.CutoffDate,
                LawGroupLabel = lawGroup,

                FullName = user.FullName ?? "Người nộp thuế",
                CitizenId = user.CitizenId ?? "N/A",
                TaxCode = !string.IsNullOrWhiteSpace(request?.TaxCode) ? request.TaxCode : (user.TaxIdNumber ?? string.Empty),
                Address = !string.IsNullOrWhiteSpace(request?.ContactAddress) ? request.ContactAddress : (user.Address ?? "Việt Nam"),
                PhoneNumber = !string.IsNullOrWhiteSpace(request?.PhoneNumber) ? request.PhoneNumber : (user.PhoneNumber ?? string.Empty),
                Email = !string.IsNullOrWhiteSpace(request?.Email) ? request.Email : (user.Email ?? string.Empty),
                TaxOfficeName = !string.IsNullOrWhiteSpace(request?.TaxOfficeName) ? request.TaxOfficeName : "Cơ quan Thuế quản lý trực tiếp",
                BankAccountNumber = !string.IsNullOrWhiteSpace(request?.BankAccountNumber) ? request.BankAccountNumber : string.Empty,
                BankName = !string.IsNullOrWhiteSpace(request?.BankName) ? request.BankName : string.Empty,

                TotalGrossIncome = dossier.TotalGrossIncome,
                TotalTaxWithheld = dossier.TotalTaxWithheld,
                TotalInsuranceDeduction = dossier.TotalInsuranceDeduction,
                PersonalDeductionMonths = dossier.PersonalDeductionMonths,
                PersonalDeductionAmount = dossier.PersonalDeductionAmount,
                DependentDeductionPersonMonths = dossier.DependentDeductionPersonMonths,
                DependentDeductionAmount = dossier.DependentDeductionAmount,
                CharityDeduction = dossier.CharityDeduction,
                MedicalDeduction = dossier.MedicalDeduction,
                EducationDeduction = dossier.EducationDeduction,
                TotalDeductions = dossier.TotalDeductions,
                TaxableIncomeYearly = dossier.TaxableIncomeYearly,
                TaxableIncomeMonthly = dossier.TaxableIncomeMonthly,
                TaxPayable = dossier.TaxPayable,
                RefundAmount = dossier.RefundAmount,
                DueAmount = dossier.DueAmount,
                AppliedBracketNo = dossier.AppliedBracketNo,

                BracketDetails = bracketDetails,
                Dependents = dependentsList,
                Receipts = receiptsList
            };
        }

        public byte[] GenerateSettlementPdf(TaxSettlementPdfModel model)
        {
            var document = new TaxSettlementPdfDocument(model);
            return document.GeneratePdf();
        }
    }
}
