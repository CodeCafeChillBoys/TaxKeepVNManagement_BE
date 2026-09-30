using System.ComponentModel.DataAnnotations;
using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Application.DTOs.ExpertApplications
{
    /// <summary>
    /// Đề xuất mức phí tư vấn theo phiên
    /// </summary>
    public class FeeProposalRequestDto
    {
        [Required(ErrorMessage = "Loại phiên tư vấn không được để trống")]
        public SessionType SessionType { get; set; } = SessionType.ONLINE_MEETING;

        [Range(15, 240, ErrorMessage = "Thời lượng tư vấn từ 15 đến 240 phút")]
        public int DurationMinutes { get; set; } = 60;

        [Range(0, 100_000_000, ErrorMessage = "Mức phí đề xuất không hợp lệ (0 - 100.000.000 VNĐ)")]
        public decimal ProposedFee { get; set; }
    }

}
