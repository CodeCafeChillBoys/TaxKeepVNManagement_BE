using Microsoft.AspNetCore.Http;

namespace TaxKeepVN.Application.DTOs.OcrAI
{
    public class IncomeOcrRequestDto
    {
        public IFormFile File { get; set; } = null!;
        public int? TargetMonth { get; set; }
        public int? TargetYear { get; set; }


    }
}
