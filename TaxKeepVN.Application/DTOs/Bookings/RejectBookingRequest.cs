using System.ComponentModel.DataAnnotations;

namespace TaxKeepVN.Application.DTOs.Bookings
{
    /// <summary>
    /// Request chuyên gia từ chối tiếp nhận ca tư vấn trong vòng 2 giờ
    /// </summary>
    public class RejectBookingRequest
    {
        [Required(ErrorMessage = "Vui lòng nhập lý do từ chối tiếp nhận ca tư vấn.")]
        [StringLength(500, MinimumLength = 5, ErrorMessage = "Lý do từ chối phải từ 5 đến 500 ký tự.")]
        public string Reason { get; set; } = string.Empty;
    }
}
