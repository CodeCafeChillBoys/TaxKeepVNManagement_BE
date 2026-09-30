using System;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.ExpertApplications;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface IExpertApplicationService
    {
        /// <summary>
        /// Tạo mới hoặc cập nhật bản nháp hồ sơ chuyên gia (Draft)
        /// </summary>
        Task<ExpertApplicationDetailResponse> SaveDraftAsync(Guid userId, SaveExpertApplicationDraftRequest request);

        /// <summary>
        /// Tải lên và đính kèm 1 chứng chỉ vào hồ sơ hiện tại
        /// </summary>
        Task<CertificateResponseDto> UploadCertificateAsync(Guid userId, UploadCertificateRequest request);

        /// <summary>
        /// Xóa 1 chứng chỉ khỏi hồ sơ bản nháp
        /// </summary>
        Task DeleteCertificateAsync(Guid userId, Guid certificateId);

        /// <summary>
        /// Nộp hồ sơ chính thức (Draft -> PendingReview)
        /// </summary>
        Task<ExpertApplicationDetailResponse> SubmitApplicationAsync(Guid userId);

        /// <summary>
        /// Lấy thông tin hồ sơ đăng ký mới nhất của người dùng hiện tại
        /// </summary>
        Task<ExpertApplicationDetailResponse?> GetMyApplicationAsync(Guid userId);
    }
}
