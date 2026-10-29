using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace TaxKeepVN.Application.DTOs.ExpertApplications
{
    /// <summary>
    /// Request tải lên ảnh đại diện chuyên gia từ Camera hoặc Thư viện ảnh điện thoại (Multipart/form-data)
    /// </summary>
    public class UploadAvatarRequest
    {
        [Required(ErrorMessage = "File ảnh đại diện không được để trống.")]
        public IFormFile File { get; set; } = null!;
    }
}
