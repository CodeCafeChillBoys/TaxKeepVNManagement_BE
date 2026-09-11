using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TaxKeepVN.Application.DTOs.IncomeSources
{
    public class IncomeSourceCreateDto
    {
        [Required(ErrorMessage = "Tên tổ chức chi trả thu nhập không được để trống.")]
        [MaxLength(255, ErrorMessage = "Tên tổ chức không được vượt quá 255 ký tự.")]
        public string CompanyName { get; set; } = string.Empty;

        /// <summary>
        /// Mã số thuế công ty (Đặc tả dùng companyTaxId hoặc companyTaxCode)
        /// </summary>
        [JsonPropertyName("companyTaxId")]
        public string? CompanyTaxId { get; set; }

        public string? CompanyTaxCode { get; set; }

        /// <summary>Thuộc tính trích xuất MST từ 1 trong 2 trường</summary>
        [JsonIgnore]
        public string ResolvedTaxCode => (!string.IsNullOrWhiteSpace(CompanyTaxId) ? CompanyTaxId : CompanyTaxCode ?? string.Empty).Trim();

        /// <summary>Năm tính thuế (ví dụ: 2026)</summary>
        [Required(ErrorMessage = "Năm tính thuế không được để trống.")]
        [Range(2000, 2100, ErrorMessage = "Năm tính thuế phải từ 2000 đến 2100.")]
        public int TaxYear { get; set; } = 2026;

        /// <summary>Tổng thu nhập trong năm từ nơi này (VNĐ)</summary>
        [Range(0, double.MaxValue, ErrorMessage = "Tổng thu nhập không được âm.")]
        public decimal TotalIncome { get; set; } = 0;

        /// <summary>Số thuế TNCN đã khấu trừ tại nguồn (VNĐ)</summary>
        [Range(0, double.MaxValue, ErrorMessage = "Số thuế đã khấu trừ không được âm.")]
        public decimal TaxWithheld { get; set; } = 0;
    }
}
