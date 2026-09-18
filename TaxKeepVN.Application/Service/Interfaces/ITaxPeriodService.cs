using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
    }
}