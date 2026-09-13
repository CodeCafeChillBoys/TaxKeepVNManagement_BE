using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace TaxKeepVN.Application.DTOs.TaxAI
{
    public class TaxDocumentUploadRequest
    {
        [Required(ErrorMessage = "Vui lòng chọn file PDF luật thuế.")]
        public IFormFile File { get; set; } = null!;

        [Required(ErrorMessage = "Năm thuế không được để trống.")]
        public int TaxYear { get; set; }

        public string? Name { get; set; }

        public string? SourceUrl { get; set; }
    }
}
