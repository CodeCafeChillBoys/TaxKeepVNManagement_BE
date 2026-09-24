using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using TaxKeepVN.Domain.Constants;

namespace TaxKeepVN.Application.Law.Common
{
    public static class LegalDocumentNumber
    {
        public static string Normalize(string? docNumber)
        {
            if (string.IsNullOrWhiteSpace(docNumber))
                return string.Empty;

            // Remove all whitespace
            string s = Regex.Replace(docNumber, @"\s+", "");

            // Replace Vietnamese Đ and đ before normalization
            s = s.Replace("Đ", "D").Replace("đ", "d");

            // NFD normalization to remove remaining diacritics
            string normalized = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();

            foreach (char c in normalized)
            {
                UnicodeCategory uc = CharUnicodeInfo.GetUnicodeCategory(c);
                if (uc != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }

            return sb.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
        }

        public static string InferType(string? docNumber)
        {
            if (string.IsNullOrWhiteSpace(docNumber))
                return LawConstants.DocumentType.KHAC;

            string normalized = Normalize(docNumber);

            if (normalized.Contains("/UBTVQH"))
                return LawConstants.DocumentType.NGHI_QUYET;

            if (normalized.Contains("/QH"))
                return LawConstants.DocumentType.LUAT;

            if (normalized.Contains("/ND-CP"))
                return LawConstants.DocumentType.NGHI_DINH;

            if (normalized.Contains("/QD"))
                return LawConstants.DocumentType.QUYET_DINH;

            if (normalized.Contains("/TT-") || normalized.Contains("/TT"))
                return LawConstants.DocumentType.THONG_TU;

            if (normalized.Contains("/VBHN-") || normalized.Contains("/VBHN"))
                return LawConstants.DocumentType.VBHN;

            return LawConstants.DocumentType.KHAC;
        }

        public static int Level(string? docType, string? docNumber = null)
        {
            if (string.IsNullOrWhiteSpace(docType))
                return 99;

            switch (docType.ToUpperInvariant())
            {
                case LawConstants.DocumentType.LUAT:
                    return 1;
                case LawConstants.DocumentType.NGHI_QUYET:
                    return 2;
                case LawConstants.DocumentType.NGHI_DINH:
                    return 3;
                case LawConstants.DocumentType.QUYET_DINH:
                    return 4;
                case LawConstants.DocumentType.THONG_TU:
                    return 5;
                case LawConstants.DocumentType.VBHN:
                    if (!string.IsNullOrEmpty(docNumber))
                    {
                        string norm = Normalize(docNumber);
                        if (norm.Contains("VBHN-VPQH"))
                            return 1;
                    }
                    return 3;
                case LawConstants.DocumentType.KHAC:
                default:
                    return 6;
            }
        }
    }
}
