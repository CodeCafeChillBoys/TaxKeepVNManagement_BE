using System;

namespace TaxKeepVN.Domain.Entities
{
    /// <summary>
    /// Danh mục dịch vụ / thuốc / thiết bị y tế thuộc phạm vi chi trả của BHYT.
    /// Dùng cho rule engine đối soát chi phí y tế khi người dùng không sử dụng thẻ BHYT (hoặc cần tra cứu danh mục).
    /// </summary>
    public class BhytCoverageCatalog
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>Loại danh mục: DRUG (Thuốc), SERVICE (Dịch vụ kỹ thuật/khám/ngày giường), SUPPLY (Thiết bị/vật tư y tế)</summary>
        public string CatalogType { get; set; } = string.Empty;

        /// <summary>Mã dịch vụ / mã hoạt chất / mã thuốc theo danh mục BYT/BHYT</summary>
        public string? Code { get; set; }

        /// <summary>Tên hiển thị đầy đủ</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Tên chuẩn hóa (viết hoa/không dấu/bỏ ký tự thừa) để phục vụ tra cứu mờ</summary>
        public string NormalizedName { get; set; } = string.Empty;

        /// <summary>Tên hoạt chất (đối với thuốc)</summary>
        public string? ActiveIngredient { get; set; }

        /// <summary>Căn cứ pháp lý ban hành (VD: "TT 20/2022/TT-BYT", "TT 39/2024/TT-BYT")</summary>
        public string? LegalDocumentRef { get; set; }

        /// <summary>Ngày có hiệu lực</summary>
        public DateOnly? EffectiveFrom { get; set; }

        /// <summary>Ngày hết hiệu lực</summary>
        public DateOnly? EffectiveTo { get; set; }

        /// <summary>Đang áp dụng</summary>
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
