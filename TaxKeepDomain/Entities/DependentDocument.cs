using System;
using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Domain.Entities
{
    public class DependentDocument
    {
        public Guid Id { get; set; }
        public Guid DependentId { get; set; }
        public DocumentType DocType { get; set; }
        public string FileUrl { get; set; } = string.Empty;
        public string FileMimeType { get; set; } = string.Empty;
        public bool IsReadable { get; set; }
        public DateTime UploadedAt { get; set; }

        // Navigation property
        public Dependent Dependent { get; set; } = null!;
    }
}
