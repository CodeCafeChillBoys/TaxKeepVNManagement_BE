using System;

namespace TaxKeepVN.Domain.Entities
{
    /// <summary>
    /// Bảng cấu hình danh mục các loại chứng chỉ hành nghề được chấp nhận (Do Admin quản lý)
    /// </summary>
    public class ExpertCertificateConfiguration
    {
        public int Id { get; set; }

        /// <summary>Mã chuẩn hóa (VD: TAX_AGENT, CPA, ACCOUNTANT, CHIEF_ACCOUNTANT...)</summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>Tên hiển thị loại chứng chỉ</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Mô tả chi tiết / Hướng dẫn tải lên cho chuyên gia</summary>
        public string? Description { get; set; }

        /// <summary>Cơ quan cấp mẫu mặc định (VD: Tổng cục Thuế, Bộ Tài chính...)</summary>
        public string? DefaultIssuingAuthority { get; set; }

        /// <summary>Chứng chỉ có bắt buộc phải có thời hạn hiệu lực không?</summary>
        public bool RequiresExpiryDate { get; set; } = false;

        /// <summary>Từ khóa / Nhãn nhận diện hỗ trợ AI OCR so khớp (ví dụ: "đại lý thuế, tax agent")</summary>
        public string? AiKeywords { get; set; }

        /// <summary>Thứ tự hiển thị trên danh sách lựa chọn</summary>
        public int DisplayOrder { get; set; } = 0;

        /// <summary>Trạng thái áp dụng (true: cho phép dùng; false: ngừng áp dụng)</summary>
        public bool IsActive { get; set; } = true;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}