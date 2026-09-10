using System;
using System.Collections.Generic;
using TaxKeepVN.Domain.Entities;

namespace TaxKeepVN.Application.DTOs
{
    public class UploadDocumentResultDto
    {
        public DependentDocument Document { get; set; } = null!;
        public bool IsProfileComplete { get; set; }
        public List<string> MissingDocuments { get; set; } = new List<string>();
    }
}
