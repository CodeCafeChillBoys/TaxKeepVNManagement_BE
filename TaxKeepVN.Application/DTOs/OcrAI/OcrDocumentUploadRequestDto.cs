using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace TaxKeepVN.Application.DTOs.OcrAI
{
    public class OcrDocumentUploadRequestDto
    {
        [Required(ErrorMessage = "Vui lòng chọn file ảnh giấy tờ.")]
        public IFormFile File { get; set; } = null!;

        public IFormFile? BackFile { get; set; }
    }
}
