using System.ComponentModel.DataAnnotations;
using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Application.DTOs.ConsultationFeeConfigurations
{
    public class UpdateConsultationFeeConfigDto
    {
        [Required(ErrorMessage = "Loại phiên tư vấn không được để trống.")]
        public SessionType SessionType { get; set; }

        [Range(15, 240, ErrorMessage = "Thời lượng tư vấn phải từ 15 đến 240 phút.")]
        public int DurationMinutes { get; set; }

        [Range(0, 100_000_000, ErrorMessage = "Mức phí sàn (MinFee) phải từ 0 đến 100.000.000 VNĐ.")]
        public decimal MinFee { get; set; }

        [Range(0, 100_000_000, ErrorMessage = "Mức phí trần (MaxFee) phải từ 0 đến 100.000.000 VNĐ.")]
        public decimal MaxFee { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
