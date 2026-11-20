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

            // So sánh họ tên
            if (!string.IsNullOrWhiteSpace(patientName) && !string.IsNullOrWhiteSpace(parentDoc.BuyerName))
            {
                if (!StringComparisonHelper.IsNameMatching(patientName, parentDoc.BuyerName))
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
                    var msg = $"Số định danh/CCCD trên bảng kê ('{patientIdCard.Trim()}') không khớp với hóa đơn viện phí ('{parentDoc.BuyerIdCard.Trim()}').";
                    data.ValidationErrors.Add(msg);
                    if (!data.CrossCheckIssues.Contains(MedicalCrossCheckIssue.PATIENT_IDCARD_MISMATCH))
                        data.CrossCheckIssues.Add(MedicalCrossCheckIssue.PATIENT_IDCARD_MISMATCH);
                    hasCriticalMismatch = true;
                }
            }

            // 2. Đối chiếu số tiền người bệnh thực trả
            decimal? patientPaidOnStatement = statement.PatientPaidAmount;
            if (data.MedicalSummary.HasValue && data.MedicalSummary.Value.ValueKind == JsonValueKind.Object)
            {
                if (data.MedicalSummary.Value.TryGetProperty("patientPaid", out var pp) && pp.ValueKind == JsonValueKind.Number && pp.TryGetDecimal(out var ppVal))
                    patientPaidOnStatement = ppVal;
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
        /// So khớp tên dịch vụ / mục khám giữa hóa đơn và bảng kê (fuzzy keyword matching)
        /// </summary>
        public static bool IsMedicalServiceMatching(string invoiceService, string statementService)
        {
            if (string.IsNullOrWhiteSpace(invoiceService) || string.IsNullOrWhiteSpace(statementService))
                return false;

            var s1 = StringComparisonHelper.RemoveDiacritics(invoiceService).Trim().ToUpperInvariant();
            var s2 = StringComparisonHelper.RemoveDiacritics(statementService).Trim().ToUpperInvariant();

            if (s1 == s2 || s1.Contains(s2) || s2.Contains(s1))
                return true;

            var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "VA", "CUA", "CHO", "THEO", "CAC", "NHUNG", "MOT", "LUC", "LAN" };
            var words1 = s1.Split(new[] { ' ', '-', ',', '.', '(', ')', '/', '&' }, StringSplitOptions.RemoveEmptyEntries)
                           .Where(w => w.Length >= 3 && !stopWords.Contains(w))
                           .ToList();

            if (!words1.Any()) return false;

            int matchedCount = words1.Count(w => s2.Contains(w));
            return (double)matchedCount / words1.Count >= 0.4;
        }
    }
}
