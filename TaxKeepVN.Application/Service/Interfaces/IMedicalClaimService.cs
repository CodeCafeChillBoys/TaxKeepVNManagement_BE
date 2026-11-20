using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using TaxKeepVN.Application.DTOs.MedicalClaims;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface IMedicalClaimService
    {
        /// <summary>
        /// Lấy chi tiết hồ sơ y tế (Hóa đơn + Bảng kê chi phí KCB + Chi tiết các dòng + Trạng thái thẩm định).
        /// </summary>
        Task<MedicalClaimDto> GetMedicalClaimAsync(Guid userId, Guid periodId, Guid documentId);

        /// <summary>
        /// Tải lên Bảng kê chi phí KCB (Mẫu 01/KBCB) gắn trực tiếp vào Hóa đơn y tế.
        /// </summary>
        Task<MedicalClaimDto> UploadMedicalStatementAsync(Guid userId, Guid periodId, Guid receiptDocumentId, IFormFile file);

        /// <summary>
        /// Ghép nối thủ công một Bảng kê chi phí KCB đã có vào Hóa đơn y tế.
        /// </summary>
        Task<MedicalClaimDto> LinkMedicalStatementAsync(Guid userId, Guid periodId, Guid receiptDocumentId, Guid statementDocumentId);

        /// <summary>
        /// Người dùng cập nhật trạng thái giảm trừ của một dòng (ELIGIBLE/EXCLUDED) hoặc bật/tắt tính phần tự trả (SelfPayOptIn).
        /// </summary>
        Task<MedicalClaimDto> UpdateItemDeductionAsync(Guid userId, Guid periodId, Guid documentId, long itemId, UpdateItemDeductionRequestDto request);

        /// <summary>
        /// Tìm danh sách các hóa đơn y tế ứng viên để ghép với bảng kê.
        /// </summary>
        Task<List<CandidateReceiptDto>> GetCandidateReceiptsForStatementAsync(Guid userId, Guid periodId, Guid statementDocumentId);
    }
}
