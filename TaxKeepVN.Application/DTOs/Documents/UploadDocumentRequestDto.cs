using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace TaxKeepVN.Application.DTOs.Documents
{
    public class UploadDocumentRequestDto
    {
        [Required(ErrorMessage = "Loại giấy tờ không được để trống.")]
        public string DocType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn tệp chứng từ cần tải lên.")]
        public IFormFile File { get; set; } = null!;
    }
}
