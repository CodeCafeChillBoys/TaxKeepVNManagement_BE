using System;

namespace TaxKeepVN.Domain.Enums
{
    /// <summary>
    /// Trạng thái của phiên đặt lịch tư vấn thuế (Đặc tả 3)
    /// </summary>
    public enum BookingStatus
    {
        /// <summary>
        /// Khách hàng vừa gửi yêu cầu, slot được tạm giữ 10 phút chờ thanh toán
        /// </summary>
        PENDING_PAYMENT,

        /// <summary>
        /// Quá hạn 10 phút mà khách hàng chưa thanh toán -> Tự động hủy và giải phóng slot
        /// </summary>
        EXPIRED_UNPAID,

        /// <summary>
        /// Khách hàng đã thanh toán thành công, đang chờ chuyên gia xét duyệt và tiếp nhận trong 2 giờ
        /// </summary>
        AWAITING_EXPERT_APPROVAL,

        /// <summary>
        /// Chuyên gia đã đồng ý tiếp nhận lịch hẹn
        /// </summary>
        CONFIRMED,

        /// <summary>
        /// Chuyên gia từ chối tiếp nhận ca tư vấn trong vòng 2 giờ (hệ thống hoàn tiền 100% cho khách)
        /// </summary>
        REJECTED_BY_EXPERT,

        /// <summary>
        /// Quá hạn 2 giờ mà chuyên gia không bấm phản hồi (hệ thống tự động hủy và hoàn tiền 100% cho khách)
        /// </summary>
        EXPIRED_NO_RESPONSE,

        /// <summary>
        /// Khách hàng chủ động hủy lịch hẹn
        /// </summary>
        CANCELLED_BY_USER,

        /// <summary>
        /// Chuyên gia hủy lịch hẹn sau khi đã xác nhận (do sự cố đột xuất)
        /// </summary>
        CANCELLED_BY_EXPERT,

        /// <summary>
        /// Phiên tư vấn đã diễn ra và hoàn thành thành công (mở quyền đánh giá review)
        /// </summary>
        COMPLETED
    }
}
