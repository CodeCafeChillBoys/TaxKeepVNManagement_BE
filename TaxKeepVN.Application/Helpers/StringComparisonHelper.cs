using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Application.Helpers
{
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public static class StringComparisonHelper
    {
        /// <summary>
        /// Loại bỏ toàn bộ dấu tiếng Việt (ví dụ: "Nguyễn Văn An" -> "Nguyen Van An")
        /// </summary>
        public static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

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

            var result = stringBuilder.ToString().Normalize(NormalizationForm.FormC);
            // Thay thế ký tự Đ/đ đặc thù trong tiếng Việt
            result = Regex.Replace(result, "[Đ]", "D");
            result = Regex.Replace(result, "[đ]", "d");

            return result;
        }

        /// <summary>
        /// So khớp họ và tên giữa 2 chuỗi (bỏ qua dấu, chữ hoa/thường, khoảng trắng thừa)
        /// </summary>
        public static bool IsNameMatching(string name1, string name2)
        {
            if (string.IsNullOrWhiteSpace(name1) || string.IsNullOrWhiteSpace(name2))
                return false;

            var clean1 = RemoveDiacritics(name1).Trim().ToUpperInvariant();
            var clean2 = RemoveDiacritics(name2).Trim().ToUpperInvariant();

            // So sánh trực tiếp
            if (clean1 == clean2)
                return true;

            // Xóa toàn bộ khoảng trắng thừa để so khớp (phòng trường hợp cách 2 space)
            var noSpace1 = Regex.Replace(clean1, @"\s+", " ");
            var noSpace2 = Regex.Replace(clean2, @"\s+", " ");

            return noSpace1 == noSpace2;
        }

        private static readonly Dictionary<string, string[]> DocTypeKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            ["CITIZEN_ID"] = new[] { "CCCD", "CITIZEN", "CMND", "CAN_CUOC" },
            ["BIRTH_CERTIFICATE"] = new[] { "BIRTH", "KHAI_SINH" },
            ["CHILD_BIRTH_CERTIFICATE"] = new[] { "BIRTH", "KHAI_SINH", "CHILD_BIRTH" },
            ["TAXPAYER_BIRTH_CERTIFICATE"] = new[] { "BIRTH", "KHAI_SINH", "TAXPAYER_BIRTH" },
            ["MARRIAGE_CERTIFICATE"] = new[] { "MARRIAGE", "KET_HON" },
            ["TAXPAYER_MARRIAGE_CERTIFICATE"] = new[] { "MARRIAGE", "KET_HON", "TAXPAYER_MARRIAGE" },
            ["DISABILITY_CERTIFICATE"] = new[] { "DISABILITY", "KHUYET_TAT" },
            ["HOUSEHOLD_CERTIFICATE"] = new[] { "HO_KHAU", "HOUSEHOLD" },
            ["RELATIONSHIP_CERTIFICATE"] = new[] { "RELATIONSHIP", "GIA_DINH" },
            ["RESIDENCE_CT07"] = new[] { "CT07", "RESIDENCE", "CU_TRU", "XAC_NHAN_CU_TRU" },
            ["STUDENT_CARD"] = new[] { "STUDENT", "SINH_VIEN", "THE_SINH_VIEN" },
            ["ADOPTION_CERTIFICATE"] = new[] { "ADOPTION", "CON_NUOI", "NUOI_CON" },
            ["PENSION_STATEMENT"] = new[] { "PENSION", "HUU_TRI", "SO_HUU_TRI" },
            ["COMMUNE_CERTIFICATION"] = new[] { "COMMUNE", "UBND", "XAC_NHAN_UBND" },
            ["SUPPORT_COMMITMENT"] = new[] { "CAM_KET", "SUPPORT_COMMITMENT" },
            ["SUPPORT_COMMITMENT_FORM"] = new[] { "CAM_KET", "SUPPORT_COMMITMENT" }
        };

        /// <summary>
        /// So khớp loại giấy tờ yêu cầu với loại giấy tờ mà AI nhận diện được
        /// </summary>
        public static bool IsDocTypeMatching(string expectedDocType, string? aiDocType)
        {
            if (string.IsNullOrWhiteSpace(aiDocType))
                return true; // Nếu AI không nhận diện được loại, không block cứng để tránh false positive

            var expected = expectedDocType.Trim().ToUpperInvariant();
            var aiType = aiDocType.Trim().ToUpperInvariant();

            // So sánh trực tiếp hoặc sau khi chuẩn hóa
            var normExpected = TaxKeepVN.Application.Service.Implementations.DependentRuleService.NormalizeDocType(expected);
            var normAi = TaxKeepVN.Application.Service.Implementations.DependentRuleService.NormalizeDocType(aiType);
            if (normExpected == normAi || expected == aiType)
                return true;

            // Nhánh tương thích đặc thù giữa Khai sinh, Kết hôn, CT07
            if ((normExpected is "CHILD_BIRTH_CERTIFICATE" or "TAXPAYER_BIRTH_CERTIFICATE" or "BIRTH_CERTIFICATE") &&
                (normAi is "CHILD_BIRTH_CERTIFICATE" or "TAXPAYER_BIRTH_CERTIFICATE" or "BIRTH_CERTIFICATE"))
                return true;

            if ((normExpected is "MARRIAGE_CERTIFICATE" or "TAXPAYER_MARRIAGE_CERTIFICATE") &&
                (normAi is "MARRIAGE_CERTIFICATE" or "TAXPAYER_MARRIAGE_CERTIFICATE"))
                return true;

            if ((normExpected is "HOUSEHOLD_CERTIFICATE" or "RELATIONSHIP_CERTIFICATE") &&
                (normAi is "HOUSEHOLD_CERTIFICATE" or "RELATIONSHIP_CERTIFICATE"))
                return true;

            if ((normExpected is "SUPPORT_COMMITMENT" or "SUPPORT_COMMITMENT_FORM") &&
                (normAi is "SUPPORT_COMMITMENT" or "SUPPORT_COMMITMENT_FORM"))
                return true;

            // Kiểm tra theo bảng từ khóa
            if (DocTypeKeywords.TryGetValue(normExpected, out var keywords))
            {
                if (keywords.Any(kw => aiType.Contains(kw, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }

            if (DocTypeKeywords.TryGetValue(normAi, out var aiKeywords))
            {
                if (aiKeywords.Any(kw => expected.Contains(kw, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }

            // Mã không có trong bảng từ khóa thì so trực tiếp chuỗi
            return aiType.Contains(normExpected, StringComparison.OrdinalIgnoreCase) || 
                   normExpected.Contains(aiType, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsDocTypeMatching(DocumentType expectedType, string? aiDocType)
        {
            return IsDocTypeMatching(expectedType.ToString(), aiDocType);
        }
    }
}
