using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TaxKeepVN.Application.DTOs.TaxPeriods
{
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class TaxPeriodResponseDto
    {
        public Guid PeriodId { get; set; }
        public int TaxYear { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
    }
}