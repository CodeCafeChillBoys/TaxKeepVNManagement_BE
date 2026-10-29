using System.ComponentModel.DataAnnotations;

namespace TaxKeepVN.Application.DTOs.ExpertApplications
{
    public class RejectExpertApplicationRequest
    {
        [Required(ErrorMessage = "Lý do từ chối không được để trống (BR-09).")]
        [StringLength(1000, MinimumLength = 5, ErrorMessage = "Lý do từ chối phải từ 5 đến 1000 ký tự.")]
        public string Reason { get; set; } = string.Empty;
    }
}
