using System;
using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Application.DTOs.ExpertApplications
{
    public class ExpertApplicationAuditResponseDto
    {
        public Guid Id { get; set; }
        public Guid ActorId { get; set; }
        public string? ActorName { get; set; }
        public ApplicationAuditAction Action { get; set; }
        public ExpertApplicationStatus? FromStatus { get; set; }
        public ExpertApplicationStatus ToStatus { get; set; }
        public string? Notes { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
