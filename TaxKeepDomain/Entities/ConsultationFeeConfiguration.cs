using System;

namespace TaxKeepVN.Domain.Entities
{
    /// <summary>
    /// Cấu hình khung giá sàn/trần của nền tảng (Mục 12 & BR-08)
    /// </summary>
    public class ConsultationFeeConfiguration
    {
        public int Id { get; set; }

        /// <summary>Loại phiên tư vấn (VD: ONLINE_MEETING, CHAT, VOICE_CALL)</summary>
        public string SessionType { get; set; } = "ONLINE_MEETING";

        /// <summary>Thời lượng tư vấn tính theo phút (VD: 30, 60)</summary>
        public int DurationMinutes { get; set; }

        /// <summary>Giá sàn tối thiểu do nền tảng quy định</summary>
        public decimal MinFee { get; set; }

        /// <summary>Giá trần tối đa do nền tảng quy định</summary>
        public decimal MaxFee { get; set; }

        /// <summary>Đang áp dụng hay tạm ngừng</summary>
        public bool IsActive { get; set; } = true;
    }
}
