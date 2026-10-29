using System;
using System.Collections.Generic;
using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Application.DTOs.ExpertApplications
{
    public class ExpertApplicationListItemResponse
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string ApplicationNumber { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string JobTitle { get; set; } = string.Empty;
        public string? CompanyName { get; set; }
        public int YearsOfExperience { get; set; }
        public ExpertApplicationStatus Status { get; set; }
        public DateTimeOffset? SubmittedAt { get; set; }
        public DateTimeOffset? ReviewedAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public List<string> SpecializationNames { get; set; } = new();
        public int CertificateCount { get; set; }
        public int VerifiedCertificateCount { get; set; }
    }
}
