using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Application.DTOs.ExpertApplications
{
    public class AdminExpertApplicationQueryParameters : QueryParameters
    {
        /// <summary>
        /// Lọc theo trạng thái hồ sơ (Draft, PendingReview, NeedSupplement, Rejected, Approved)
        /// </summary>
        public ExpertApplicationStatus? Status { get; set; }
    }
}
