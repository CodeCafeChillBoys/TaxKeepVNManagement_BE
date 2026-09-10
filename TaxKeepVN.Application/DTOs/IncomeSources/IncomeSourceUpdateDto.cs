using System.ComponentModel.DataAnnotations;

namespace TaxKeepVN.Application.DTOs.IncomeSources
{
    public class IncomeSourceUpdateDto
    {
        [Required(ErrorMessage = "Tên tổ chức không được để trống.")]
        [MaxLength(255, ErrorMessage = "Tên tổ chức không được vượt quá 255 ký tự.")]
        public string CompanyName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mã số thuế tổ chức không được để trống.")]
        public string CompanyTaxCode { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }
}
