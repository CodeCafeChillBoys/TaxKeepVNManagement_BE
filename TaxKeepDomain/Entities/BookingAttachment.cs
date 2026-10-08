namespace TaxKeepVN.Domain.Entities
{
    /// <summary>
    /// Tệp đính kèm / chứng từ / tài liệu mẫu do khách hàng gửi khi đặt lịch tư vấn (Đặc tả 3)
    /// </summary>
    public class BookingAttachment
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>FK trỏ tới Booking</summary>
        public Guid BookingId { get; set; }

        /// <summary>Tên file gốc do người dùng tải lên</summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>Đường dẫn lưu trữ an toàn (Storage URL / Private Path)</summary>
        public string FileUrl { get; set; } = string.Empty;

        /// <summary>Dung lượng tệp tính theo Byte</summary>
        public long FileSize { get; set; }

        /// <summary>MIME type của tệp (application/pdf, v.v.)</summary>
        public string ContentType { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        // Navigation property
        public Booking? Booking { get; set; }
    }
}
