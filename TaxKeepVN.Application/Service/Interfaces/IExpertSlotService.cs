using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Experts;

namespace TaxKeepVN.Application.Service.Interfaces
{
    /// <summary>
    /// Service quản lý khung giờ làm việc của Chuyên gia (CRUD Expert Slots)
    /// </summary>
    public interface IExpertSlotService
    {
        /// <summary>
        /// Lấy danh sách khung giờ của chuyên gia hiện tại kèm bộ lọc
        /// </summary>
        Task<List<ExpertSlotDetailResponse>> GetMySlotsAsync(Guid expertUserId, ExpertSlotFilterParameters filters);

        /// <summary>
        /// Xem chi tiết 1 khung giờ
        /// </summary>
        Task<ExpertSlotDetailResponse> GetSlotByIdAsync(Guid expertUserId, Guid slotId);

        /// <summary>
        /// Tạo mới 1 khung giờ rảnh
        /// </summary>
        Task<ExpertSlotDetailResponse> CreateSlotAsync(Guid expertUserId, CreateExpertSlotRequest request);


        /// <summary>
        /// Cập nhật thông tin khung giờ (chỉ cho phép khi chưa có người đặt và không bị giữ chỗ)
        /// </summary>
        Task<ExpertSlotDetailResponse> UpdateSlotAsync(Guid expertUserId, Guid slotId, UpdateExpertSlotRequest request);

        /// <summary>
        /// Bật / tắt nhanh trạng thái hoạt động của khung giờ
        /// </summary>
        Task<ExpertSlotDetailResponse> ToggleSlotActiveAsync(Guid expertUserId, Guid slotId);

        /// <summary>
        /// Xóa khung giờ (chỉ cho phép khi chưa có người đặt và không bị giữ chỗ)
        /// </summary>
        Task DeleteSlotAsync(Guid expertUserId, Guid slotId);

        /// <summary>
        /// Tạo hàng loạt khung giờ làm việc định kỳ theo tuần cho nhiều tuần liên tiếp
        /// </summary>
        Task<RecurringSlotBatchResultResponse> CreateRecurringSlotsAsync(Guid expertUserId, CreateRecurringSlotRequest request);


        Task<EmergencyCancelSlotResponse> EmergencyCancelSlotAsync(Guid expertUserId, Guid slotId, EmergencyCancelSlotRequest request,string? cancelledBy = null);
    }
}
