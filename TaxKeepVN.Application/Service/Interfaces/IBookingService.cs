using TaxKeepVN.Application.DTOs.Bookings;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Experts;
using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Application.Service.Interfaces
{
    /// <summary>
    /// Giao diện Service xử lý quy trình Đặt lịch tư vấn (Đặc tả 3)
    /// </summary>
    public interface IBookingService
    {
        /// <summary>
        /// Lấy lịch rảnh khả dụng của chuyên gia (đã áp dụng Lead-time và loại trừ slot đang bị hold)
        /// </summary>
        Task<List<ExpertAvailableSlotDto>> GetAvailableSlotsForBookingAsync(Guid expertProfileId, DateOnly? fromDate = null, int days = 14);

        /// <summary>
        /// Xem trước tóm tắt thông tin đặt lịch và tính toán chi phí (Dry-run preview)
        /// </summary>
        Task<BookingPreviewResponse> PreviewBookingAsync(Guid userId, BookingPreviewRequest request);

        /// <summary>
        /// Khởi tạo lịch hẹn tư vấn, tải lên tệp đính kèm và tạm khóa slot 10 phút (Hold Slot)
        /// </summary>
        Task<BookingDetailResponse> CreateBookingAsync(Guid userId, CreateBookingRequest request);

        /// <summary>
        /// Xem chi tiết một phiên tư vấn (kiểm tra quyền truy cập User / Expert / Admin)
        /// </summary>
        Task<BookingDetailResponse> GetBookingByIdAsync(Guid bookingId, Guid currentUserId, string currentUserRole);

        /// <summary>
        /// Lấy danh sách lịch hẹn của khách hàng (My Bookings)
        /// </summary>
        Task<PagedResult<BookingListItemResponse>> GetMyBookingsAsync(Guid userId, BookingStatus? status = null, int page = 1, int size = 10);

        /// <summary>
        /// Lấy danh sách các ca tư vấn đang chờ chuyên gia phê duyệt (Pending Approval)
        /// </summary>
        Task<PagedResult<BookingListItemResponse>> GetExpertPendingBookingsAsync(Guid expertUserId, int page = 1, int size = 10);

        /// <summary>
        /// Lấy toàn bộ lịch hẹn tư vấn của chuyên gia (Expert Bookings)
        /// </summary>
        Task<PagedResult<BookingListItemResponse>> GetExpertBookingsAsync(Guid expertUserId, BookingStatus? status = null, int page = 1, int size = 10);

        /// <summary>
        /// Chuyên gia đồng ý tiếp nhận ca tư vấn trong vòng 2 giờ
        /// </summary>
        Task<BookingDetailResponse> AcceptBookingAsync(Guid expertUserId, Guid bookingId);

        /// <summary>
        /// Chuyên gia từ chối tiếp nhận ca tư vấn trong vòng 2 giờ (kèm lý do)
        /// </summary>
        Task<BookingDetailResponse> RejectBookingAsync(Guid expertUserId, Guid bookingId, RejectBookingRequest request);

        /// <summary>
        /// Khách hàng chủ động hủy lịch hẹn
        /// </summary>
        Task<BookingDetailResponse> CancelBookingAsync(Guid userId, Guid bookingId, string reason);

        /// <summary>
        /// Lấy URL tải an toàn cho tệp chứng từ đính kèm (kiểm tra quyền sở hữu)
        /// </summary>
        Task<string> GetAttachmentDownloadUrlAsync(Guid bookingId, Guid attachmentId, Guid currentUserId, string currentUserRole);
    }
}
