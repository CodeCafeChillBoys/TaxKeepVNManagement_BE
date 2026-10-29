using System;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Requests.Profile;
using TaxKeepVN.Application.DTOs.Responses.Profile;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface IProfileService
    {
        /// <summary>Lấy thông tin cá nhân của người dùng đang đăng nhập</summary>
        Task<UserProfileResponse> GetProfileAsync(Guid userId);

        /// <summary>Cập nhật thông tin cá nhân (họ tên, sđt, địa chỉ, MST, ngày sinh)</summary>
        Task<UserProfileResponse> UpdateProfileAsync(Guid userId, UpdateProfileRequest request);
    }
}
