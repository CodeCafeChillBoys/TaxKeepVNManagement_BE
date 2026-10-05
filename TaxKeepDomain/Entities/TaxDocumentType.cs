using System.Collections.Generic;

namespace TaxKeepVN.Domain.Entities
{
    public class TaxDocumentType
    {
        /// <summary>SALES_INVOICE, VAT_INVOICE, WITHHOLDING_VOUCHER</summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>Tên loại (Hóa đơn bán hàng, Hóa đơn GTGT...)</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Có được dùng giảm trừ/tính thuế không</summary>
        public bool IsTaxEligible { get; set; } = true;

        /// <summary>Mô tả chi tiết đặc điểm nhận diện chứng từ (truyền cho AI và hiển thị cho Admin)</summary>
        public string? Description { get; set; }

        // Navigation properties
        public ICollection<Document> Documents { get; set; } = new List<Document>();
    }
}
