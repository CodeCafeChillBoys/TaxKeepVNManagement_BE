using System;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Requests.Profile;
using TaxKeepVN.Application.DTOs.Responses.Profile;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    public class ProfileService : IProfileService
    {
        private readonly IUnitOfWork _unitOfWork;

        public ProfileService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<UserProfileResponse> GetProfileAsync(Guid userId)
        {
            var user = await _unitOfWork.Repository<User>().GetByIdAsync(userId);

            if (user == null)
            {
                throw new NotFoundException("Không tìm thấy thông tin tài khoản người dùng.");
            }

            return MapToResponse(user);
        }

        public async Task<UserProfileResponse> UpdateProfileAsync(Guid userId, UpdateProfileRequest request)
        {
            var userRepo = _unitOfWork.Repository<User>();
            var user = await userRepo.GetByIdAsync(userId);

            if (user == null)
            {
                throw new NotFoundException("Không tìm thấy thông tin tài khoản người dùng.");
            }

            // ── Validation nghiệp vụ: CCCD làm MST (12 số) ─────────────────────
            if (!string.IsNullOrEmpty(request.TaxIdNumber) && request.TaxIdNumber.Length == 12)
            {
                if (request.IsTaxRegisteredConfirmed != true)
                {
                    throw new BadRequestException(
                        "TAX_REGISTRATION_CONFIRMATION_REQUIRED",
                        "Số Căn cước công dân chỉ được dùng làm Mã số thuế nếu bạn đã hoàn tất thủ tục đăng ký thuế lần đầu với Cơ quan Thuế. Vui lòng xác nhận trước khi lưu."
                    );
                }
            }

            // Cập nhật các trường được phép thay đổi
            user.FullName = request.FullName;
            user.PhoneNumber = request.PhoneNumber;
            user.DateOfBirth = request.DateOfBirth;
            user.Address = request.Address;
            user.TaxIdNumber = request.TaxIdNumber;
            user.UpdatedAt = DateTimeOffset.UtcNow;

            userRepo.Update(user);
            await _unitOfWork.SaveChangesAsync();

            return MapToResponse(user);
        }

        // ── Helper ─────────────────────────────────────────────────────────────
        private static UserProfileResponse MapToResponse(User user) => new()
        {
            UserId = user.UserId,
            CitizenId = user.CitizenId,
            TaxIdNumber = user.TaxIdNumber,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            DateOfBirth = user.DateOfBirth,
            Address = user.Address,
            UserRole = user.UserRole,
            IsVerified = user.IsVerified,
            Status = user.Status,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }
}
