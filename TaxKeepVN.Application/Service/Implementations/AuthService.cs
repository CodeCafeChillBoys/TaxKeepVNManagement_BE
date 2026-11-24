using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
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
        private readonly IUserSubscriptionService? _userSubscriptionService;
        private readonly IEmailService? _emailService;
        private readonly IConfiguration _configuration;

        public AuthService(
            IUnitOfWork unitOfWork,
            IJwtTokenService jwtTokenService,
            IPasswordHasher passwordHasher,
            IConfiguration configuration,
            IUserSubscriptionService? userSubscriptionService = null,
            IEmailService? emailService = null)
        {
            _unitOfWork = unitOfWork;
            _jwtTokenService = jwtTokenService;
            _passwordHasher = passwordHasher;
            _configuration = configuration;
            _userSubscriptionService = userSubscriptionService;
            _emailService = emailService;
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

            // Tạo token xác minh email (GUID ngẫu nhiên, hết hạn sau 24 giờ)
            var verificationToken = Guid.NewGuid().ToString("N");
            var tokenExpiry = DateTimeOffset.UtcNow.AddHours(24);

            // Đọc vai trò mặc định (Người nộp thuế - taxpayer) từ bảng roles trong DB, không hardcode
            var roleRepo = _unitOfWork.Repository<Role>();
            var taxpayerRoles = await roleRepo.FindAsync(r => r.Code.ToLower() == "taxpayer" && r.IsActive);
            var defaultRole = taxpayerRoles.FirstOrDefault();

            var assignedRoleId = defaultRole?.Id ?? 1;
            var assignedRoleCode = defaultRole?.Code ?? "taxpayer";

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
                RoleId = assignedRoleId,
                UserRole = assignedRoleCode,
                IsVerified = false,       // Xác minh MST — sẽ được set bởi ProfileService
                IsEmailVerified = false,  // Xác minh email — sẽ được set sau khi click link
                Status = "active",
                EmailVerificationToken = verificationToken,
                EmailVerificationTokenExpiry = tokenExpiry,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            await userRepo.AddAsync(newUser);

            // Tự động khởi tạo ví cá nhân (WalletType = "USER", số dư 0 VNĐ) ngay khi đăng ký tài khoản mới
            var initialWallet = new UserWallet
            {
                Id = Guid.NewGuid(),
                UserId = newUser.UserId,
                WalletType = "USER",
                AvailableBalance = 0m,
                FrozenBalance = 0m,
                TotalEarned = 0m,
                TotalWithdrawn = 0m,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            await _unitOfWork.Repository<UserWallet>().AddAsync(initialWallet);

            await _unitOfWork.SaveChangesAsync();

            if (_userSubscriptionService != null)
            {
                await _userSubscriptionService.AutoAssignBasicPlanIfNoneAsync(newUser.UserId);
            }

            // Gửi email xác minh (nếu EmailService được đăng ký)
            if (_emailService != null)
            {
                var verificationLink = BuildVerificationLink(verificationToken);
                // Gửi bất đồng bộ, không block luồng đăng ký (lỗi gửi mail không làm thất bại đăng ký)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _emailService.SendEmailVerificationAsync(newUser.Email, newUser.FullName, verificationLink);
                    }
                    catch
                    {
                        // Ghi log nếu cần; không throw để tránh ảnh hưởng luồng đăng ký
                    }
                });
            }

            return new RegisterResponse
            {
                UserId = newUser.UserId,
                FullName = newUser.FullName,
                Email = newUser.Email,
                CitizenId = newUser.CitizenId,
                RoleId = newUser.RoleId,
                UserRole = newUser.UserRole,
                IsVerified = newUser.IsVerified,
                RequiresEmailVerification = _emailService != null
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

            // Chặn đăng nhập nếu email chưa được xác minh
            if (!user.IsEmailVerified)
            {
                throw new UnauthorizedException(
                    "EMAIL_NOT_VERIFIED",
                    "Tài khoản chưa được xác minh email. Vui lòng kiểm tra hộp thư và nhấn vào liên kết xác minh."
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
                RoleId = user.RoleId,
                UserRole = user.UserRole,
                IsVerified = user.IsVerified
            };
        }

        public async Task VerifyEmailAsync(string token)
        {
            token = SanitizeToken(token);

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new BadRequestException("INVALID_TOKEN", "Token xác minh không hợp lệ.");
            }

            var userRepo = _unitOfWork.Repository<User>();
            var users = await userRepo.FindAsync(u => u.EmailVerificationToken == token);
            var user = users.FirstOrDefault();

            if (user == null)
            {
                throw new BadRequestException(
                    "INVALID_TOKEN",
                    "Liên kết xác minh không hợp lệ hoặc đã được sử dụng."
                );
            }

            if (user.IsEmailVerified)
            {
                // Email đã được xác minh — trả về thành công để idempotent
                return;
            }

            if (user.EmailVerificationTokenExpiry.HasValue
                && user.EmailVerificationTokenExpiry.Value < DateTimeOffset.UtcNow)
            {
                throw new BadRequestException(
                    "TOKEN_EXPIRED",
                    "Liên kết xác minh đã hết hạn. Vui lòng yêu cầu gửi lại email xác minh."
                );
            }

            user.IsEmailVerified = true;
            user.EmailVerificationToken = null;
            user.EmailVerificationTokenExpiry = null;
            user.UpdatedAt = DateTimeOffset.UtcNow;

            userRepo.Update(user);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task ResendVerificationEmailAsync(string email)
        {
            if (_emailService == null)
            {
                throw new BadRequestException(
                    "EMAIL_SERVICE_UNAVAILABLE",
                    "Dịch vụ email chưa được cấu hình."
                );
            }

            var userRepo = _unitOfWork.Repository<User>();
            var users = await userRepo.FindAsync(u => u.Email == email.ToLower());
            var user = users.FirstOrDefault();

            // Trả lỗi mơ hồ để không lộ thông tin tài khoản
            if (user == null || user.IsEmailVerified)
            {
                throw new BadRequestException(
                    "RESEND_NOT_APPLICABLE",
                    "Nếu email này tồn tại và chưa được xác minh, chúng tôi đã gửi lại email xác minh."
                );
            }

            // Tạo token mới và gia hạn thêm 24 giờ
            var newToken = Guid.NewGuid().ToString("N");
            user.EmailVerificationToken = newToken;
            user.EmailVerificationTokenExpiry = DateTimeOffset.UtcNow.AddHours(24);
            user.UpdatedAt = DateTimeOffset.UtcNow;

            userRepo.Update(user);
            await _unitOfWork.SaveChangesAsync();

            var verificationLink = BuildVerificationLink(newToken);
            await _emailService.SendEmailVerificationAsync(user.Email, user.FullName, verificationLink);
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

        public async Task ForgotPasswordAsync(ForgotPasswordRequest request)
        {
            if (_emailService == null)
            {
                throw new BadRequestException(
                    "EMAIL_SERVICE_UNAVAILABLE",
                    "Dịch vụ email chưa được cấu hình."
                );
            }

            var userRepo = _unitOfWork.Repository<User>();
            var normalizedEmail = request.Email.Trim().ToLower();
            var users = await userRepo.FindAsync(u => u.Email == normalizedEmail);
            var user = users.FirstOrDefault();

            // Nếu user không tồn tại hoặc tài khoản bị khóa, kết thúc âm thầm để bảo mật thông tin (tránh Account Enumeration)
            if (user == null || user.Status == "suspended")
            {
                return;
            }

            // Sinh reset token ngẫu nhiên (GUID 32 ký tự hex) và hạn dùng 15 phút
            var resetToken = Guid.NewGuid().ToString("N");
            user.PasswordResetToken = resetToken;
            user.PasswordResetTokenExpiry = DateTimeOffset.UtcNow.AddMinutes(15);
            user.UpdatedAt = DateTimeOffset.UtcNow;

            userRepo.Update(user);
            await _unitOfWork.SaveChangesAsync();

            var resetLink = BuildPasswordResetLink(resetToken);

            // Gửi email đặt lại mật khẩu ở chế độ background task
            _ = Task.Run(async () =>
            {
                try
                {
                    await _emailService.SendPasswordResetAsync(user.Email, user.FullName, resetLink);
                }
                catch
                {
                    // Tránh gián đoạn luồng chính nếu lỗi mạng gửi email
                }
            });
        }

        public async Task<bool> ValidateResetTokenAsync(string token)
        {
            token = SanitizeToken(token);

            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            var userRepo = _unitOfWork.Repository<User>();
            var users = await userRepo.FindAsync(u => u.PasswordResetToken == token);
            var user = users.FirstOrDefault();

            if (user == null)
            {
                return false;
            }

            if (!user.PasswordResetTokenExpiry.HasValue || user.PasswordResetTokenExpiry.Value < DateTimeOffset.UtcNow)
            {
                return false;
            }

            return true;
        }

        public async Task ResetPasswordAsync(ResetPasswordRequest request)
        {
            var token = SanitizeToken(request.Token);

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new BadRequestException("INVALID_TOKEN", "Token đặt lại mật khẩu không hợp lệ.");
            }

            var userRepo = _unitOfWork.Repository<User>();
            var users = await userRepo.FindAsync(u => u.PasswordResetToken == token);
            var user = users.FirstOrDefault();

            if (user == null)
            {
                throw new BadRequestException(
                    "INVALID_TOKEN",
                    "Liên kết đặt lại mật khẩu không hợp lệ hoặc đã được sử dụng."
                );
            }

            if (!user.PasswordResetTokenExpiry.HasValue || user.PasswordResetTokenExpiry.Value < DateTimeOffset.UtcNow)
            {
                throw new BadRequestException(
                    "TOKEN_EXPIRED",
                    "Liên kết đặt lại mật khẩu đã hết hạn. Vui lòng yêu cầu liên kết mới."
                );
            }

            // Không cho phép đặt lại mật khẩu giống mật khẩu hiện tại
            if (_passwordHasher.Verify(request.NewPassword, user.PasswordHash))
            {
                throw new BadRequestException(
                    "SAME_PASSWORD",
                    "Mật khẩu mới không được trùng với mật khẩu hiện tại."
                );
            }

            // Hash và cập nhật mật khẩu mới, đồng thời hủy bỏ token
            user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiry = null;
            user.IsEmailVerified = true; // Người dùng đã xác nhận sở hữu hòm mail
            user.UpdatedAt = DateTimeOffset.UtcNow;

            userRepo.Update(user);
            await _unitOfWork.SaveChangesAsync();
        }

        // ── Helper ──────────────────────────────────────────────────────────
        private string BuildVerificationLink(string token)
        {
            var baseUrl = _configuration["Email:VerificationBaseUrl"]
                          ?? _configuration["AppSettings:BaseUrl"]
                          ?? "https://taxkeepvn.vercel.app";
            return $"{baseUrl.TrimEnd('/')}/verify-email?token={token}";
        }

        private string BuildPasswordResetLink(string token)
        {
            var baseUrl = _configuration["Email:PasswordResetBaseUrl"]
                          ?? _configuration["Email:VerificationBaseUrl"]
                          ?? _configuration["AppSettings:BaseUrl"]
                          ?? "https://taxkeepvn.vercel.app";
            return $"{baseUrl.TrimEnd('/')}/reset-password?token={token}";
        }

        /// <summary>
        /// Chuẩn hóa token: Nếu người dùng hoặc Swagger dán nhầm cả URL (vd: https://.../verify-email?token=abc123xyz),
        /// hàm sẽ tự bóc tách chính xác chuỗi token ra.
        /// </summary>
        private static string SanitizeToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return string.Empty;

            try
            {
                token = Uri.UnescapeDataString(token);
            }
            catch
            {
                // Bỏ qua nếu token không giải mã được
            }

            token = token.Trim();

            var tokenIndex = token.LastIndexOf("token=", StringComparison.OrdinalIgnoreCase);
            if (tokenIndex >= 0)
            {
                token = token.Substring(tokenIndex + 6);
                var ampIndex = token.IndexOf('&');
                if (ampIndex >= 0)
                {
                    token = token.Substring(0, ampIndex);
                }
            }

            return token.Trim();
        }
    }
}
