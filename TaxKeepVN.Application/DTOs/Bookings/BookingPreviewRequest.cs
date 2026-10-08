using System;
using System.ComponentModel.DataAnnotations;

namespace TaxKeepVN.Application.DTOs.Bookings
{
    /// <summary>
    /// Request tính toán và xem trước thông tin đặt lịch tư vấn (Đặc tả 3 - Mục 5 Luồng chính)
    /// </summary>
    public class BookingPreviewRequest
    {
        [Required(ErrorMessage = "Vui lòng chọn khung giờ tư vấn (expertSlotId).")]
        public Guid ExpertSlotId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn chủ đề cần tư vấn (specializationId).")]
        public int SpecializationId { get; set; }
    }
}
