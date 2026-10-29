using System.Collections.Generic;

namespace TaxKeepVN.Application.DTOs.Responses.Income
{
    public class IncomeGroupedResponseDto
    {
        public string OrganizationName { get; set; } = string.Empty;
        public string? TaxIdNumber { get; set; }
        public decimal TotalIncomeCompany { get; set; }
        public List<IncomeResponseDto> Details { get; set; } = new List<IncomeResponseDto>();
    }
}
