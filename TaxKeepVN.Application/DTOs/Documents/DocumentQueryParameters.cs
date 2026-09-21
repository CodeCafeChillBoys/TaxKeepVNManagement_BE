using TaxKeepVN.Application.DTOs.Common;

namespace TaxKeepVN.Application.DTOs.Documents
{
    /// <summary>
    /// Tham số truy vấn cho danh sách chứng từ thuế trong kỳ kê khai
    /// </summary>
    public class DocumentQueryParameters : QueryParameters
    {
        /// <summary>
        /// Lọc theo trạng thái chứng từ: UPLOADED | EXTRACTED | CONFIRMED | FAILED
        /// </summary>
        public string? Status { get; set; }

        /// <summary>
        /// Lọc theo mã loại chứng từ: SALES_INVOICE | VAT_INVOICE | WITHHOLDING_VOUCHER
        /// </summary>
        public string? DocTypeCode { get; set; }
    }
}
