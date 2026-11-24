using System;
using System.Collections.Generic;
using System.Globalization;

namespace TaxKeepVN.Application.Helpers
{
    public static class PeriodHelper
    {
        /// <summary>
        /// Tạo danh sách các tháng định dạng "YYYY-MM" trong khoảng [fromMonth, toMonth].
        /// </summary>
        public static HashSet<string> GetMonthsInRange(string? fromMonth, string? toMonth)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(fromMonth) || string.IsNullOrWhiteSpace(toMonth))
                return result;

            if (!DateTime.TryParseExact(fromMonth.Trim() + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fromDate) ||
                !DateTime.TryParseExact(toMonth.Trim() + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var toDate))
            {
                return result;
            }

            if (fromDate > toDate)
                return result;

            var cur = fromDate;
            while (cur <= toDate)
            {
                result.Add(cur.ToString("yyyy-MM", CultureInfo.InvariantCulture));
                cur = cur.AddMonths(1);
            }

            return result;
        }

        /// <summary>
        /// Lấy tất cả các năm trong khoảng [fromMonth, toMonth].
        /// </summary>
        public static HashSet<int> GetYearsInRange(string? fromMonth, string? toMonth)
        {
            var months = GetMonthsInRange(fromMonth, toMonth);
            var years = new HashSet<int>();
            foreach (var m in months)
            {
                if (int.TryParse(m.Substring(0, 4), out var y))
                    years.Add(y);
            }
            return years;
        }

        /// <summary>
        /// Tính các năm có tháng bị thay đổi (hiệu đối xứng giữa tập tháng cũ và tập tháng mới).
        /// </summary>
        public static HashSet<int> AffectedYears(string? oldFrom, string? oldTo, string? newFrom, string? newTo)
        {
            var oldMonths = GetMonthsInRange(oldFrom, oldTo);
            var newMonths = GetMonthsInRange(newFrom, newTo);

            // Hiệu đối xứng: (old \ new) U (new \ old)
            var changedMonths = new HashSet<string>(oldMonths, StringComparer.Ordinal);
            changedMonths.SymmetricExceptWith(newMonths);

            var affectedYears = new HashSet<int>();
            foreach (var m in changedMonths)
            {
                if (int.TryParse(m.Substring(0, 4), out var y))
                    affectedYears.Add(y);
            }

            return affectedYears;
        }
    }
}
