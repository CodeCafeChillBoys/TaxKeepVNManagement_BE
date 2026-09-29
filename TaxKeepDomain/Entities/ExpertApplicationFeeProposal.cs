using System;

namespace TaxKeepVN.Domain.Entities
{
    /// <summary>
    /// Mức phí tư vấn đề xuất trong hồ sơ (Mục 5.5 & Mục 12)
    /// </summary>
    public class ExpertApplicationFeeProposal
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid ApplicationId { get; set; }

        /// <summary>Loại phiên tư vấn (VD: ONLINE_MEETING, CHAT, VOICE_CALL)</summary>
        public string SessionType { get; set; } = "ONLINE_MEETING";

        /// <summary>Thời lượng tư vấn tính theo phút (VD: 30, 60)</summary>
        public int DurationMinutes { get; set; }

        /// <summary>Mức phí đề xuất (VNĐ)</summary>
        public decimal ProposedFee { get; set; }

        // Navigation property
        public ExpertApplication? Application { get; set; }
    }
}
