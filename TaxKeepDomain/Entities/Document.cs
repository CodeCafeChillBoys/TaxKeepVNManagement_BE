using System;
using System.Collections.Generic;

namespace TaxKeepVN.Domain.Entities
{
    public class Document
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>Thuộc kỳ kê khai nào</summary>
        public Guid PeriodId { get; set; }

        /// <summary>Mã loại chứng từ (SALES_INVOICE, VAT_INVOICE, WITHHOLDING_VOUCHER) - Cho phép null khi mới upload</summary>
        public string? DocTypeCode { get; set; }

        /// <summary>Đường dẫn file ảnh / PDF</summary>
        public string FileUrl { get; set; } = string.Empty;

        /// <summary>Tên file ban đầu khi tải lên</summary>
        public string? OriginalFilename { get; set; }

        /// <summary>Ký hiệu hóa đơn (VD: 2C26TBI)</summary>
        public string? InvoiceSeries { get; set; }

        /// <summary>Số hóa đơn (VD: 82621)</summary>
        public string? InvoiceNumber { get; set; }

        /// <summary>Ngày lập hóa đơn (VD: 2026-07-24)</summary>
        public DateOnly? InvoiceDate { get; set; }

        /// <summary>Tên đơn vị/công ty người bán</summary>
        public string? SellerName { get; set; }

        /// <summary>MST người bán</summary>
        public string? SellerTaxCode { get; set; }

        /// <summary>Địa chỉ người bán</summary>
        public string? SellerAddress { get; set; }

        /// <summary>Điện thoại người bán</summary>
        public string? SellerPhone { get; set; }

        /// <summary>Họ tên người mua</summary>
        public string? BuyerName { get; set; }

        /// <summary>MST người mua (nếu có)</summary>
        public string? BuyerTaxCode { get; set; }

        /// <summary>Số CCCD người mua</summary>
        public string? BuyerIdCard { get; set; }

        /// <summary>Địa chỉ người mua</summary>
        public string? BuyerAddress { get; set; }

        /// <summary>Hình thức thanh toán (QR, Chuyển khoản...)</summary>
        public string? PaymentMethod { get; set; }

        /// <summary>Tổng cộng tiền thanh toán</summary>
        public decimal? TotalAmount { get; set; }

        /// <summary>Số tiền bằng chữ</summary>
        public string? TotalAmountInWords { get; set; }

        /// <summary>Link tra cứu HĐĐT</summary>
        public string? LookupUrl { get; set; }

        /// <summary>Mã bí mật tra cứu</summary>
        public string? LookupCode { get; set; }

        /// <summary>Năm AI bóc tách</summary>
        public short? ExtractedYear { get; set; }

        /// <summary>Có khớp năm kê khai</summary>
        public bool? IsYearValid { get; set; }

        /// <summary>Có khớp CCCD/Tên với User</summary>
        public bool? IsIdentityValid { get; set; }

        /// <summary>Cam kết: Khoản chi phí này chưa được bồi hoàn từ bảo hiểm y tế hoặc nguồn tài trợ khác</summary>
        public bool? IsNotReimbursed { get; set; } = false;

        /// <summary>UPLOADED, EXTRACTED, CONFIRMED</summary>
        public string Status { get; set; } = "UPLOADED";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public TaxPeriod? Period { get; set; }
        public TaxDocumentType? DocType { get; set; }
        public ICollection<DocumentItem> Items { get; set; } = new List<DocumentItem>();
    }
}