using System.ComponentModel.DataAnnotations;

namespace TaxKeepVN.Application.DTOs.ExpertApplications
{
    public class RequestSupplementRequest
    {
        [Required(ErrorMessage = "Nội dung yêu cầu bổ sung không được để trống (BR-10).")]
        [StringLength(1000, MinimumLength = 5, ErrorMessage = "Nội dung yêu cầu bổ sung phải từ 5 đến 1000 ký tự.")]
        public string Reason { get; set; } = string.Empty;
    }
}
