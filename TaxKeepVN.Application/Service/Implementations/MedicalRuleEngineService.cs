using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TaxKeepVN.Application.Constants;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    public class MedicalRuleEngineService : IMedicalRuleEngineService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<MedicalRuleEngineService> _logger;

        private static readonly string[] ForeignKeywords = new[]
        {
            "nuoc ngoai", "singapore", "thailand", "thai lan", "usa", "my",
            "japan", "nhat ban", "korea", "han quoc", "phap", "france",
            "china", "trung quoc", "uc", "australia", "hospital abroad"
        };

        public MedicalRuleEngineService(IUnitOfWork unitOfWork, ILogger<MedicalRuleEngineService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task EvaluateClaimAsync(Document receipt, Document? statement, Guid userId, int taxYear)
        {
            _logger.LogInformation("Evaluating Medical Claim: ReceiptId={ReceiptId}, StatementId={StatementId}, UserId={UserId}, Year={TaxYear}",
                receipt.Id, statement?.Id, userId, taxYear);

            // ── 1. ĐỌC CẤU HÌNH TỪ system_configs ────────────────────────────────
            var configRepo = _unitOfWork.Repository<SystemConfig>();
            var allConfigs = (await configRepo.FindAsync(c => c.IsActive)).ToList();

            bool requireStatement = true;
            var reqConfig = allConfigs.FirstOrDefault(c => c.ConfigKey == MedicalConfigKeys.MedicalRequireStatement);
            if (reqConfig != null && bool.TryParse(reqConfig.ConfigValue, out bool parsedReq))
            {
                requireStatement = parsedReq;
            }

            var excludedGroupCodesStr = allConfigs.FirstOrDefault(c => c.ConfigKey == MedicalConfigKeys.MedicalNoCardExcludedGroupCodes)?.ConfigValue ?? "12";
            var excludedGroupCodes = new HashSet<string>(
                excludedGroupCodesStr.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()),
                StringComparer.OrdinalIgnoreCase);

            decimal tolerance = 1000m;
            var tolConfig = allConfigs.FirstOrDefault(c => c.ConfigKey == MedicalConfigKeys.MedicalStatementAmountTolerance);
            if (tolConfig != null && decimal.TryParse(tolConfig.ConfigValue, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal parsedTol))
            {
                tolerance = parsedTol;
            }

            // ── 2. CẤP HỒ SƠ: RULE R3 - CƠ SỞ KCB TRONG NƯỚC (K2a Đ49 NĐ 253) ──
            var facilityCandidate = statement?.FacilityName ?? receipt.FacilityName ?? receipt.SellerName ?? receipt.SellerAddress ?? string.Empty;
            if (IsForeignFacility(facilityCandidate))
            {
                _logger.LogWarning("Medical Claim REJECTED: Facility is abroad: '{Facility}'", facilityCandidate);
                receipt.MedicalClaimStatus = MedicalClaimStatus.REJECTED;
                receipt.EligibleAmount = 0m;
                if (statement != null)
                {
                    statement.MedicalClaimStatus = MedicalClaimStatus.REJECTED;
                    statement.EligibleAmount = 0m;
                }
                return;
            }

            // ── 3. CẤP HỒ SƠ: RULE R1 - CAM KẾT CHƯA BỒI HOÀN (K3c Đ49 NĐ 253) ──
            // IsNotReimbursed = false có nghĩa là đã được bồi hoàn hoặc người dùng không cam kết
            if (receipt.IsNotReimbursed == false)
            {
                _logger.LogWarning("Medical Claim REJECTED: Reimbursed from other sources: ReceiptId={Id}", receipt.Id);
                receipt.MedicalClaimStatus = MedicalClaimStatus.REJECTED;
                receipt.EligibleAmount = 0m;
                if (statement != null)
                {
                    statement.MedicalClaimStatus = MedicalClaimStatus.REJECTED;
                    statement.EligibleAmount = 0m;
                }
                return;
            }

            // ── 4. CẤP HỒ SƠ: RULE R2 - ĐỐI CHIẾU TÊN NGƯỜI BỆNH (K3b Đ49 NĐ 253) ─
            var patientName = statement?.PatientName ?? receipt.PatientName ?? receipt.BuyerName;
            var (isMatch, isNearMatch, matchedDependentId) = await MatchPatientAsync(patientName, userId);

            if (!isMatch && !isNearMatch)
            {
                _logger.LogWarning("Medical Claim REJECTED: Patient '{Patient}' does not match taxpayer or active dependents", patientName);
                receipt.MedicalClaimStatus = MedicalClaimStatus.REJECTED;
                receipt.EligibleAmount = 0m;
                if (statement != null)
                {
                    statement.MedicalClaimStatus = MedicalClaimStatus.REJECTED;
                    statement.EligibleAmount = 0m;
                }
                return;
            }

            receipt.PatientDependentId = matchedDependentId;
            if (statement != null)
            {
                statement.PatientDependentId = matchedDependentId;
            }

            // ── 5. CẤP HỒ SƠ: RULE R0 - BẮT BUỘC BẢNG KÊ CHI PHÍ KCB (K3a Đ49 NĐ 253) ──
            if (requireStatement && statement == null)
            {
                _logger.LogInformation("Medical Claim MISSING_STATEMENT: ReceiptId={Id} has no statement attached", receipt.Id);
                receipt.MedicalClaimStatus = MedicalClaimStatus.MISSING_STATEMENT;
                receipt.EligibleAmount = 0m;
                return;
            }

            // ── 6. CẤP DÒNG: THẨM ĐỊNH TỪNG DÒNG CHI PHÍ BẢNG KÊ (L1 - L6) ────────
            bool hasNeedReviewLine = !isMatch && isNearMatch; // Nếu tên gần đúng thì hồ sơ cần review
            decimal totalEligible = 0m;

            if (statement != null && statement.Items != null && statement.Items.Any())
            {
                // Nạp danh mục BHYT phục vụ khối không thẻ
                var catalogRepo = _unitOfWork.Repository<BhytCoverageCatalog>();
                var activeCatalogs = (await catalogRepo.FindAsync(c => c.IsActive)).ToList();

                foreach (var item in statement.Items.OrderBy(i => i.ItemOrder))
                {
                    // Bỏ qua dòng con trong gói thiết bị y tế mục 10
                    if (item.IsPackageSubLine)
                    {
                        item.EligibleAmount = 0m;
                        item.DeductionStatus = MedicalDeductionStatus.EXCLUDED;
                        item.DeductionReason = "PACKAGE_SUB_LINE";
                        continue;
                    }

                    // Dòng do người dùng đã can thiệp sửa thủ công (L6)
                    if (item.IsUserOverridden)
                    {
                        if (string.Equals(item.DeductionStatus, MedicalDeductionStatus.ELIGIBLE, StringComparison.OrdinalIgnoreCase))
                        {
                            decimal baseEligible = item.PatientCoPay ?? 0m;
                            decimal selfPay = (item.SelfPayOptIn && item.SelfPayAmount.HasValue) ? item.SelfPayAmount.Value : 0m;
                            item.EligibleAmount = baseEligible + selfPay;
                            totalEligible += item.EligibleAmount.Value;
                        }
                        else
                        {
                            item.EligibleAmount = 0m;
                        }
                        continue;
                    }

                    // Xác định dòng thuộc khối có thẻ hay không
                    bool hasCard = item.HasInsuranceCard ?? statement.HasInsuranceCard ?? false;

                    if (hasCard)
                    {
                        // ── KHỐI CÓ THẺ ──────────────────────────────────────────────
                        decimal insRate = item.InsuranceRate ?? 0m;

                        if (insRate > 0)
                        {
                            // L1: Bình thường trong danh mục BHYT
                            item.DeductionStatus = MedicalDeductionStatus.ELIGIBLE;
                            item.DeductionReason = MedicalDeductionReason.IN_COVERAGE_COPAY;
                            decimal eligibleBase = item.PatientCoPay ?? 0m;
                            item.EligibleAmount = eligibleBase;
                            item.SelfPayAmount = 0m;

                            decimal selfPay = item.PatientSelfPay ?? 0m;
                            if (selfPay > 0)
                            {
                                decimal col4 = item.UnitPrice;
                                decimal col5 = item.UnitPriceInsurance ?? 0m;
                                decimal col6 = item.ServiceRate ?? 100m;
                                decimal col8 = item.InsuranceRate ?? 100m;

                                if (col4 > col5 && col5 > 0)
                                {
                                    // K2 Chênh lệch giá
                                    item.SelfPayAmount = selfPay;
                                    item.SelfPayReason = MedicalDeductionReason.PRICE_DIFFERENCE_SELF_PAY;
                                }
                                else if (col4 == col5 && (col8 < 100m || col6 < 100m))
                                {
                                    // K3 BHYT trả theo tỷ lệ
                                    item.SelfPayAmount = selfPay;
                                    item.SelfPayReason = MedicalDeductionReason.PARTIAL_RATE_SELF_PAY;
                                }
                                else
                                {
                                    // K5 Không rõ
                                    item.DeductionStatus = MedicalDeductionStatus.NEED_REVIEW;
                                    item.DeductionReason = MedicalDeductionReason.SELF_PAY_UNKNOWN;
                                    item.SelfPayAmount = selfPay;
                                    hasNeedReviewLine = true;
                                }
                            }

                            // Người dùng bật opt-in tính cả phần tự trả
                            if (item.SelfPayOptIn && item.SelfPayAmount.HasValue)
                            {
                                item.EligibleAmount = (item.EligibleAmount ?? 0m) + item.SelfPayAmount.Value;
                            }

                            if (item.DeductionStatus == MedicalDeductionStatus.ELIGIBLE)
                            {
                                totalEligible += item.EligibleAmount ?? 0m;
                            }
                        }
                        else
                        {
                            // L2: Cột 8 = 0 -> Ngoài danh mục BHYT
                            item.DeductionStatus = MedicalDeductionStatus.EXCLUDED;
                            item.DeductionReason = MedicalDeductionReason.OUT_OF_COVERAGE;
                            item.EligibleAmount = 0m;
                            item.SelfPayAmount = item.PatientSelfPay ?? item.TotalPrice;
                        }
                    }
                    else
                    {
                        // ── KHỐI KHÔNG THẺ (cột 8 = 0, cột 13 = cột 7) ─────────────────
                        if (!string.IsNullOrWhiteSpace(item.GroupCode) && excludedGroupCodes.Contains(item.GroupCode))
                        {
                            // L3: Nhóm loại trừ theo config
                            item.DeductionStatus = MedicalDeductionStatus.EXCLUDED;
                            item.DeductionReason = MedicalDeductionReason.NON_MEDICAL_SERVICE;
                            item.EligibleAmount = 0m;
                        }
                        else
                        {
                            // L4: Tra cứu danh mục BHYT
                            var normItemName = NormalizeVietnamese(item.ItemName);
                            var match = activeCatalogs.FirstOrDefault(c =>
                                string.Equals(c.NormalizedName, normItemName, StringComparison.OrdinalIgnoreCase) ||
                                (!string.IsNullOrWhiteSpace(c.Code) && string.Equals(c.Code, item.ItemName.Trim(), StringComparison.OrdinalIgnoreCase)) ||
                                (!string.IsNullOrWhiteSpace(c.ActiveIngredient) && normItemName.Contains(NormalizeVietnamese(c.ActiveIngredient))));

                            if (match == null)
                            {
                                // Không có trong danh mục BHYT
                                item.DeductionStatus = MedicalDeductionStatus.EXCLUDED;
                                item.DeductionReason = MedicalDeductionReason.OUT_OF_COVERAGE;
                                item.EligibleAmount = 0m;
                            }
                            else
                            {
                                decimal? col5 = item.UnitPriceInsurance;
                                if (col5.HasValue && col5.Value > 0)
                                {
                                    // Cột 5 có in: tính min(cột 7, cột 3 * cột 5)
                                    decimal hospAmount = item.HospitalAmount ?? item.TotalPrice;
                                    decimal bhLimit = Math.Min(hospAmount, item.Quantity * col5.Value);
                                    item.EligibleAmount = bhLimit;

                                    decimal diff = Math.Max(0m, hospAmount - bhLimit);
                                    if (diff > 0)
                                    {
                                        item.SelfPayAmount = diff;
                                        item.SelfPayReason = MedicalDeductionReason.PRICE_DIFFERENCE_SELF_PAY;
                                    }

                                    if (item.SelfPayOptIn && item.SelfPayAmount.HasValue)
                                    {
                                        item.EligibleAmount += item.SelfPayAmount.Value;
                                    }

                                    item.DeductionStatus = MedicalDeductionStatus.ELIGIBLE;
                                    item.DeductionReason = MedicalDeductionReason.IN_COVERAGE_COPAY;
                                    totalEligible += item.EligibleAmount ?? 0m;
                                }
                                else
                                {
                                    // Cột 5 trống: không tách được phần chênh lệch -> NEED_REVIEW
                                    item.DeductionStatus = MedicalDeductionStatus.NEED_REVIEW;
                                    item.DeductionReason = MedicalDeductionReason.NO_CARD_PRICE_UNKNOWN;
                                    item.EligibleAmount = 0m;
                                    item.SelfPayAmount = item.HospitalAmount ?? item.TotalPrice;
                                    hasNeedReviewLine = true;
                                }
                            }
                        }
                    }

                    // L5: Trừ nguồn khác chi trả (cột 12)
                    if (item.OtherSourcePaid.HasValue && item.OtherSourcePaid.Value > 0)
                    {
                        item.EligibleAmount = Math.Max(0m, (item.EligibleAmount ?? 0m) - item.OtherSourcePaid.Value);
                    }
                }
            }

            // ── 7. TỔNG HỢP TRẠNG THÁI HỒ SƠ ─────────────────────────────────────
            var claimStatus = hasNeedReviewLine ? MedicalClaimStatus.NEED_REVIEW : MedicalClaimStatus.ELIGIBLE;

            if (statement != null)
            {
                statement.EligibleAmount = totalEligible;
                statement.MedicalClaimStatus = claimStatus;
            }

            receipt.EligibleAmount = totalEligible;
            receipt.MedicalClaimStatus = claimStatus;

            // Đồng bộ metadata sang hóa đơn nếu hóa đơn chưa có
            if (statement != null)
            {
                receipt.PatientName ??= statement.PatientName;
                receipt.FacilityName ??= statement.FacilityName ?? receipt.SellerName;
                receipt.HasInsuranceCard ??= statement.HasInsuranceCard;
                receipt.InsuranceBenefitRate ??= statement.InsuranceBenefitRate;
                receipt.ServiceDate ??= statement.ServiceDate ?? receipt.InvoiceDate;
            }

            _logger.LogInformation("Medical Claim Evaluated: Status={Status}, EligibleAmount={Amount:N0} VNĐ", claimStatus, totalEligible);
        }

        public async Task<(bool isMatch, bool isNearMatch, Guid? matchedDependentId)> MatchPatientAsync(string? patientName, Guid userId)
        {
            if (string.IsNullOrWhiteSpace(patientName))
            {
                return (false, false, null);
            }

            var normPatient = NormalizeVietnamese(patientName);

            // 1. Kiểm tra chính Người nộp thuế
            var userRepo = _unitOfWork.Repository<User>();
            var user = await userRepo.GetByIdAsync(userId);
            if (user != null && !string.IsNullOrWhiteSpace(user.FullName))
            {
                var normUser = NormalizeVietnamese(user.FullName);
                if (string.Equals(normUser, normPatient, StringComparison.OrdinalIgnoreCase))
                {
                    return (true, false, null);
                }

                if (IsNearMatch(normUser, normPatient))
                {
                    return (false, true, null);
                }
            }

            // 2. Kiểm tra danh sách Người phụ thuộc ACTIVE của người nộp thuế
            var depRepo = _unitOfWork.Repository<Dependent>();
            var dependents = await depRepo.FindAsync(d => d.TaxpayerId == userId && !d.IsDeleted);

            foreach (var dep in dependents)
            {
                if (string.IsNullOrWhiteSpace(dep.FullName)) continue;

                var normDep = NormalizeVietnamese(dep.FullName);
                if (string.Equals(normDep, normPatient, StringComparison.OrdinalIgnoreCase))
                {
                    return (true, false, dep.Id);
                }

                if (IsNearMatch(normDep, normPatient))
                {
                    return (false, true, dep.Id);
                }
            }

            return (false, false, null);
        }

        private static bool IsForeignFacility(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            var normalized = NormalizeVietnamese(text);
            return ForeignKeywords.Any(k => normalized.Contains(k));
        }

        private static bool IsNearMatch(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2)) return false;
            if (s1.Contains(s2) || s2.Contains(s1)) return true;
            int dist = ComputeLevenshteinDistance(s1, s2);
            return dist <= 2;
        }

        private static int ComputeLevenshteinDistance(string s, string t)
        {
            int n = s.Length;
            int m = t.Length;
            var d = new int[n + 1, m + 1];

            if (n == 0) return m;
            if (m == 0) return n;

            for (int i = 0; i <= n; d[i, 0] = i++) { }
            for (int j = 0; j <= m; d[0, j] = j++) { }

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }
            return d[n, m];
        }

        private static string NormalizeVietnamese(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            var clean = stringBuilder.ToString().Normalize(NormalizationForm.FormC);
            clean = clean.Replace('đ', 'd').Replace('Đ', 'D');
            clean = Regex.Replace(clean, @"[^a-zA-Z0-9\s]", " ");
            clean = Regex.Replace(clean, @"\s+", " ").Trim().ToLowerInvariant();
            return clean;
        }
    }
}
