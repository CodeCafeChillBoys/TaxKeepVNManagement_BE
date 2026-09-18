using System;
using System.Collections.Generic;

namespace TaxKeepVN.Application.DTOs.Documents
{
    public class BatchUploadDocumentsResponseDto
    {
        public Guid PeriodId { get; set; }
        public int TotalUploaded { get; set; }
        public List<UploadedDocumentItemDto> Documents { get; set; } = new();
    }

    public class UploadedDocumentItemDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid PeriodId { get; set; }
        public string? DocTypeCode { get; set; }
        public string? OriginalFilename { get; set; }
        public string FileUrl { get; set; } = string.Empty;
        public string Status { get; set; } = "UPLOADED";
        public DateTime CreatedAt { get; set; }
    }
}
