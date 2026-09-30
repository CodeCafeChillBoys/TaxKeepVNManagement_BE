using System.ComponentModel.DataAnnotations;

namespace TaxKeepVN.Application.DTOs.ExpertApplications
{
    /// <summary>
    /// Đề xuất mức phí tư vấn theo phiên
    /// </summary>
    public class FeeProposalRequestDto
    {
        [Required(ErrorMessage = "Loại phiên tư vấn không được để trống")]
        public string SessionType { get; set; } = "ONLINE_MEETING"; // ONLINE_MEETING, CHAT, VOICE_CALL

        [Range(15, 240, ErrorMessage = "Thời lượng tư vấn từ 15 đến 240 phút")]
        public int DurationMinutes { get; set; } = 60;

        [Range(0, 100_000_000, ErrorMessage = "Mức phí đề xuất không hợp lệ (0 - 100.000.000 VNĐ)")]
        public decimal ProposedFee { get; set; }
    }

}
