using System;

namespace TaxKeepVN.Application.DTOs.Responses.Dependent
{
    /// <summary>
    /// DTO đại diện 1 document đã upload, dùng trong response chi tiết NPT.
    /// </summary>
    public class DependentDocumentDto
    {
        public Guid DocId { get; set; }

        /// <summary>
        /// Loại giấy tờ: BIRTH_CERTIFICATE | CITIZEN_ID | STUDENT_CARD | DISABILITY_CERTIFICATE | PENSION_STATEMENT | OTHER
        /// </summary>
        public string DocType { get; set; } = string.Empty;

        /// <summary>URL truy cập file đã upload (Cloudinary / S3 / MinIO...)</summary>
        public string FileUrl { get; set; } = string.Empty;

        public string FileMimeType { get; set; } = string.Empty;

        /// <summary>true nếu file có thể đọc được (không bị lỗi/hỏng)</summary>
        public bool IsReadable { get; set; }

        public DateTime UploadedAt { get; set; }
    }
}
