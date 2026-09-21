using System;

namespace TaxKeepVN.Application.DTOs.Documents
{
    public class ConfirmDocumentReviewRequestDto
    {
        /// <summary>Mã loại chứng từ (VD: MEDICAL_EXPENSE_INVOICE, TUITION_FEE_INVOICE)</summary>
        public string? DocTypeCode { get; set; }

        /// <summary>Ký hiệu hóa đơn</summary>
        public string? InvoiceSeries { get; set; }

        /// <summary>Số hóa đơn</summary>
        public string? InvoiceNumber { get; set; }

        /// <summary>Ngày lập hóa đơn</summary>
        public DateOnly? InvoiceDate { get; set; }

        /// <summary>Tên đơn vị người bán / Bệnh viện / Trường học</summary>
        public string? SellerName { get; set; }

        /// <summary>Mã số thuế người bán</summary>
        public string? SellerTaxCode { get; set; }

        /// <summary>Địa chỉ người bán</summary>
        public string? SellerAddress { get; set; }

        /// <summary>Điện thoại người bán</summary>
        public string? SellerPhone { get; set; }

        /// <summary>Tên người mua / Bệnh nhân / Học sinh</summary>
        public string? BuyerName { get; set; }

        /// <summary>Mã số thuế người mua (nếu có)</summary>
        public string? BuyerTaxCode { get; set; }

        /// <summary>Số CCCD người mua</summary>
        public string? BuyerIdCard { get; set; }

        /// <summary>Địa chỉ người mua</summary>
        public string? BuyerAddress { get; set; }

        /// <summary>Hình thức thanh toán (QR, Chuyển khoản, Tiền mặt...)</summary>
        public string? PaymentMethod { get; set; }

        /// <summary>Tổng tiền thanh toán</summary>
        public decimal? TotalAmount { get; set; }

        /// <summary>Tổng tiền bằng chữ</summary>
        public string? TotalAmountInWords { get; set; }

        /// <summary>Link tra cứu hóa đơn điện tử</summary>
        public string? LookupUrl { get; set; }

        /// <summary>Mã tra cứu hóa đơn</summary>
        public string? LookupCode { get; set; }

        /// <summary>Năm chứng từ</summary>
        public short? ExtractedYear { get; set; }

        /// <summary>Hợp lệ năm tính thuế</summary>
        public bool? IsYearValid { get; set; }

        /// <summary>Hợp lệ danh tính</summary>
        public bool? IsIdentityValid { get; set; }

        /// <summary>Cam kết: Khoản chi phí này chưa được bồi hoàn từ bảo hiểm y tế hoặc nguồn tài trợ khác</summary>
        public bool? IsNotReimbursed { get; set; }

        /// <summary>Bảng chi tiết các mặt hàng, dịch vụ, viện phí, học phí</summary>
        public System.Collections.Generic.List<TaxKeepVN.Application.DTOs.TaxAI.InvoiceLineItemDto>? Items { get; set; }
    }
}
