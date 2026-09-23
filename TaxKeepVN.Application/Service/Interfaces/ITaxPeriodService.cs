using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Documents;
using TaxKeepVN.Application.DTOs.TaxPeriods;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface ITaxPeriodService
    {
        /// <summary>
        /// Khởi tạo kỳ kê khai mới hoặc lấy kỳ hiện có nếu chưa bị khóa.
        /// </summary>
        Task<TaxPeriodResponseDto> InitOrGetPeriodAsync(Guid userId, int taxYear);

        /// <summary>
        /// Tải lên nhiều chứng từ thuế theo kỳ kê khai và kích hoạt bóc tách OCR qua RabbitMQ.
        /// </summary>
        Task<TaxKeepVN.Application.DTOs.Documents.BatchUploadDocumentsResponseDto> BatchUploadDocumentsAsync(Guid userId, Guid periodId, IList<Microsoft.AspNetCore.Http.IFormFile> files);

        /// <summary>
        /// Xác nhận và lưu chính thức dữ liệu sau khi người dùng review kết quả bóc tách từ AI.
        /// </summary>
        Task<TaxKeepVN.Application.DTOs.Documents.DocumentReviewResponseDto> ConfirmDocumentReviewAsync(Guid userId, Guid periodId, Guid documentId, TaxKeepVN.Application.DTOs.Documents.ConfirmDocumentReviewRequestDto dto);

        /// <summary>
        /// Kích hoạt lại bóc tách OCR cho một chứng từ cụ thể (chỉ cho phép khi ở trạng thái UPLOADED).
        /// </summary>
        Task TriggerDocumentOcrAsync(Guid userId, Guid periodId, Guid documentId);


        /// <summary>
        /// Lấy danh sách chứng từ theo kỳ kê khai (hỗ trợ phân trang, lọc theo DocType, Status, tìm kiếm, mặc định sắp xếp mới nhất lên đầu).
        /// </summary>
        Task<List<DocumentReviewResponseDto>> GetDocumentsAsync(Guid userId, Guid periodId);
        /// <summary>
        /// Xem chi tiết một chứng từ cụ thể theo ID kèm theo items và thông tin loại chứng từ.
        /// </summary>
        Task<DocumentReviewResponseDto> GetDocumentByIdAsync(Guid userId, Guid periodId, Guid documentId);

        /// <summary>
        /// Nộp / hoàn tất kỳ kê khai thuế (chuyển status sang SUBMITTED và khóa chỉnh sửa chứng từ).
        /// </summary>
        Task<TaxPeriodResponseDto> SubmitTaxPeriodAsync(Guid userId, Guid periodId);
    }
}