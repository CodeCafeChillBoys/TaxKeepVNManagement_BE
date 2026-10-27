using System;

namespace TaxKeepVN.Application.DTOs.Bookings
{
    /// <summary>
    /// Request tạo lịch hẹn tư vấn thuần JSON gọn nhẹ (Đặc tả 3 - Mục 5 Luồng chính)
    /// Đã loại bỏ đính kèm tệp trước buổi tư vấn để bảo vệ quyền riêng tư & dữ liệu nhạy cảm của khách hàng
    /// </summary>
    public class CreateBookingRequest
    {
        /// <summary>Khung giờ rảnh đã chọn trên lịch chuyên gia</summary>
        public Guid ExpertSlotId { get; set; }

        /// <summary>Chủ đề cần tư vấn (chọn từ danh mục có sẵn)</summary>
        public int SpecializationId { get; set; }

        /// <summary>Tiêu đề tóm tắt buổi tư vấn</summary>
        public string TopicTitle { get; set; } = string.Empty;

        /// <summary>Mô tả chi tiết vấn đề / câu hỏi trọng tâm</summary>
        public string ProblemDescription { get; set; } = string.Empty;
    }
}
