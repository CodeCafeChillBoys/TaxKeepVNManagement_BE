using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Application.Helpers
{
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

        /// <summary>
        /// So khớp loại giấy tờ yêu cầu với loại giấy tờ mà AI nhận diện được
        /// </summary>
        public static bool IsDocTypeMatching(DocumentType expectedType, string? aiDocType)
        {
            if (string.IsNullOrWhiteSpace(aiDocType))
                return true; // Nếu AI không chắc chắn, không block cứng để tránh false positive

            var aiType = aiDocType.Trim().ToUpperInvariant();

            switch (expectedType)
            {
                case DocumentType.CITIZEN_ID:
                    return aiType.Contains("CCCD") || aiType.Contains("CITIZEN");

                case DocumentType.BIRTH_CERTIFICATE:
                    return aiType.Contains("BIRTH") || aiType.Contains("KHAI_SINH");

                case DocumentType.MARRIAGE_CERTIFICATE:
                    return aiType.Contains("MARRIAGE") || aiType.Contains("KET_HON");

                case DocumentType.DISABILITY_CERTIFICATE:
                    return aiType.Contains("DISABILITY") || aiType.Contains("KHUYET_TAT");

                case DocumentType.RELATIONSHIP_CERTIFICATE:
                    return aiType.Contains("CT07") || aiType.Contains("RESIDENCE") || aiType.Contains("HO_KHAU");

                default:
                    return true;
            }
        }
    }
}
