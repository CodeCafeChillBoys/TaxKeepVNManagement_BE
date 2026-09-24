using System;
using System.Collections.Generic;
using System.Text.Json;
using TaxKeepVN.Application.Law.Normalizer;

namespace TaxKeepVN.Application.DTOs.Law
{
    public class RevisionDocumentDto
    {
        public Guid Id { get; set; }
        public string DocumentNumber { get; set; } = string.Empty;
        public string? Title { get; set; }
    }

    public class RevisionDto
    {
        public int RevisionNo { get; set; }
        public int? ParentRevisionNo { get; set; }
        public string Message { get; set; } = string.Empty;
        public RevisionDocumentDto? Document { get; set; }
        public string? Summary { get; set; }
        public string MergedBy { get; set; } = string.Empty;
        public DateTime MergedAt { get; set; }
    }

    public class RevisionChangeDto
    {
        public string RuleCode { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string OpType { get; set; } = string.Empty;
        public RuleValueSnapshot? Before { get; set; }
        public RuleValueSnapshot? After { get; set; }
    }

    public class RevisionDetailDto : RevisionDto
    {
        public List<RevisionChangeDto> Changes { get; set; } = new();
    }
}
