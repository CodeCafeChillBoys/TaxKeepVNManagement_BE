using System;

namespace TaxKeepVN.Domain.Entities
{
    public class DocumentItem
    {
        /// <summary>Khóa chính tự tăng</summary>
        public long Id { get; set; }

        /// <summary>Thuộc hóa đơn nào</summary>
        public Guid DocumentId { get; set; }

        /// <summary>STT dòng (1, 2...)</summary>
        public int ItemOrder { get; set; }

        /// <summary>Tên hàng hóa/dịch vụ</summary>
        public string ItemName { get; set; } = string.Empty;

        /// <summary>Đơn vị tính (Lần, cái...)</summary>
        public string? Unit { get; set; }

        /// <summary>Số lượng (1, 4...)</summary>
        public decimal Quantity { get; set; } = 0;

        /// <summary>Đơn giá</summary>
        public decimal UnitPrice { get; set; } = 0;

        /// <summary>Thành tiền</summary>
        public decimal TotalPrice { get; set; } = 0;

        // Navigation property
        public Document? Document { get; set; }
    }
}
