using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.TaxAI;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Helpers
{
    public static class MedicalCrossCheckHelper
    {
        /// <summary>
        /// Thực hiện đối chiếu chéo (Cross-Validation) giữa Bảng kê chi phí KCB và Hóa đơn viện phí cha:
        /// 1. Đối chiếu thông tin bệnh nhân (Họ tên, CCCD/CMND)
        /// 2. Đối chiếu số tiền người bệnh thực trả
        /// 3. Đối chiếu danh mục các mục khám / dịch vụ y tế
        /// </summary>
        public static async Task PerformMedicalCrossCheckAsync(
            IUnitOfWork unitOfWork,
            Document parentDoc,
            MedicalStatement statement,
            DocumentOcrData data)
        {
            data.CrossCheckIssues ??= new List<string>();
            data.ValidationErrors ??= new List<string>();
            bool hasCriticalMismatch = false;

            // 1. Đối chiếu thông tin bệnh nhân (Họ tên & CCCD)
            string? patientName = statement.PatientName;
            string? patientIdCard = statement.PatientIdCard;

            if (data.MedicalHeader.HasValue && data.MedicalHeader.Value.ValueKind == JsonValueKind.Object)
            {
                if (data.MedicalHeader.Value.TryGetProperty("patientName", out var pn) && pn.ValueKind == JsonValueKind.String)
                    patientName = pn.GetString();
                if (data.MedicalHeader.Value.TryGetProperty("idCard", out var idc) && idc.ValueKind == JsonValueKind.String)
                    patientIdCard = idc.GetString();
            }

            patientName ??= data.BuyerName;
            patientIdCard ??= data.BuyerIdCard;

            // 1.1 Lấy thông tin Người nộp thuế & Danh sách Người phụ thuộc từ DB
            User? taxpayer = null;
            List<Dependent> dependents = new();
            var periodRepo = unitOfWork.Repository<TaxPeriod>();
            if (periodRepo != null && parentDoc.PeriodId != Guid.Empty)
            {
                var period = await periodRepo.GetByIdAsync(parentDoc.PeriodId);
                if (period != null)
                {
                    var userRepo = unitOfWork.Repository<User>();
                    if (userRepo != null)
                        taxpayer = await userRepo.GetByIdAsync(period.UserId);

                    var depRepo = unitOfWork.Repository<Dependent>();
                    if (depRepo != null)
                    {
                        dependents = (await depRepo
                            .FindAsync(d => d.TaxpayerId == period.UserId && !d.IsDeleted)).ToList();
                    }
                }
            }

            // 1.2 [Khoản 3b Điều 49 Nghị định 253/2026/NĐ-CP & Rule R2]
            // Bệnh nhân bắt buộc phải là chính Người nộp thuế hoặc Người phụ thuộc được đăng ký giảm trừ
            bool isPatientTaxpayerOrDependent = false;
            if (!string.IsNullOrWhiteSpace(patientName))
            {
                // Kiểm tra trùng với chính người nộp thuế
                if (taxpayer != null && !string.IsNullOrWhiteSpace(taxpayer.FullName) &&
                    StringComparisonHelper.IsNameMatching(patientName, taxpayer.FullName))
                {
                    isPatientTaxpayerOrDependent = true;
                }
                else
                {
                    // Kiểm tra trùng với danh sách người phụ thuộc
                    var matchedDep = dependents.FirstOrDefault(d =>
                        !string.IsNullOrWhiteSpace(d.FullName) &&
                        StringComparisonHelper.IsNameMatching(patientName, d.FullName));

                    if (matchedDep != null)
                    {
                        isPatientTaxpayerOrDependent = true;
                        if (matchedDep.Status != DependentStatus.ACTIVE)
                        {
                            var warnMsg = $"Người bệnh là người phụ thuộc '{matchedDep.FullName}' nhưng trạng thái hồ sơ đang là '{matchedDep.Status}'. Cần hoàn tất hồ sơ người phụ thuộc để được tính giảm trừ chính thức.";
                            data.ValidationErrors.Add(warnMsg);
                        }
                    }
                }

                // Nếu có CCCD thì kiểm tra thêm đối chiếu CCCD
                if (!isPatientTaxpayerOrDependent && !string.IsNullOrWhiteSpace(patientIdCard))
                {
                    var cleanPatientId = Regex.Replace(patientIdCard, @"\D", "");
                    if (!string.IsNullOrEmpty(cleanPatientId))
                    {
                        if (taxpayer != null && !string.IsNullOrEmpty(taxpayer.CitizenId) &&
                            cleanPatientId == Regex.Replace(taxpayer.CitizenId, @"\D", ""))
                        {
                            isPatientTaxpayerOrDependent = true;
                        }
                        else if (dependents.Any(d => !string.IsNullOrEmpty(d.CitizenId) && cleanPatientId == Regex.Replace(d.CitizenId, @"\D", "")))
                        {
                            isPatientTaxpayerOrDependent = true;
                        }
                    }
                }

                //  Nếu không phải NNT và không phải NPT: Từ chối theo Khoản 3b Điều 49 NĐ 253/2026/NĐ-CP
                if (!isPatientTaxpayerOrDependent && taxpayer != null)
                {
                    var allowedNames = new List<string> { taxpayer.FullName };
                    allowedNames.AddRange(dependents.Select(d => $"{d.FullName} (NPT)"));
                    var allowedStr = string.Join(", ", allowedNames);

                    var msg = $"Người bệnh trên bảng kê KCB ('{patientName.Trim()}') không phải Người nộp thuế ('{taxpayer.FullName}') và không thuộc danh sách Người phụ thuộc hợp lệ [{allowedStr}] (Khoản 3b Điều 49 Nghị định 253/2026/NĐ-CP).";
                    data.ValidationErrors.Add(msg);
                    if (!data.CrossCheckIssues.Contains(MedicalCrossCheckIssue.PATIENT_NOT_TAXPAYER_OR_DEPENDENT))
                        data.CrossCheckIssues.Add(MedicalCrossCheckIssue.PATIENT_NOT_TAXPAYER_OR_DEPENDENT);
                    hasCriticalMismatch = true;
                }
            }

            // 1.3 So sánh họ tên giữa bảng kê và hóa đơn viện phí
            if (!string.IsNullOrWhiteSpace(patientName) && !string.IsNullOrWhiteSpace(parentDoc.BuyerName))
            {
                bool isNameSame = StringComparisonHelper.IsNameMatching(patientName, parentDoc.BuyerName);
                // Trường hợp người mua hóa đơn là NNT trả tiền cho NPT (ví dụ bố mẹ trả viện phí cho con) thì được chấp nhận
                bool isBuyerTaxpayer = taxpayer != null && StringComparisonHelper.IsNameMatching(parentDoc.BuyerName, taxpayer.FullName);

                if (!isNameSame && !isBuyerTaxpayer)
                {
                    var msg = $"Thông tin bệnh nhân trên bảng kê ('{patientName.Trim()}') không trùng khớp với người đứng tên trên hóa đơn viện phí ('{parentDoc.BuyerName.Trim()}').";
                    data.ValidationErrors.Add(msg);
                    if (!data.CrossCheckIssues.Contains(MedicalCrossCheckIssue.PATIENT_NAME_MISMATCH))
                        data.CrossCheckIssues.Add(MedicalCrossCheckIssue.PATIENT_NAME_MISMATCH);
                    hasCriticalMismatch = true;
                }
            }

            // So sánh CCCD/CMND nếu cả hai bên đều có
            if (!string.IsNullOrWhiteSpace(patientIdCard) && !string.IsNullOrWhiteSpace(parentDoc.BuyerIdCard))
            {
                var cleanPatientId = Regex.Replace(patientIdCard, @"\D", "");
                var cleanInvoiceId = Regex.Replace(parentDoc.BuyerIdCard, @"\D", "");
                if (!string.IsNullOrEmpty(cleanPatientId) && !string.IsNullOrEmpty(cleanInvoiceId) && cleanPatientId != cleanInvoiceId)
                {
                    // Nếu bệnh nhân là NPT và người mua hóa đơn là NNT thì CCCD khác nhau là hoàn toàn hợp lệ
                    bool isInvoiceBuyerTaxpayer = taxpayer != null && (
                        (!string.IsNullOrEmpty(taxpayer.CitizenId) && cleanInvoiceId == Regex.Replace(taxpayer.CitizenId, @"\D", "")) ||
                        (!string.IsNullOrWhiteSpace(parentDoc.BuyerName) && StringComparisonHelper.IsNameMatching(parentDoc.BuyerName, taxpayer.FullName))
                    );

                    if (!(isPatientTaxpayerOrDependent && isInvoiceBuyerTaxpayer))
                    {
                        var msg = $"Số định danh/CCCD trên bảng kê ('{patientIdCard.Trim()}') không khớp với hóa đơn viện phí ('{parentDoc.BuyerIdCard.Trim()}').";
                        data.ValidationErrors.Add(msg);
                        if (!data.CrossCheckIssues.Contains(MedicalCrossCheckIssue.PATIENT_IDCARD_MISMATCH))
                            data.CrossCheckIssues.Add(MedicalCrossCheckIssue.PATIENT_IDCARD_MISMATCH);
                        hasCriticalMismatch = true;
                    }
                }
            }

            // 2. Đối chiếu số tiền người bệnh thực trả
            decimal? patientPaidOnStatement = statement.PatientPaidAmount;

            // Ưu tiên 1: Tự động cộng từ danh sách các dòng chi tiết (Cột 11 + Cột 13)
            if (data.MedicalLines.HasValue && data.MedicalLines.Value.ValueKind == JsonValueKind.Array)
            {
                decimal sumCoPay = 0m;
                decimal sumSelfPay = 0m;
                foreach (var item in data.MedicalLines.Value.EnumerateArray())
                {
                    if (item.TryGetProperty("patientCoPayAmount", out var cp) && cp.TryGetDecimal(out var cpVal))
                        sumCoPay += cpVal;
                    else if (item.TryGetProperty("patientCoPay", out var cp2) && cp2.TryGetDecimal(out var cpVal2))
                        sumCoPay += cpVal2;

                    if (item.TryGetProperty("patientSelfPayAmount", out var sp) && sp.TryGetDecimal(out var spVal))
                        sumSelfPay += spVal;
                    else if (item.TryGetProperty("patientSelfPay", out var sp2) && sp2.TryGetDecimal(out var spVal2))
                        sumSelfPay += spVal2;
                }

                if (sumCoPay > 0 || sumSelfPay > 0)
                {
                    patientPaidOnStatement = sumCoPay + sumSelfPay;
                    statement.PatientPaidAmount = patientPaidOnStatement;
                    statement.PatientSelfPayAmount = sumSelfPay;
                }
            }

            // Ưu tiên 2: Đọc từ MedicalSummary do AI tính
            if (patientPaidOnStatement == null && data.MedicalSummary.HasValue && data.MedicalSummary.Value.ValueKind == JsonValueKind.Object)
            {
                if (data.MedicalSummary.Value.TryGetProperty("patientPaid", out var pp) && pp.ValueKind == JsonValueKind.Number && pp.TryGetDecimal(out var ppVal))
                    patientPaidOnStatement = ppVal;
                else if (data.MedicalSummary.Value.TryGetProperty("patientPaidAmount", out var ppa) && ppa.ValueKind == JsonValueKind.Number && ppa.TryGetDecimal(out var ppaVal))
                    patientPaidOnStatement = ppaVal;
                else if (data.MedicalSummary.Value.TryGetProperty("patientCoPayAmount", out var copay) && copay.ValueKind == JsonValueKind.Number && copay.TryGetDecimal(out var cpVal))
                {
                    var selfPay = 0m;
                    if (data.MedicalSummary.Value.TryGetProperty("patientSelfPayAmount", out var sp) && sp.ValueKind == JsonValueKind.Number && sp.TryGetDecimal(out var spVal))
                        selfPay = spVal;
                    patientPaidOnStatement = cpVal + selfPay;
                }
                else if (data.MedicalSummary.Value.TryGetProperty("coPayWithinInsurance", out var coPayOld) && coPayOld.ValueKind == JsonValueKind.Number && coPayOld.TryGetDecimal(out var cpOldVal))
                {
                    var otherPay = 0m;
                    if (data.MedicalSummary.Value.TryGetProperty("otherPatientPayments", out var op) && op.ValueKind == JsonValueKind.Number && op.TryGetDecimal(out var opVal))
                        otherPay = opVal;
                    patientPaidOnStatement = cpOldVal + otherPay;
                }
            }

            // Ưu tiên 3: Nếu AI đọc lệch hoặc thiếu, kiểm tra lại bằng công thức chuẩn toán học (Tổng viện phí - BHYT trả)
            if (statement.TotalHospitalAmount.HasValue && statement.InsurancePaidAmount.HasValue)
            {
                var expectedFromDiff = statement.TotalHospitalAmount.Value - statement.InsurancePaidAmount.Value;
                if (patientPaidOnStatement == null || Math.Abs(patientPaidOnStatement.Value - expectedFromDiff) > 1000m)
                {
                    if (parentDoc.TotalAmount.HasValue && Math.Abs(expectedFromDiff - parentDoc.TotalAmount.Value) <= 1000m)
                    {
                        patientPaidOnStatement = expectedFromDiff;
                        statement.PatientPaidAmount = expectedFromDiff;
                    }
                }
            }

            if (patientPaidOnStatement.HasValue && parentDoc.TotalAmount.HasValue)
            {
                var diff = Math.Abs(patientPaidOnStatement.Value - parentDoc.TotalAmount.Value);
                if (diff > 1000m) // Chấp nhận sai số làm tròn tối đa 1.000 VNĐ
                {
                    var msg = $"Số tiền người bệnh thực trả trên bảng kê ({patientPaidOnStatement.Value:N0} đ) không khớp với số tiền thực tế trên hóa đơn viện phí ({parentDoc.TotalAmount.Value:N0} đ).";
                    data.ValidationErrors.Add(msg);
                    if (!data.CrossCheckIssues.Contains(MedicalCrossCheckIssue.TOTAL_AMOUNT_MISMATCH))
                        data.CrossCheckIssues.Add(MedicalCrossCheckIssue.TOTAL_AMOUNT_MISMATCH);
                    hasCriticalMismatch = true;
                }
            }

            // 3. Đối chiếu danh mục các mục khám / dịch vụ
            var itemRepo = unitOfWork.Repository<DocumentItem>();
            var parentItems = (await itemRepo.FindAsync(i => i.DocumentId == parentDoc.Id)).ToList();

            if (parentItems.Any() && data.MedicalLines.HasValue && data.MedicalLines.Value.ValueKind == JsonValueKind.Array)
            {
                var statementLineNames = new List<string>();
                foreach (var lineElem in data.MedicalLines.Value.EnumerateArray())
                {
                    if (lineElem.TryGetProperty("itemName", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                    {
                        var sName = nameProp.GetString();
                        if (!string.IsNullOrWhiteSpace(sName))
                            statementLineNames.Add(sName);
                    }
                }

                foreach (var item in parentItems)
                {
                    if (string.IsNullOrWhiteSpace(item.ItemName)) continue;

                    // Bỏ qua nếu dòng hóa đơn chỉ là dòng tổng quát (Lump-sum)
                    var cleanItemName = StringComparisonHelper.RemoveDiacritics(item.ItemName).Trim().ToUpperInvariant();
                    if (cleanItemName.Contains("VIEN PHI") || cleanItemName.Contains("KHAM CHUA BENH") || cleanItemName.Contains("KCB"))
                    {
                        continue;
                    }

                    // Kiểm tra xem có dòng tương ứng nào trong bảng kê không
                    bool isItemMatched = statementLineNames.Any(sLine => IsMedicalServiceMatching(item.ItemName, sLine));
                    if (!isItemMatched)
                    {
                        var msg = $"Mục khám/dịch vụ '{item.ItemName}' trên hóa đơn viện phí không tìm thấy mục tương ứng trên bảng kê chi phí KCB.";
                        data.ValidationErrors.Add(msg);
                        if (!data.CrossCheckIssues.Contains(MedicalCrossCheckIssue.SERVICE_ITEM_MISMATCH))
                            data.CrossCheckIssues.Add(MedicalCrossCheckIssue.SERVICE_ITEM_MISMATCH);
                    }
                }
            }

            // 4. Nếu có lỗi nghiêm trọng (thông tin bệnh nhân hoặc số tiền không khớp)
            if (hasCriticalMismatch)
            {
                data.Status = MedicalStatementStatus.FAILED;
                statement.Status = MedicalStatementStatus.FAILED;
                statement.IsPassedThreshold = false;
                data.ValidationStatus ??= new DocumentValidationStatus();
                data.ValidationStatus.IsPassedThreshold = false;
                data.ValidationStatus.IsIdentityValid = false;
            }
        }

        /// <summary>
        /// So khớp tổng quát tên dịch vụ / mục khám giữa hóa đơn và bảng kê (Fuzzy Token Overlap)
        /// Không hardcode cố định tên thuốc hay dịch vụ cụ thể.
        /// </summary>
        public static bool IsMedicalServiceMatching(string invoiceService, string statementService)
        {
            if (string.IsNullOrWhiteSpace(invoiceService) || string.IsNullOrWhiteSpace(statementService))
                return false;

            // 1. Loại bỏ các đoạn chú thích trong ngoặc đơn, ví dụ: "(Phần cùng chi trả 20% BHYT)", "(Theo yêu cầu)"
            var cleanInvoice = Regex.Replace(invoiceService, @"\([^)]*\)", "").Trim();
            var cleanStatement = Regex.Replace(statementService, @"\([^)]*\)", "").Trim();

            // 2. Lược bỏ các tiền tố mã mục thường in ở đầu dòng bảng kê: "Mục 1 -", "Mục 2.", "I.", "II.", "1.", "2."
            cleanStatement = Regex.Replace(cleanStatement, @"^(mục|muc|\d+|[ivx]+)[\s\.\-_:]+", "", RegexOptions.IgnoreCase).Trim();

            var s1 = StringComparisonHelper.RemoveDiacritics(cleanInvoice).Trim().ToUpperInvariant();
            var s2 = StringComparisonHelper.RemoveDiacritics(cleanStatement).Trim().ToUpperInvariant();

            if (s1 == s2 || s1.Contains(s2) || s2.Contains(s1))
                return true;

            var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "VA", "CUA", "CHO", "THEO", "CAC", "NHUNG", "MOT", "LUC", "LAN", "THONG", "THUONG", "DICH", "VU", "CHUYEN", "KHOA", "PHAN", "GIA", "LOAI", "DOT", "NGAY", "SO"
            };

            var words1 = s1.Split(new[] { ' ', '-', ',', '.', '/', '&', '+', ':' }, StringSplitOptions.RemoveEmptyEntries)
                           .Where(w => w.Length >= 2 && !stopWords.Contains(w))
                           .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var words2 = s2.Split(new[] { ' ', '-', ',', '.', '/', '&', '+', ':' }, StringSplitOptions.RemoveEmptyEntries)
                           .Where(w => w.Length >= 2 && !stopWords.Contains(w))
                           .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (!words1.Any() || !words2.Any()) return false;

            // 3. Đếm số từ khóa trùng nhau hoặc chứa nhau giữa 2 tập từ
            int matchedCount = 0;
            foreach (var w1 in words1)
            {
                if (words2.Any(w2 => w1 == w2 || (w1.Length >= 4 && w2.Contains(w1)) || (w2.Length >= 4 && w1.Contains(w2))))
                {
                    matchedCount++;
                }
            }

            // Tỷ lệ khớp từ so với chuỗi ngắn hơn (từ 30% trở lên là coi như cùng dịch vụ)
            double matchRatio = (double)matchedCount / Math.Min(words1.Count, words2.Count);
            return matchRatio >= 0.30;
        }
    }
}
