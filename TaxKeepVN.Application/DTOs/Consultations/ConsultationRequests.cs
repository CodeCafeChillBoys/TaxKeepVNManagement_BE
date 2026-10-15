using System.ComponentModel.DataAnnotations;

namespace TaxKeepVN.Application.DTOs.Consultations
{
    /// <summary>
    /// Yêu cầu cập nhật bản nháp/ghi chú kết luận tư vấn (Chuyên gia)
    /// </summary>
    public class UpdateConsultationNotesRequest
    {
        [Required(ErrorMessage = "Nội dung ghi chú tư vấn không được để trống.")]
        [StringLength(5000, ErrorMessage = "Ghi chú không được vượt quá 5000 ký tự.")]
        public string SharedNotes { get; set; } = string.Empty;
    }

    /// <summary>
    /// Yêu cầu kết thúc phiên tư vấn trực tuyến (Bắt buộc nhập biên bản tóm tắt kết luận theo BR-5.3)
    /// </summary>
    public class CompleteConsultationRequest
    {
        [Required(ErrorMessage = "Bắt buộc nhập biên bản tóm tắt giải pháp tư vấn trước khi kết thúc ca (Quy định BR-5.3).")]
        [MinLength(10, ErrorMessage = "Nội dung tóm tắt kết luận phải có tối thiểu 10 ký tự.")]
        [StringLength(5000, ErrorMessage = "Nội dung tóm tắt kết luận không được vượt quá 5000 ký tự.")]
        public string SharedNotes { get; set; } = string.Empty;
    }

    /// <summary>
    /// Yêu cầu báo cáo đối phương vắng mặt (No-Show quá 15 phút)
    /// </summary>
    public class ReportNoShowRequest
    {
        [StringLength(500, ErrorMessage = "Lý do không được vượt quá 500 ký tự.")]
        public string? Reason { get; set; }
    }
}
