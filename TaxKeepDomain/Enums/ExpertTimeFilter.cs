namespace TaxKeepVN.Domain.Enums
{
    /// <summary>
    /// Bộ lọc khung thời gian rảnh của chuyên gia (Đặc tả 2 - Mục 5)
    /// </summary>
    public enum ExpertTimeFilter
    {
        /// <summary>Tất cả thời gian</summary>
        All = 0,

        /// <summary>Rảnh trong hôm nay</summary>
        Today = 1,

        /// <summary>Rảnh vào ngày mai</summary>
        Tomorrow = 2,

        /// <summary>Rảnh vào cuối tuần này (Thứ 7 & Chủ Nhật)</summary>
        ThisWeekend = 3,

        /// <summary>Rảnh trong 7 ngày tới</summary>
        Next7Days = 4
    }
}
