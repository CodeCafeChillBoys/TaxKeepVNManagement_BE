using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.OcrAI;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface IOcrService
    {
        /// <summary>
        /// Gọi trực tiếp AI Service để trích xuất thông tin ảnh/PDF
        /// </summary>
        Task<ExtractedDependentDataDto> ExtractDocumentAsync(IFormFile file, IFormFile? backFile = null);

        /// <summary>
        /// Nghiệp vụ Case 1: Bóc tách CCCD người nộp thuế & kiểm tra trùng lặp tài khoản
        /// </summary>
        Task<UserCccdOcrResponseDto> ProcessUserCccdOcrAsync(IFormFile file, IFormFile? backFile = null);

        /// <summary>
        /// Nghiệp vụ Case 2: Bóc tách CCCD/Khai sinh người phụ thuộc & gợi ý nhóm, kiểm tra trùng lặp NPT
        /// </summary>
        Task<DependentOcrResponseDto> ProcessDependentOcrAsync(IFormFile file, IFormFile? backFile = null);

        /// <summary>
        /// Nghiệp vụ Case 3: Kiểm tra chéo (Verify) giấy tờ upload có đúng loại và đúng người phụ thuộc không
        /// </summary>
        Task ValidateDependentDocumentAsync(Dependent dependent, DocumentType expectedType, IFormFile file);
    }
}
