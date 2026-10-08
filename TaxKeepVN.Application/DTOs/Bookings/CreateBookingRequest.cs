using Microsoft.AspNetCore.Http;

namespace TaxKeepVN.Application.DTOs.Bookings
{
    /// <summary>
    /// Request tạo lịch hẹn tư vấn (Đặc tả 3 - Mục 5 Luồng chính)
    /// Nhận multipart/form-data kèm tệp đính kèm
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

        /// <summary>Đính kèm chứng từ/báo cáo mẫu (Tối đa 5 file, tổng dung lượng <= 25MB)</summary>
        public List<IFormFile>? Attachments { get; set; }
    }
}
