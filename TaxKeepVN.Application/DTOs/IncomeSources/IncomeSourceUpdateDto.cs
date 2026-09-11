using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TaxKeepVN.Application.DTOs.IncomeSources
{
    public class IncomeSourceUpdateDto
    {
        [Required(ErrorMessage = "Tên tổ chức chi trả thu nhập không được để trống.")]
        [MaxLength(255, ErrorMessage = "Tên tổ chức không được vượt quá 255 ký tự.")]
        public string CompanyName { get; set; } = string.Empty;

        [JsonPropertyName("companyTaxId")]
        public string? CompanyTaxId { get; set; }

        public string? CompanyTaxCode { get; set; }

        [JsonIgnore]
        public string ResolvedTaxCode => (!string.IsNullOrWhiteSpace(CompanyTaxId) ? CompanyTaxId : CompanyTaxCode ?? string.Empty).Trim();

        [Required(ErrorMessage = "Năm tính thuế không được để trống.")]
        [Range(2000, 2100, ErrorMessage = "Năm tính thuế phải từ 2000 đến 2100.")]
        public int TaxYear { get; set; } = 2026;

        [Range(0, double.MaxValue, ErrorMessage = "Tổng thu nhập không được âm.")]
        public decimal TotalIncome { get; set; } = 0;

        [Range(0, double.MaxValue, ErrorMessage = "Số thuế đã khấu trừ không được âm.")]
        public decimal TaxWithheld { get; set; } = 0;

        public bool IsActive { get; set; } = true;
    }
}
