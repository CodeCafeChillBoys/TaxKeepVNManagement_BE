using System;
using System.ComponentModel.DataAnnotations;

namespace TaxKeepVN.Application.DTOs.PlatformPolicy
{
    /// <summary>
    /// Thông tin chính sách vận hành sàn tư vấn và quy chế Escrow hiện hành
    /// </summary>
    public class PlatformPolicyResponseDto
    {
        public int Id { get; set; }

        /// <summary>Phí tư vấn sàn tối thiểu chung của hệ thống (VNĐ)</summary>
        public decimal ExpertMinFeeVnd { get; set; }

        /// <summary>Phí tư vấn trần tối đa chung của hệ thống (VNĐ)</summary>
        public decimal ExpertMaxFeeVnd { get; set; }

        /// <summary>Thời gian tạm giữ slot chờ thanh toán (Phút)</summary>
        public int BookingSlotHoldMinutes { get; set; }

        /// <summary>Thời gian tối thiểu phải đặt trước giờ hẹn (Giờ)</summary>
        public int BookingMinLeadTimeHours { get; set; }

        /// <summary>Hạn chót chuyên gia xác nhận ca đặt lịch (Giờ)</summary>
        public int ExpertConfirmTimeoutHours { get; set; }

        /// <summary>Tỷ lệ phí hoa hồng nền tảng trích từ ca tư vấn (%)</summary>
        public decimal PlatformCommissionRatePercent { get; set; }

        /// <summary>Thời gian mở phòng trước giờ hẹn (Phút)</summary>
        public int SessionOpenBeforeMinutes { get; set; }

        /// <summary>Ngưỡng thời gian xác định vắng mặt No-Show (Phút)</summary>
        public int SessionNoShowMinutes { get; set; }

        /// <summary>Thời hạn cho phép gửi đánh giá sau ca tư vấn (Ngày)</summary>
        public int RatingWindowDays { get; set; }

        /// <summary>Thời gian tạm giữ tiền Escrow chờ khiếu nại trước khi tự giải ngân (Giờ)</summary>
        public int EscrowCooldownHours { get; set; }

        /// <summary>Mốc hủy lịch sớm được hoàn tiền 100% (Giờ)</summary>
        public int CancelFullRefundHours { get; set; }

        /// <summary>Mốc hủy lịch cận giờ được hoàn một phần (Giờ)</summary>
        public int CancelPartialRefundHours { get; set; }

        /// <summary>Tỷ lệ hoàn tiền khi hủy cận giờ (%)</summary>
        public decimal CancelPartialRefundPercent { get; set; }

        /// <summary>Số ca tư vấn tối đa chuyên gia được mở trong ngày</summary>
        public int ExpertMaxDailySlots { get; set; }

        /// <summary>Hạn mức rút tiền tối thiểu từ ví chuyên gia (VNĐ)</summary>
        public decimal ExpertMinWithdrawalVnd { get; set; }

        /// <summary>Mốc thời gian chuyên gia hủy lịch bị coi là hủy muộn (Giờ)</summary>
        public int ExpertLateCancelHours { get; set; }

        /// <summary>Mức phạt tiền chuyên gia khi hủy lịch muộn (VNĐ)</summary>
        public decimal ExpertLateCancelPenaltyVnd { get; set; }

        /// <summary>Thời hạn người nộp thuế được mở khiếu nại (Giờ)</summary>
        public int DisputeWindowHours { get; set; }

        /// <summary>Thời hạn chuyên gia phải phản hồi khiếu nại (Giờ)</summary>
        public int DisputeExpertReplyHours { get; set; }

        public bool IsActive { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }
    }

    /// <summary>
    /// Yêu cầu cập nhật các tham số vận hành sàn tư vấn và Escrow
    /// </summary>
    public class UpdatePlatformPolicyRequest
    {
        /// <summary>Phí tư vấn sàn tối thiểu chung của hệ thống (VNĐ)</summary>
        [Range(0, 100000000, ErrorMessage = "Phí tư vấn sàn tối thiểu phải >= 0")]
        public decimal? ExpertMinFeeVnd { get; set; }

        /// <summary>Phí tư vấn trần tối đa chung của hệ thống (VNĐ)</summary>
        [Range(0, 1000000000, ErrorMessage = "Phí tư vấn trần tối đa phải >= 0")]
        public decimal? ExpertMaxFeeVnd { get; set; }

        /// <summary>Thời gian tạm giữ slot chờ thanh toán (Phút)</summary>
        [Range(1, 1440, ErrorMessage = "Thời gian giữ slot phải từ 1 đến 1440 phút")]
        public int? BookingSlotHoldMinutes { get; set; }

        /// <summary>Thời gian tối thiểu phải đặt trước giờ hẹn (Giờ)</summary>
        [Range(0, 720, ErrorMessage = "Thời gian đặt trước tối thiểu phải >= 0 giờ")]
        public int? BookingMinLeadTimeHours { get; set; }

        /// <summary>Hạn chót chuyên gia xác nhận ca đặt lịch (Giờ)</summary>
        [Range(1, 168, ErrorMessage = "Hạn chót chuyên gia xác nhận phải từ 1 đến 168 giờ")]
        public int? ExpertConfirmTimeoutHours { get; set; }

        /// <summary>Tỷ lệ phí hoa hồng nền tảng trích từ ca tư vấn (%)</summary>
        [Range(0, 100, ErrorMessage = "Tỷ lệ hoa hồng nền tảng phải từ 0% đến 100%")]
        public decimal? PlatformCommissionRatePercent { get; set; }

        /// <summary>Thời gian mở phòng trước giờ hẹn (Phút)</summary>
        [Range(0, 120, ErrorMessage = "Thời gian mở phòng trước giờ hẹn phải từ 0 đến 120 phút")]
        public int? SessionOpenBeforeMinutes { get; set; }

        /// <summary>Ngưỡng thời gian xác định vắng mặt No-Show (Phút)</summary>
        [Range(1, 120, ErrorMessage = "Ngưỡng thời gian No-Show phải từ 1 đến 120 phút")]
        public int? SessionNoShowMinutes { get; set; }

        /// <summary>Thời hạn cho phép gửi đánh giá sau ca tư vấn (Ngày)</summary>
        [Range(1, 365, ErrorMessage = "Thời hạn gửi đánh giá phải từ 1 đến 365 ngày")]
        public int? RatingWindowDays { get; set; }

        /// <summary>Thời gian tạm giữ tiền Escrow chờ khiếu nại trước khi tự giải ngân (Giờ)</summary>
        [Range(0, 720, ErrorMessage = "Thời gian tạm giữ Escrow phải từ 0 đến 720 giờ")]
        public int? EscrowCooldownHours { get; set; }

        /// <summary>Mốc hủy lịch sớm được hoàn tiền 100% (Giờ)</summary>
        [Range(0, 720, ErrorMessage = "Mốc hủy lịch hoàn tiền 100% phải từ 0 đến 720 giờ")]
        public int? CancelFullRefundHours { get; set; }

        /// <summary>Mốc hủy lịch cận giờ được hoàn một phần (Giờ)</summary>
        [Range(0, 720, ErrorMessage = "Mốc hủy lịch hoàn một phần phải từ 0 đến 720 giờ")]
        public int? CancelPartialRefundHours { get; set; }

        /// <summary>Tỷ lệ hoàn tiền khi hủy cận giờ (%)</summary>
        [Range(0, 100, ErrorMessage = "Tỷ lệ hoàn tiền khi hủy cận giờ phải từ 0% đến 100%")]
        public decimal? CancelPartialRefundPercent { get; set; }

        /// <summary>Số ca tư vấn tối đa chuyên gia được mở trong ngày</summary>
        [Range(1, 50, ErrorMessage = "Số ca tư vấn tối đa mỗi ngày phải từ 1 đến 50")]
        public int? ExpertMaxDailySlots { get; set; }

        /// <summary>Hạn mức rút tiền tối thiểu từ ví chuyên gia (VNĐ)</summary>
        [Range(0, 1000000000, ErrorMessage = "Hạn mức rút tiền tối thiểu phải >= 0")]
        public decimal? ExpertMinWithdrawalVnd { get; set; }

        /// <summary>Mốc thời gian chuyên gia hủy lịch bị coi là hủy muộn (Giờ)</summary>
        [Range(0, 720, ErrorMessage = "Mốc thời gian hủy muộn phải >= 0")]
        public int? ExpertLateCancelHours { get; set; }

        /// <summary>Mức phạt tiền chuyên gia khi hủy lịch muộn (VNĐ)</summary>
        [Range(0, 100000000, ErrorMessage = "Mức phạt tiền khi hủy muộn phải >= 0")]
        public decimal? ExpertLateCancelPenaltyVnd { get; set; }

        /// <summary>Thời hạn người nộp thuế được mở khiếu nại (Giờ)</summary>
        [Range(1, 720, ErrorMessage = "Thời hạn mở khiếu nại phải từ 1 đến 720 giờ")]
        public int? DisputeWindowHours { get; set; }

        /// <summary>Thời hạn chuyên gia phải phản hồi khiếu nại (Giờ)</summary>
        [Range(1, 720, ErrorMessage = "Thời hạn phản hồi khiếu nại phải từ 1 đến 720 giờ")]
        public int? DisputeExpertReplyHours { get; set; }
    }
}
