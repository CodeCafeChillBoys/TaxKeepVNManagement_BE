using System;
using System.Linq;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Requests.Auth;
using TaxKeepVN.Application.DTOs.Responses.Auth;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    public class AuthService : IAuthService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly IPasswordHasher _passwordHasher;

        public AuthService(IUnitOfWork unitOfWork, IJwtTokenService jwtTokenService, IPasswordHasher passwordHasher)
        {
            _unitOfWork = unitOfWork;
            _jwtTokenService = jwtTokenService;
            _passwordHasher = passwordHasher;
        }

        public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
        {
            var userRepo = _unitOfWork.Repository<User>();

            // Kiểm tra CCCD đã tồn tại chưa
            var existingByCitizenId = await userRepo.FindAsync(u => u.CitizenId == request.CitizenId);
            if (existingByCitizenId.Any())
            {
                throw new BadRequestException(
                    "CITIZEN_ID_ALREADY_EXISTS",
                    "Số CCCD này đã được đăng ký tài khoản. Vui lòng kiểm tra lại hoặc đăng nhập."
                );
            }

            // Kiểm tra Email đã tồn tại chưa
            var existingByEmail = await userRepo.FindAsync(u => u.Email == request.Email.ToLower());
            if (existingByEmail.Any())
            {
                throw new BadRequestException(
                    "EMAIL_ALREADY_EXISTS",
                    "Địa chỉ email này đã được sử dụng. Vui lòng dùng email khác."
                );
            }

            var newUser = new User
            {
                UserId = Guid.NewGuid(),
                CitizenId = request.CitizenId,
                FullName = request.FullName,
                Email = request.Email.ToLower(),
                PhoneNumber = request.PhoneNumber,
                PasswordHash = _passwordHasher.Hash(request.Password),
                DateOfBirth = request.DateOfBirth,
                Address = request.Address,
                UserRole = "taxpayer",
                IsVerified = false,
                Status = "active",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            await userRepo.AddAsync(newUser);
            await _unitOfWork.SaveChangesAsync();

            return new RegisterResponse
            {
                UserId = newUser.UserId,
                FullName = newUser.FullName,
                Email = newUser.Email,
                CitizenId = newUser.CitizenId,
                UserRole = newUser.UserRole,
                IsVerified = newUser.IsVerified
            };
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest request)
        {
            var userRepo = _unitOfWork.Repository<User>();

            // Tìm user theo số CCCD
            var users = await userRepo.FindAsync(u => u.CitizenId == request.CitizenId);
            var user = users.FirstOrDefault();

            if (user == null)
            {
                throw new UnauthorizedException(
                    "INVALID_CREDENTIALS",
                    "Số CCCD hoặc mật khẩu không chính xác."
                );
            }

            // Kiểm tra trạng thái tài khoản
            if (user.Status == "suspended")
            {
                throw new UnauthorizedException(
                    "ACCOUNT_SUSPENDED",
                    "Tài khoản của bạn đã bị tạm khóa. Vui lòng liên hệ hỗ trợ."
                );
            }

            // Xác minh mật khẩu
            if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
            {
                throw new UnauthorizedException(
                    "INVALID_CREDENTIALS",
                    "Số CCCD hoặc mật khẩu không chính xác."
                );
            }

            var token = _jwtTokenService.GenerateToken(user);

            return new AuthResponse
            {
                Token = token,
                UserId = user.UserId,
                FullName = user.FullName,
                Email = user.Email,
                CitizenId = user.CitizenId,
                UserRole = user.UserRole,
                IsVerified = user.IsVerified
            };
        }

        public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
        {
            var userRepo = _unitOfWork.Repository<User>();

            // Tìm user theo UserId (lấy từ JWT claim)
            var user = await userRepo.GetByIdAsync(userId);
            if (user == null)
            {
                throw new NotFoundException("Không tìm thấy tài khoản người dùng.");
            }

            // Xác minh mật khẩu hiện tại
            if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            {
                throw new BadRequestException(
                    "WRONG_CURRENT_PASSWORD",
                    "Mật khẩu hiện tại không chính xác."
                );
            }

            // Không cho phép đặt lại mật khẩu giống mật khẩu cũ
            if (_passwordHasher.Verify(request.NewPassword, user.PasswordHash))
            {
                throw new BadRequestException(
                    "SAME_PASSWORD",
                    "Mật khẩu mới không được trùng với mật khẩu hiện tại."
                );
            }

            // Hash và lưu mật khẩu mới
            user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
            user.UpdatedAt = DateTimeOffset.UtcNow;

            userRepo.Update(user);
            await _unitOfWork.SaveChangesAsync();
        }
    }
}
