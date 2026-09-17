using System;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Requests.Dependent;
using TaxKeepVN.Application.DTOs.Responses.Dependent;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface IDependentService
    {
        /// <summary>
        /// Đăng ký người phụ thuộc mới cho người nộp thuế đang đăng nhập.
        /// Tự động kiểm tra trùng lặp khoảng thời gian với các đăng ký khác.
        /// </summary>
        Task<DependentResponse> CreateDependentAsync(Guid taxpayerId, CreateDependentRequest request);

        /// <summary>
        /// Lấy danh sách người phụ thuộc của taxpayer đang đăng nhập với phân trang,
        /// hỗ trợ search theo tên/CCCD và filter theo status, relationship.
        /// </summary>
        Task<PagedResult<DependentListItemResponse>> GetDependentsAsync(Guid taxpayerId, DependentQueryParameters query);

        /// <summary>
        /// Lấy chi tiết một người phụ thuộc (bao gồm danh sách documents đã upload).
        /// Chỉ trả về nếu dependent thuộc về taxpayer đang đăng nhập.
        /// Ném ForbiddenException nếu không đúng chủ sở hữu, NotFoundException nếu không tồn tại.
        /// </summary>
        Task<DependentDetailResponse> GetDependentByIdAsync(Guid taxpayerId, Guid dependentId);

        /// <summary>
        /// Chuyển nhóm điều kiện của người phụ thuộc (ví dụ: CHILD_UNDER_18 → CHILD_OVER_18_STUDYING).
        /// Reset IsProfileComplete = false và Status = PENDING_DOCUMENTS để yêu cầu upload lại tài liệu.
        /// Tạo SystemNotification thông báo cho người dùng.
        /// Cho phép cập nhật Note cùng lúc.
        /// </summary>
        Task<UpdateDependentGroupResponse> UpdateGroupAsync(Guid taxpayerId, Guid dependentId, UpdateDependentGroupRequest request);
    }
}
