using System;

namespace TaxKeepVN.Domain.Entities
{
    public class TaxSettlementSchedule
    {
        public int Id { get; set; } = 1;

        /// <summary>Năm quyết toán thuế (NULL = mặc định áp dụng chung cho mọi năm)</summary>
        public int? TaxYear { get; set; }

        /// <summary>Tháng hạn chót nộp quyết toán thuế TNCN (Mặc định: tháng 4)</summary>
        public int DeadlineMonth { get; set; } = 4;

        /// <summary>Ngày hạn chót nộp quyết toán thuế TNCN (Mặc định: ngày 30)</summary>
        public int DeadlineDay { get; set; } = 30;

        /// <summary>Các mốc ngày nhắc nhở trước hạn (ngăn cách bởi dấu phẩy, VD: 60,30,15,7,3,1)</summary>
        public string ReminderDays { get; set; } = "60,30,15,7,3,1";

        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
