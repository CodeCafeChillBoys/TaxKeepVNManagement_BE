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
        /// Tải lên ảnh chân dung / avatar chuyên gia (hỗ trợ chụp trực tiếp từ Camera điện thoại hoặc thư viện ảnh)
        /// </summary>
        Task<UploadAvatarResponseDto> UploadAvatarAsync(Guid userId, UploadAvatarRequest request);

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

        // ── ADMIN / REVIEWER METHODS (Bước 4 Thẩm định) ────────────────────
        /// <summary>
        /// Lấy danh sách hồ sơ đăng ký chuyên gia (phân trang, lọc theo status, search)
        /// </summary>
        Task<TaxKeepVN.Application.DTOs.Common.PagedResult<ExpertApplicationListItemResponse>> GetApplicationsAsync(AdminExpertApplicationQueryParameters query);

        /// <summary>
        /// Lấy chi tiết hồ sơ chuyên gia theo Id cho Admin
        /// </summary>
        Task<ExpertApplicationDetailResponse> GetApplicationByIdAsync(Guid applicationId);

        /// <summary>
        /// Thẩm định chứng chỉ đơn lẻ trong hồ sơ
        /// </summary>
        Task<CertificateResponseDto> VerifyCertificateAsync(Guid adminId, Guid applicationId, Guid certificateId, VerifyCertificateRequest request);

        /// <summary>
        /// Phê duyệt hồ sơ chuyên gia (Approved - Nâng role thành expert, tạo ExpertProfile)
        /// </summary>
        Task<ExpertApplicationDetailResponse> ApproveApplicationAsync(Guid adminId, Guid applicationId, ApproveExpertApplicationRequest? request);

        /// <summary>
        /// Từ chối hồ sơ chuyên gia kèm lý do (Rejected - BR-09)
        /// </summary>
        Task<ExpertApplicationDetailResponse> RejectApplicationAsync(Guid adminId, Guid applicationId, RejectExpertApplicationRequest request);

        /// <summary>
        /// Yêu cầu bổ sung thông tin hồ sơ kèm nội dung (NeedSupplement - BR-10)
        /// </summary>
        Task<ExpertApplicationDetailResponse> RequestSupplementAsync(Guid adminId, Guid applicationId, RequestSupplementRequest request);
    }
}
