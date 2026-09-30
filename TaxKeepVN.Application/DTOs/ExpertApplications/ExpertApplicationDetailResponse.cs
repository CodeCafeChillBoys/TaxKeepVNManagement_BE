using System;
using System.Collections.Generic;
using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Application.DTOs.ExpertApplications
{
    /// <summary>
    /// Response chi tiết hồ sơ chuyên gia trả về cho Frontend
    /// </summary>
    public class ExpertApplicationDetailResponse
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string ApplicationNumber { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string JobTitle { get; set; } = string.Empty;
        public string? CompanyName { get; set; }
        public string? Bio { get; set; }
        public int YearsOfExperience { get; set; }
        public string? CurrentPosition { get; set; }
        public string ExperienceDescription { get; set; } = string.Empty;
        public ExpertApplicationStatus Status { get; set; }
        public DateTimeOffset? SubmittedAt { get; set; }
        public Guid? ReviewedBy { get; set; }
        public DateTimeOffset? ReviewedAt { get; set; }
        public string? RejectionReason { get; set; }
        public string? SupplementRequestReason { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }

        public List<SpecializationDtoItem> Specializations { get; set; } = new();
        public List<CertificateResponseDto> Certificates { get; set; } = new();
        public List<FeeProposalResponseDto> FeeProposals { get; set; } = new();
        public List<ExpertApplicationAuditResponseDto> Audits { get; set; } = new();
    }
}
