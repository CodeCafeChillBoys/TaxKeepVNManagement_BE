using System;

namespace TaxKeepVN.Domain.Entities
{
    public class ConsultationPlatformPolicy
    {
        public int Id { get; set; } = 1;

        /// <summary>Phí tư vấn sàn tối thiểu chung của hệ thống (VNĐ)</summary>
        public decimal ExpertMinFeeVnd { get; set; } = 100000m;

        /// <summary>Phí tư vấn trần tối đa chung của hệ thống (VNĐ)</summary>
        public decimal ExpertMaxFeeVnd { get; set; } = 2000000m;

        /// <summary>Thời gian tạm giữ slot chờ thanh toán (Phút)</summary>
        public int BookingSlotHoldMinutes { get; set; } = 10;

        /// <summary>Thời gian tối thiểu phải đặt trước giờ hẹn (Giờ)</summary>
        public int BookingMinLeadTimeHours { get; set; } = 4;

        /// <summary>Hạn chót chuyên gia xác nhận ca đặt lịch (Giờ)</summary>
        public int ExpertConfirmTimeoutHours { get; set; } = 2;

        /// <summary>Tỷ lệ phí hoa hồng nền tảng trích từ ca tư vấn (%)</summary>
        public decimal PlatformCommissionRatePercent { get; set; } = 20m;

        /// <summary>Thời gian mở phòng trước giờ hẹn (Phút)</summary>
        public int SessionOpenBeforeMinutes { get; set; } = 10;

        /// <summary>Ngưỡng thời gian xác định vắng mặt No-Show (Phút)</summary>
        public int SessionNoShowMinutes { get; set; } = 15;

        /// <summary>Thời hạn cho phép gửi đánh giá sau ca tư vấn (Ngày)</summary>
        public int RatingWindowDays { get; set; } = 7;

        /// <summary>Thời gian tạm giữ tiền Escrow chờ khiếu nại trước khi tự giải ngân (Giờ)</summary>
        public int EscrowCooldownHours { get; set; } = 24;

        /// <summary>Mốc hủy lịch sớm được hoàn tiền 100% (Giờ)</summary>
        public int CancelFullRefundHours { get; set; } = 24;

        /// <summary>Mốc hủy lịch cận giờ được hoàn một phần (Giờ)</summary>
        public int CancelPartialRefundHours { get; set; } = 4;

        /// <summary>Tỷ lệ hoàn tiền khi hủy cận giờ (%)</summary>
        public decimal CancelPartialRefundPercent { get; set; } = 50m;

        /// <summary>Số ca tư vấn tối đa chuyên gia được mở trong ngày</summary>
        public int ExpertMaxDailySlots { get; set; } = 6;

        /// <summary>Hạn mức rút tiền tối thiểu từ ví chuyên gia (VNĐ)</summary>
        public decimal ExpertMinWithdrawalVnd { get; set; } = 500000m;

        /// <summary>Mốc thời gian chuyên gia hủy lịch bị coi là hủy muộn (Giờ)</summary>
        public int ExpertLateCancelHours { get; set; } = 12;

        /// <summary>Mức phạt tiền chuyên gia khi hủy lịch muộn (VNĐ)</summary>
        public decimal ExpertLateCancelPenaltyVnd { get; set; } = 100000m;

        /// <summary>Thời hạn người nộp thuế được mở khiếu nại (Giờ)</summary>
        public int DisputeWindowHours { get; set; } = 24;

        /// <summary>Thời hạn chuyên gia phải phản hồi khiếu nại (Giờ)</summary>
        public int DisputeExpertReplyHours { get; set; } = 24;

        public bool IsActive { get; set; } = true;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
