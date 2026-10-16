using System;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Consultations;

namespace TaxKeepVN.Application.Service.Interfaces
{
    /// <summary>
    /// Giao diện Service xử lý Phiên tư vấn trực tuyến 1-1 (Đặc tả 5)
    /// </summary>
    public interface IConsultationService
    {
        /// <summary>
        /// Khách hàng hoặc Chuyên gia điểm danh vào phòng tư vấn Jitsi Meet
        /// </summary>
        Task<JoinConsultationResponse> JoinSessionAsync(Guid currentUserId, string currentUserRole, Guid bookingId);

        /// <summary>
        /// Xem chi tiết trạng thái phiên tư vấn (thời gian vào, thời lượng, ghi chú, url phòng họp)
        /// </summary>
        Task<ConsultationSessionResponse> GetSessionAsync(Guid currentUserId, string currentUserRole, Guid bookingId);

        /// <summary>
        /// Xem toàn bộ hồ sơ thuế được chia sẻ của khách hàng (Nguồn thu nhập, NPT, Chứng từ Y tế/Giáo dục, Bản tính nháp)
        /// </summary>
        Task<SharedTaxDossierResponse> GetSharedTaxDossierAsync(Guid currentUserId, string currentUserRole, Guid bookingId);

        /// <summary>
        /// Chuyên gia cập nhật ghi chú/bản nháp kết luận tư vấn trong phòng
        /// </summary>
        Task<ConsultationSessionResponse> UpdateNotesAsync(Guid currentUserId, string currentUserRole, Guid bookingId, UpdateConsultationNotesRequest request);

        /// <summary>
        /// Chuyên gia hoàn thành phiên tư vấn (Bắt buộc nhập biên bản tóm tắt kết luận theo BR-5.3)
        /// </summary>
        Task<ConsultationSessionResponse> CompleteSessionAsync(Guid currentUserId, string currentUserRole, Guid bookingId, CompleteConsultationRequest request);

        /// <summary>
        /// Báo cáo đối phương vắng mặt không phép (No-Show quá 15 phút)
        /// </summary>
        Task<ConsultationSessionResponse> ReportNoShowAsync(Guid currentUserId, string currentUserRole, Guid bookingId, ReportNoShowRequest request);

        /// <summary>
        /// Lấy thông tin nhắc nhở lịch hẹn meeting trực tuyến sắp tới dành cho User hoặc Expert (Dashboard & Banner)
        /// </summary>
        Task<UpcomingMeetingReminderDto> GetUpcomingMeetingReminderAsync(Guid currentUserId);
    }
}
