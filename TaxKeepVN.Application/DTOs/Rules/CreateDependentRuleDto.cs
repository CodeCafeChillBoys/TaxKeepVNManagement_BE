using System.ComponentModel.DataAnnotations;

namespace TaxKeepVN.Application.DTOs.Rules
{
    public class CreateDependentRuleDto
    {
        [Required(ErrorMessage = "Nhóm đối tượng áp dụng (target_group) không được để trống.")]
        [MaxLength(50, ErrorMessage = "Nhóm đối tượng không được vượt quá 50 ký tự.")]
        public string TargetGroup { get; set; } = string.Empty;

        [Required(ErrorMessage = "Loại giấy tờ (doc_type) không được để trống.")]
        [MaxLength(50, ErrorMessage = "Loại giấy tờ không được vượt quá 50 ký tự.")]
        public string DocType { get; set; } = string.Empty;

        public bool IsMandatory { get; set; } = true;

        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
