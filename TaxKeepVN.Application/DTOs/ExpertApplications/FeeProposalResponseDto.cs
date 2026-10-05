using System;
using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Application.DTOs.ExpertApplications
{
    /// <summary>
    /// Thông tin mức phí đề xuất trả về trong hồ sơ chuyên gia
    /// </summary>
    public class FeeProposalResponseDto
    {
        public Guid Id { get; set; }
        public SessionType SessionType { get; set; }
        public int DurationMinutes { get; set; }
        public decimal ProposedFee { get; set; }
    }
}
