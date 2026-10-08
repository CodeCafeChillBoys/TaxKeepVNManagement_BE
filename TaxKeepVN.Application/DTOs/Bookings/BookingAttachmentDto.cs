namespace TaxKeepVN.Application.DTOs.Bookings
{
    /// <summary>
    /// Thông tin tệp chứng từ đính kèm của phiên tư vấn
    /// </summary>
    public class BookingAttachmentDto
    {
        public Guid Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
    }
}
