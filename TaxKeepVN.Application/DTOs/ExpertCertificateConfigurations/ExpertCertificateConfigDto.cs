using System;

namespace TaxKeepVN.Application.DTOs.ExpertCertificateConfigurations
{
    public class ExpertCertificateConfigDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? DefaultIssuingAuthority { get; set; }
        public bool RequiresExpiryDate { get; set; }
        public string? AiKeywords { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}