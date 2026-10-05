using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.TaxSettlement;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    /// <summary>
    /// Engine tính toán quyết toán thuế TNCN.
    /// ZERO-HARDCODE: Toàn bộ tham số (mức giảm trừ, biểu thuế, tỷ lệ bảo hiểm)
    /// đọc động từ bảng system_configs qua ISystemConfigService.
    /// Admin có thể điều chỉnh mọi con số mà không cần build lại code.
    /// </summary>
    public class TaxSettlementService : ITaxSettlementService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISystemConfigService _configService;

        // ── Tên key cấu hình trong system_configs ────────────────────────────────
        // Cấu trúc: {PREFIX}_{YEAR_SUFFIX} — suffix là "2025" (≤2025) hoặc "2026" (≥2026)
        private const string KeyNewLawFromYear       = "PIT_NEW_LAW_FROM_YEAR";
        private const string KeyPersonalMonthly      = "PIT_DEDUCTION_PERSONAL_MONTHLY_{0}";
        private const string KeyDependentMonthly     = "PIT_DEDUCTION_DEPENDENT_MONTHLY_{0}";
        private const string KeyBracketsJson         = "PIT_BRACKETS_JSON_{0}";
        private const string KeyBhxhRate             = "PIT_INSURANCE_BHXH_RATE";
        private const string KeyBhytRate             = "PIT_INSURANCE_BHYT_RATE";
        private const string KeyBhtnRate             = "PIT_INSURANCE_BHTN_RATE";
        private const string KeySmallExemption       = "PIT_SMALL_AMOUNT_EXEMPTION";
        // Chi phí y tế và giáo dục — chỉ áp dụng từ năm 2026 (Luật 109/2025/QH15)
        private const string KeyMedicalMaxYearly     = "PIT_MEDICAL_MAX_YEARLY";
        private const string KeyEducationMaxYearly   = "PIT_EDUCATION_MAX_YEARLY";
        private const string DocTypeMedical          = "MEDICAL_RECEIPT";
        private const string DocTypeEducation        = "EDUCATION_RECEIPT";

        public TaxSettlementService(IUnitOfWork unitOfWork, ISystemConfigService configService)
        {
            _unitOfWork = unitOfWork;
            _configService = configService;
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  PUBLIC METHODS
        // ══════════════════════════════════════════════════════════════════════════

        public async Task<TaxSettlementPreviewDto> PreviewAsync(Guid taxpayerId, TaxSettlementPreviewRequest request)
        {
            var cutoff = ParseCutoffDate(request.CutoffDate, request.TaxYear);
            var config = await LoadConfigAsync(request.TaxYear);
            var incomes = await LoadIncomesAsync(taxpayerId, request.TaxYear, cutoff);
            var dependents = await LoadValidDependentsAsync(taxpayerId, request.TaxYear, cutoff);
            var (medicalDed, educationDed) = await LoadReceiptDeductionsAsync(taxpayerId, request.TaxYear, config);

            return CalculatePreview(
                dossierId: null,
                taxpayerId: taxpayerId,
                taxYear: request.TaxYear,
                cutoff: cutoff,
                config: config,
                incomes: incomes,
                dependents: dependents,
                charityDeduction: request.CharityDeduction,
                medicalDeduction: medicalDed,
                educationDeduction: educationDed,
                status: "DRAFT",
                selectedIds: null);
        }

        public async Task<TaxSettlementPreviewDto> ExportAsync(Guid taxpayerId, TaxSettlementExportRequest request)
        {
            var cutoff = ParseCutoffDate(request.CutoffDate, request.TaxYear);
            var config = await LoadConfigAsync(request.TaxYear);
            var allIncomes = await LoadIncomesAsync(taxpayerId, request.TaxYear, cutoff);
            var dependents = await LoadValidDependentsAsync(taxpayerId, request.TaxYear, cutoff);
            var (medicalDed, educationDed) = await LoadReceiptDeductionsAsync(taxpayerId, request.TaxYear, config);

            // Lọc income theo selectedIds (nếu không truyền → chọn tất cả)
            var selectedIds = request.SelectedIncomeSourceIds;

            // Tính toán chính thức
            var preview = CalculatePreview(
                dossierId: null,
                taxpayerId: taxpayerId,
                taxYear: request.TaxYear,
                cutoff: cutoff,
                config: config,
                incomes: allIncomes,
                dependents: dependents,
                charityDeduction: request.CharityDeduction,
                medicalDeduction: medicalDed,
                educationDeduction: educationDed,
                status: "LOCKED",
                selectedIds: selectedIds);

            // Lưu hồ sơ vào DB
            var dossierId = await PersistDossierAsync(taxpayerId, preview, config, request.Note);
            preview.DossierId = dossierId;
            preview.Status = "LOCKED";

            return preview;
        }

        public async Task<List<TaxSettlementListItemDto>> GetListAsync(Guid taxpayerId)
        {
            var repo = _unitOfWork.Repository<TaxSettlementDossier>();
            var dossiers = (await repo.FindAsync(d => d.TaxpayerId == taxpayerId))
                .OrderByDescending(d => d.TaxYear)
                .ThenByDescending(d => d.CreatedAt)
                .ToList();

            return dossiers.Select(d => new TaxSettlementListItemDto
            {
                Id = d.Id,
                TaxYear = d.TaxYear,
                CutoffDate = d.CutoffDate.ToString("yyyy-MM-dd"),
                TaxPayable = d.TaxPayable,
                RefundAmount = d.RefundAmount,
                DueAmount = d.DueAmount,
                Status = d.Status,
                LawGroupLabel = BuildLawGroupLabel(d.TaxYear, GetLawSuffix(d.TaxYear, 2026)),
                CreatedAt = d.CreatedAt,
                LockedAt = d.LockedAt
            }).ToList();
        }

        public async Task<TaxSettlementPreviewDto> GetByIdAsync(Guid dossierId, Guid taxpayerId)
        {
            var repo = _unitOfWork.Repository<TaxSettlementDossier>();
            var dossier = await repo.GetByIdAsync(dossierId)
                ?? throw new NotFoundException($"Không tìm thấy hồ sơ quyết toán '{dossierId}'.");

            if (dossier.TaxpayerId != taxpayerId)
                throw new ForbiddenException("Bạn không có quyền xem hồ sơ này.");

            var config = await LoadConfigAsync(dossier.TaxYear);

            // Tái tạo preview từ snapshot đã lưu
            var dto = BuildPreviewFromDossier(dossier, config);
            return dto;
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  PRIVATE — Config Loading
        // ══════════════════════════════════════════════════════════════════════════

        private async Task<TaxCalcConfig> LoadConfigAsync(int taxYear)
        {
            // Xác định nhóm luật: "2026" hoặc "2025"
            var newLawFromYearStr = await _configService.GetRequiredConfigValueAsync(KeyNewLawFromYear);
            var newLawFromYear = int.Parse(newLawFromYearStr);
            var suffix = taxYear >= newLawFromYear ? newLawFromYear.ToString() : (newLawFromYear - 1).ToString();

            // Đọc các tham số mức giảm trừ
            var personalMonthlyStr = await _configService.GetRequiredConfigValueAsync(string.Format(KeyPersonalMonthly, suffix));
            var dependentMonthlyStr = await _configService.GetRequiredConfigValueAsync(string.Format(KeyDependentMonthly, suffix));
            var bracketsJson = await _configService.GetRequiredConfigValueAsync(string.Format(KeyBracketsJson, suffix));
            var bhxhRateStr = await _configService.GetRequiredConfigValueAsync(KeyBhxhRate);
            var bhytRateStr = await _configService.GetRequiredConfigValueAsync(KeyBhytRate);
            var bhtnRateStr = await _configService.GetRequiredConfigValueAsync(KeyBhtnRate);
            var smallExemptionStr = await _configService.GetRequiredConfigValueAsync(KeySmallExemption);

            // Giới hạn chi phí y tế & giáo dục (chỉ load nếu năm >= 2026)
            decimal medicalMaxYearly = 0, educationMaxYearly = 0;
            if (taxYear >= 2026)
            {
                var medicalMaxStr = await _configService.GetRequiredConfigValueAsync(KeyMedicalMaxYearly);
                var educationMaxStr = await _configService.GetRequiredConfigValueAsync(KeyEducationMaxYearly);
                medicalMaxYearly = decimal.Parse(medicalMaxStr);
                educationMaxYearly = decimal.Parse(educationMaxStr);
            }

            // Parse biểu thuế JSON — số bậc do Admin cấu hình (5 hay 7 hay bao nhiêu cũng được)
            var brackets = JsonSerializer.Deserialize<List<PitBracketConfigDto>>(bracketsJson)
                ?? throw new InvalidOperationException($"Cấu hình '{string.Format(KeyBracketsJson, suffix)}' không hợp lệ.");

            return new TaxCalcConfig
            {
                LawSuffix = suffix,
                TaxYear = taxYear,
                PersonalMonthly = decimal.Parse(personalMonthlyStr),
                DependentMonthly = decimal.Parse(dependentMonthlyStr),
                Brackets = brackets.OrderBy(b => b.BracketNo).ToList(),
                BhxhRate = decimal.Parse(bhxhRateStr),
                BhytRate = decimal.Parse(bhytRateStr),
                BhtnRate = decimal.Parse(bhtnRateStr),
                SmallAmountExemption = decimal.Parse(smallExemptionStr),
                MedicalMaxYearly = medicalMaxYearly,
                EducationMaxYearly = educationMaxYearly
            };
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  PRIVATE — Data Loading
        // ══════════════════════════════════════════════════════════════════════════

        private async Task<List<IncomeSource>> LoadIncomesAsync(Guid taxpayerId, int taxYear, DateOnly cutoff)
        {
            var repo = _unitOfWork.Repository<IncomeSource>();
            var incomes = await repo.FindAsync(i =>
                i.TaxpayerId == taxpayerId &&
                i.TaxYear == taxYear &&
                i.IsActive);
            return incomes.ToList();
        }

        private async Task<List<Dependent>> LoadValidDependentsAsync(Guid taxpayerId, int taxYear, DateOnly cutoff)
        {
            var repo = _unitOfWork.Repository<Dependent>();
            var yearEnd = $"{taxYear}-12";
            var yearStart = $"{taxYear}-01";

            // Bước 1: Query DB lọc theo taxpayerId, chưa xóa và trạng thái hợp lệ (EF Core dịch được 100%)
            var deps = await repo.FindAsync(d =>
                d.TaxpayerId == taxpayerId &&
                !d.IsDeleted &&
                (d.Status == DependentStatus.ACTIVE || d.IsProfileComplete));

            // Bước 2: Lọc thời gian hiệu lực giao nhau với năm tính thuế trong bộ nhớ (in-memory)
            return deps
                .Where(d =>
                    (string.IsNullOrEmpty(d.EffectiveFromMonth) || string.Compare(d.EffectiveFromMonth, yearEnd, StringComparison.Ordinal) <= 0) &&
                    (string.IsNullOrEmpty(d.EffectiveToMonth) || string.Compare(d.EffectiveToMonth, yearStart, StringComparison.Ordinal) >= 0))
                .ToList();
        }

        /// <summary>
        /// Đọc tổng số tiền biên lai y tế (MEDICAL_RECEIPT) và giáo dục (EDUCATION_RECEIPT)
        /// đã được xác nhận (Status=CONFIRMED) trong năm quyết toán.
        /// Tham chiếu: Biện pháp giảm trừ thu TNCN theo Luật 109/2025/QH15 (từ năm 2026)
        /// </summary>
        private async Task<(decimal medical, decimal education)> LoadReceiptDeductionsAsync(
            Guid userId, int taxYear, TaxCalcConfig config)
        {
            // Chỉ áp dụng từ năm 2026 trở đi
            if (taxYear < 2026)
                return (0, 0);

            var docRepo = _unitOfWork.Repository<Document>();

            // Lấy tất cả biên lai đã xác nhận (CONFIRMED) của user trong năm quyết toán
            // biên lai thuộc TaxPeriod của user (join qua Period.UserId == userId AND Period.TaxYear == taxYear)
            var docs = await docRepo.FindAsync(d =>
                d.Period != null &&
                d.Period.UserId == userId &&
                d.Period.TaxYear == (short)taxYear &&
                d.Status == "CONFIRMED" &&
                (d.DocTypeCode == DocTypeMedical || d.DocTypeCode == DocTypeEducation) &&
                d.TotalAmount != null);

            var medicalTotal = docs
                .Where(d => d.DocTypeCode == DocTypeMedical)
                .Sum(d => d.TotalAmount ?? 0);

            var educationTotal = docs
                .Where(d => d.DocTypeCode == DocTypeEducation)
                .Sum(d => d.TotalAmount ?? 0);

            // Cap theo giới hạn luật (lấy từ config, không hardcode)
            var medicalCapped = config.MedicalMaxYearly > 0
                ? Math.Min(medicalTotal, config.MedicalMaxYearly)
                : medicalTotal;

            var educationCapped = config.EducationMaxYearly > 0
                ? Math.Min(educationTotal, config.EducationMaxYearly)
                : educationTotal;

            return (medicalCapped, educationCapped);
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  PRIVATE — Core Calculation Engine (Zero-Hardcode)
        // ══════════════════════════════════════════════════════════════════════════

        private TaxSettlementPreviewDto CalculatePreview(
            Guid? dossierId,
            Guid taxpayerId,
            int taxYear,
            DateOnly cutoff,
            TaxCalcConfig config,
            List<IncomeSource> incomes,
            List<Dependent> dependents,
            decimal charityDeduction,
            decimal medicalDeduction,
            decimal educationDeduction,
            string status,
            List<Guid>? selectedIds)
        {
            // ── Bước 1: Gom thu nhập ──────────────────────────────────────────────
            var incomeItems = incomes.Select(i =>
            {
                var isSelected = selectedIds == null || selectedIds.Contains(i.Id);
                // Tiền bảo hiểm lấy thực tế từ chứng từ (Mục 14b) thay vì tự tính 10.5%
                var insurance = i.InsuranceDeducted;
                return new SettlementIncomeItemDto
                {
                    IncomeSourceId = i.Id,
                    CompanyName = i.CompanyName,
                    CompanyTaxCode = i.CompanyTaxCode,
                    GrossIncome = i.TotalIncome,
                    TaxWithheld = i.TaxWithheld,
                    InsuranceDeduction = insurance,
                    IsSelected = isSelected
                };
            }).ToList();

            var selectedItems = incomeItems.Where(i => i.IsSelected).ToList();
            var totalGross      = selectedItems.Sum(i => i.GrossIncome);
            var totalWithheld   = selectedItems.Sum(i => i.TaxWithheld);
            var totalInsurance  = selectedItems.Sum(i => i.InsuranceDeduction);

            // ── Bước 2: Tính giảm trừ bản thân ──────────────────────────────────
            // Số tháng = từ tháng 1 đến tháng chốt trong năm đó
            var personalMonths = cutoff.Month;
            var personalAmount = personalMonths * config.PersonalMonthly;

            // ── Bước 3: Tính giảm trừ người phụ thuộc ───────────────────────────
            var depItems = dependents.Select(d =>
            {
                var validMonths = CalcDependentValidMonths(d, taxYear, cutoff);
                var amount = validMonths * config.DependentMonthly;
                return new SettlementDependentItemDto
                {
                    DependentId = d.Id,
                    FullName = d.FullName,
                    Relationship = d.Relationship.ToString(),
                    ValidMonths = validMonths,
                    DeductionAmount = amount,
                    EffectiveFromMonth = d.EffectiveFromMonth,
                    EffectiveToMonth = d.EffectiveToMonth
                };
            }).ToList();

            var totalDepPersonMonths = depItems.Sum(d => d.ValidMonths);
            var totalDepAmount = depItems.Sum(d => d.DeductionAmount);

            // ── Bước 4: Tổng giảm trừ & Thu nhập tính thuế ──────────────────────
            // medicalDeduction và educationDeduction đã được cap trong LoadReceiptDeductionsAsync
            var totalDeductions = totalInsurance + personalAmount + totalDepAmount + charityDeduction
                + medicalDeduction + educationDeduction;

            // Bổ sung chặn giá trị âm: Thu nhập tính thuế/tháng = max(0, Tổng thu nhập - Tổng giảm trừ) / 12
            // Nếu Tổng giảm trừ > Tổng thu nhập (do NPT hoặc từ thiện nhiều), TNTT = 0, tránh lỗi logic áp biểu lũy tiến
            var taxableYearly   = Math.Max(0, totalGross - totalDeductions);
            var taxableMonthly  = Math.Max(0, Math.Round(taxableYearly / 12, 0));

            // ── Bước 5: Áp biểu thuế lũy tiến từng phần (đọc động từ config) ────
            var (taxPayableMonthly, appliedBracket, bracketDetails) = ApplyProgressiveBrackets(taxableMonthly, config.Brackets);
            var taxPayableYearly = Math.Round(taxPayableMonthly * 12, 0);

            // ── Bước 6: So sánh với thuế đã tạm nộp ──────────────────────────────
            var netResult = taxPayableYearly - totalWithheld;
            decimal refundAmount = 0, dueAmount = 0;
            if (netResult < 0)
            {
                refundAmount = Math.Abs(netResult);
            }
            else if (netResult > 0)
            {
                // Áp dụng quy tắc miễn phạt nhỏ (đọc từ config, không hardcode 50.000)
                dueAmount = netResult <= config.SmallAmountExemption ? 0 : netResult;
            }

            // ── Tạo message tóm tắt ──────────────────────────────────────────────
            var summary = BuildSummaryMessage(taxPayableYearly, totalWithheld, refundAmount, dueAmount, config.SmallAmountExemption);

            var lawSuffix = config.LawSuffix;
            var lawLabel = BuildLawGroupLabel(taxYear, lawSuffix);

            // Snapshot để lưu audit trail
            var bracketsSnapshot = JsonSerializer.Serialize(config.Brackets);
            var deductionSnapshot = JsonSerializer.Serialize(new DeductionConfigSnapshotDto
            {
                PersonalMonthly = config.PersonalMonthly,
                DependentMonthly = config.DependentMonthly,
                BhxhRate = config.BhxhRate,
                BhytRate = config.BhytRate,
                BhtnRate = config.BhtnRate,
                SmallAmountExemption = config.SmallAmountExemption,
                MedicalMaxYearly = config.MedicalMaxYearly,
                EducationMaxYearly = config.EducationMaxYearly,
                LawGroup = lawSuffix
            });

            return new TaxSettlementPreviewDto
            {
                DossierId = dossierId,
                TaxYear = taxYear,
                CutoffDate = cutoff.ToString("yyyy-MM-dd"),
                LawGroupLabel = lawLabel,

                IncomeItems = incomeItems,
                TotalGrossIncome = totalGross,
                TotalTaxWithheld = totalWithheld,
                TotalInsuranceDeduction = totalInsurance,

                DependentItems = depItems,

                PersonalDeductionMonthlyRate = config.PersonalMonthly,
                PersonalDeductionMonths = personalMonths,
                PersonalDeductionAmount = personalAmount,
                DependentMonthlyRate = config.DependentMonthly,
                DependentDeductionPersonMonths = totalDepPersonMonths,
                DependentDeductionAmount = totalDepAmount,
                CharityDeduction = charityDeduction,
                MedicalDeduction = medicalDeduction,
                EducationDeduction = educationDeduction,
                TotalDeductions = totalDeductions,

                TaxableIncomeYearly = taxableYearly,
                TaxableIncomeMonthly = taxableMonthly,
                BracketDetails = bracketDetails,
                AppliedBracketNo = appliedBracket,
                TaxPayableMonthly = taxPayableMonthly,
                TaxPayable = taxPayableYearly,

                RefundAmount = refundAmount,
                DueAmount = dueAmount,
                SummaryMessage = summary,
                Status = status
            };
        }

        /// <summary>
        /// Áp biểu thuế lũy tiến từng phần theo tháng.
        /// Dùng công thức rút gọn: Thuế/tháng = TNTT/tháng × Rate - QuickDeduct
        /// Số bậc (5 hay 7) do Admin cấu hình, code không biết cứng.
        /// </summary>
        private (decimal taxMonthly, int bracketNo, List<BracketCalculationDetailDto> details)
            ApplyProgressiveBrackets(decimal taxableMonthly, List<PitBracketConfigDto> brackets)
        {
            var details = brackets.Select(b => new BracketCalculationDetailDto
            {
                BracketNo = b.BracketNo,
                FromMonthly = b.FromMonthly,
                ToMonthly = b.ToMonthly,
                Rate = b.Rate,
                TaxableMonthly = 0,
                TaxMonthly = 0,
                IsApplied = false
            }).ToList();

            if (taxableMonthly <= 0) return (0, 0, details);

            // Tìm bậc phù hợp (bậc cao nhất mà TNTT/tháng > From)
            var appliedBracket = brackets
                .Where(b => taxableMonthly > b.FromMonthly)
                .OrderByDescending(b => b.BracketNo)
                .FirstOrDefault();

            if (appliedBracket == null) return (0, 0, details);

            // Áp công thức rút gọn: Tax = TNTT × Rate - QuickDeduct
            var taxMonthly = Math.Max(0, Math.Round(taxableMonthly * appliedBracket.Rate - appliedBracket.QuickDeduct, 0));

            // Cập nhật detail cho bậc áp dụng
            var detail = details.First(d => d.BracketNo == appliedBracket.BracketNo);
            detail.TaxableMonthly = taxableMonthly;
            detail.TaxMonthly = taxMonthly;
            detail.IsApplied = true;

            return (taxMonthly, appliedBracket.BracketNo, details);
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  PRIVATE — Persist & Helpers
        // ══════════════════════════════════════════════════════════════════════════

        private async Task<Guid> PersistDossierAsync(Guid taxpayerId, TaxSettlementPreviewDto preview, TaxCalcConfig config, string? note)
        {
            var repo = _unitOfWork.Repository<TaxSettlementDossier>();

            var dossier = new TaxSettlementDossier
            {
                Id = Guid.NewGuid(),
                TaxpayerId = taxpayerId,
                TaxYear = preview.TaxYear,
                CutoffDate = DateOnly.Parse(preview.CutoffDate),
                TotalGrossIncome = preview.TotalGrossIncome,
                TotalTaxWithheld = preview.TotalTaxWithheld,
                TotalInsuranceDeduction = preview.TotalInsuranceDeduction,
                PersonalDeductionMonths = preview.PersonalDeductionMonths,
                PersonalDeductionAmount = preview.PersonalDeductionAmount,
                DependentDeductionPersonMonths = preview.DependentDeductionPersonMonths,
                DependentDeductionAmount = preview.DependentDeductionAmount,
                CharityDeduction = preview.CharityDeduction,
                MedicalDeduction = preview.MedicalDeduction,
                EducationDeduction = preview.EducationDeduction,
                TotalDeductions = preview.TotalDeductions,
                TaxableIncomeYearly = preview.TaxableIncomeYearly,
                TaxableIncomeMonthly = preview.TaxableIncomeMonthly,
                TaxPayable = preview.TaxPayable,
                RefundAmount = preview.RefundAmount,
                DueAmount = preview.DueAmount,
                AppliedBracketNo = preview.AppliedBracketNo,
                PitBracketsSnapshot = JsonSerializer.Serialize(config.Brackets),
                DeductionConfigSnapshot = JsonSerializer.Serialize(new DeductionConfigSnapshotDto
                {
                    PersonalMonthly = config.PersonalMonthly,
                    DependentMonthly = config.DependentMonthly,
                    BhxhRate = config.BhxhRate,
                    BhytRate = config.BhytRate,
                    BhtnRate = config.BhtnRate,
                    SmallAmountExemption = config.SmallAmountExemption,
                    MedicalMaxYearly = config.MedicalMaxYearly,
                    EducationMaxYearly = config.EducationMaxYearly,
                    LawGroup = config.LawSuffix
                }),
                Status = "LOCKED",
                Note = note,
                CreatedAt = DateTime.UtcNow,
                LockedAt = DateTime.UtcNow
            };

            // Lưu các income items đã chọn
            dossier.IncomeItems = preview.IncomeItems.Select(i => new TaxSettlementIncomeItem
            {
                Id = Guid.NewGuid(),
                DossierId = dossier.Id,
                IncomeSourceId = i.IncomeSourceId,
                CompanyName = i.CompanyName,
                CompanyTaxCode = i.CompanyTaxCode,
                GrossIncome = i.GrossIncome,
                TaxWithheld = i.TaxWithheld,
                InsuranceDeduction = i.InsuranceDeduction,
                IsSelected = i.IsSelected
            }).ToList();

            await repo.AddAsync(dossier);
            await _unitOfWork.SaveChangesAsync();

            return dossier.Id;
        }

        private TaxSettlementPreviewDto BuildPreviewFromDossier(TaxSettlementDossier d, TaxCalcConfig config)
        {
            var brackets = d.PitBracketsSnapshot != null
                ? JsonSerializer.Deserialize<List<PitBracketConfigDto>>(d.PitBracketsSnapshot) ?? config.Brackets
                : config.Brackets;

            var (_, appliedNo, bracketDetails) = ApplyProgressiveBrackets(Math.Max(0, d.TaxableIncomeMonthly), brackets);

            return new TaxSettlementPreviewDto
            {
                DossierId = d.Id,
                TaxYear = d.TaxYear,
                CutoffDate = d.CutoffDate.ToString("yyyy-MM-dd"),
                LawGroupLabel = BuildLawGroupLabel(d.TaxYear, config.LawSuffix),
                IncomeItems = d.IncomeItems.Select(i => new SettlementIncomeItemDto
                {
                    IncomeSourceId = i.IncomeSourceId,
                    CompanyName = i.CompanyName,
                    CompanyTaxCode = i.CompanyTaxCode,
                    GrossIncome = i.GrossIncome,
                    TaxWithheld = i.TaxWithheld,
                    InsuranceDeduction = i.InsuranceDeduction,
                    IsSelected = i.IsSelected
                }).ToList(),
                TotalGrossIncome = d.TotalGrossIncome,
                TotalTaxWithheld = d.TotalTaxWithheld,
                TotalInsuranceDeduction = d.TotalInsuranceDeduction,
                PersonalDeductionMonthlyRate = config.PersonalMonthly,
                PersonalDeductionMonths = d.PersonalDeductionMonths,
                PersonalDeductionAmount = d.PersonalDeductionAmount,
                DependentMonthlyRate = config.DependentMonthly,
                DependentDeductionPersonMonths = d.DependentDeductionPersonMonths,
                DependentDeductionAmount = d.DependentDeductionAmount,
                CharityDeduction = d.CharityDeduction,
                MedicalDeduction = d.MedicalDeduction,
                EducationDeduction = d.EducationDeduction,
                TotalDeductions = d.TotalDeductions,
                TaxableIncomeYearly = d.TaxableIncomeYearly,
                TaxableIncomeMonthly = d.TaxableIncomeMonthly,
                BracketDetails = bracketDetails,
                AppliedBracketNo = d.AppliedBracketNo,
                TaxPayableMonthly = d.TaxableIncomeMonthly > 0 ? Math.Round(d.TaxPayable / 12, 0) : 0,
                TaxPayable = d.TaxPayable,
                RefundAmount = d.RefundAmount,
                DueAmount = d.DueAmount,
                SummaryMessage = BuildSummaryMessage(d.TaxPayable, d.TotalTaxWithheld, d.RefundAmount, d.DueAmount, config.SmallAmountExemption),
                Status = d.Status
            };
        }

        private static int CalcDependentValidMonths(Dependent dep, int taxYear, DateOnly cutoff)
        {
            // Chuyển EffectiveFromMonth / ToMonth (format YYYY-MM) sang số tháng trong năm
            var fromParts = dep.EffectiveFromMonth.Split('-');
            var toParts = dep.EffectiveToMonth.Split('-');
            if (fromParts.Length < 2 || toParts.Length < 2) return 0;

            if (!int.TryParse(fromParts[0], out int fromYear) || !int.TryParse(fromParts[1], out int fromMonth)) return 0;
            if (!int.TryParse(toParts[0], out int toYear) || !int.TryParse(toParts[1], out int toMonth)) return 0;

            // Chuẩn hóa về phạm vi năm tính thuế
            int effectiveFrom = (fromYear < taxYear) ? 1 : (fromYear == taxYear ? fromMonth : 13);
            int effectiveTo = (toYear > taxYear) ? 12 : (toYear == taxYear ? toMonth : 0);

            // Giới hạn theo ngày chốt
            effectiveTo = Math.Min(effectiveTo, cutoff.Month);

            return Math.Max(0, effectiveTo - effectiveFrom + 1);
        }

        private static DateOnly ParseCutoffDate(string? cutoffDateStr, int taxYear)
        {
            if (string.IsNullOrWhiteSpace(cutoffDateStr))
                return new DateOnly(taxYear, 12, 31);

            if (DateOnly.TryParse(cutoffDateStr, out var parsed) && parsed.Year == taxYear)
                return parsed;

            throw new BadRequestException("INVALID_CUTOFF_DATE",
                $"Ngày chốt '{cutoffDateStr}' không hợp lệ. Phải thuộc năm {taxYear} (định dạng: YYYY-MM-DD).");
        }

        private static string GetLawSuffix(int taxYear, int newLawFromYear)
            => taxYear >= newLawFromYear ? newLawFromYear.ToString() : (newLawFromYear - 1).ToString();

        private static string BuildLawGroupLabel(int taxYear, string suffix)
            => suffix == "2026"
                ? $"Luật 109/2025/QH15 (áp dụng từ 2026) — 5 bậc thuế"
                : $"Luật cũ (≤2025) — 7 bậc thuế";

        private static string BuildSummaryMessage(decimal payable, decimal withheld, decimal refund, decimal due, decimal exemption)
        {
            var net = payable - withheld;
            if (net <= 0)
                return $"✅ Bạn nộp thừa {refund:N0} VNĐ — Đủ điều kiện đề nghị hoàn thuế!";
            if (due == 0 && net > 0)
                return $"✅ Chênh lệch {net:N0} VNĐ ≤ ngưỡng miễn {exemption:N0} VNĐ — Được miễn, không phải nộp thêm.";
            return $"⚠️ Bạn nộp thiếu {due:N0} VNĐ — Cần nộp thêm khi quyết toán.";
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  PRIVATE — Inner Config Object
        // ══════════════════════════════════════════════════════════════════════════

        private class TaxCalcConfig
        {
            public string LawSuffix { get; set; } = string.Empty;
            public int TaxYear { get; set; }
            public decimal PersonalMonthly { get; set; }
            public decimal DependentMonthly { get; set; }
            public List<PitBracketConfigDto> Brackets { get; set; } = new();
            public decimal BhxhRate { get; set; }
            public decimal BhytRate { get; set; }
            public decimal BhtnRate { get; set; }
            public decimal SmallAmountExemption { get; set; }
            /// <summary>Giới hạn chi phí y tế tối đa/năm (lấy từ config PIT_MEDICAL_MAX_YEARLY). 0 = chưa áp dụng.</summary>
            public decimal MedicalMaxYearly { get; set; }
            /// <summary>Giới hạn chi phí giáo dục tối đa/năm (lấy từ config PIT_EDUCATION_MAX_YEARLY). 0 = chưa áp dụng.</summary>
            public decimal EducationMaxYearly { get; set; }
        }
    }
}
