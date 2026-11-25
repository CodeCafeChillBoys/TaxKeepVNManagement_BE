using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using TaxKeepVN.Application.DTOs.Requests.Auth;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Implementations;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;
using Xunit;

namespace TaxKeepVN.UnitTests.Application.Services
{
    public class AuthServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IGenericRepository<User>> _mockUserRepo;
        private readonly Mock<IJwtTokenService> _mockJwtTokenService;
        private readonly Mock<IPasswordHasher> _mockPasswordHasher;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly AuthService _authService;

        public AuthServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockUserRepo = new Mock<IGenericRepository<User>>();
            _mockJwtTokenService = new Mock<IJwtTokenService>();
            _mockPasswordHasher = new Mock<IPasswordHasher>();
            _mockConfiguration = new Mock<IConfiguration>();
            _mockEmailService = new Mock<IEmailService>();

            // Cấu hình VerificationBaseUrl mặc định cho test
            _mockConfiguration.Setup(c => c["Email:VerificationBaseUrl"]).Returns("https://taxkeepvn.vercel.app");
            _mockConfiguration.Setup(c => c["Email:PasswordResetBaseUrl"]).Returns("https://taxkeepvn.vercel.app");

            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(_mockUserRepo.Object);

            var mockRoleRepo = new Mock<IGenericRepository<Role>>();
            mockRoleRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Role, bool>>>()))
                        .ReturnsAsync(new List<Role> { new Role { Id = 1, Code = "taxpayer", Name = "Người nộp thuế", IsActive = true } });
            _mockUnitOfWork.Setup(u => u.Repository<Role>()).Returns(mockRoleRepo.Object);

            _authService = new AuthService(
                _mockUnitOfWork.Object,
                _mockJwtTokenService.Object,
                _mockPasswordHasher.Object,
                _mockConfiguration.Object,
                null,
                _mockEmailService.Object
            );
        }

        #region FN_AUTH_01: LoginAsync

        [Fact]
        public async Task UTCID01_LoginAsync_ValidCredentials_ReturnsAuthResponse()
        {
            // Arrange
            var request = new LoginRequest { CitizenId = "012345678901", Password = "Password@123" };
            var existingUser = new User
            {
                UserId = Guid.NewGuid(),
                CitizenId = request.CitizenId,
                Status = "active",
                PasswordHash = "Hashed_Password@123",
                FullName = "Nguyễn Văn A",
                Email = "a@taxkeep.vn",
                UserRole = "taxpayer",
                IsVerified = false,       // Xác minh MST — không ảnh hưởng login
                IsEmailVerified = true    // Đã xác minh email
            };

            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User> { existingUser });
            _mockPasswordHasher.Setup(p => p.Verify(request.Password, existingUser.PasswordHash)).Returns(true);
            _mockJwtTokenService.Setup(j => j.GenerateToken(existingUser)).Returns("valid-jwt-token");

            // Act
            var result = await _authService.LoginAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.Token.Should().Be("valid-jwt-token");
            result.CitizenId.Should().Be(request.CitizenId);
        }

        [Fact]
        public async Task UTCID02_LoginAsync_CitizenIdNotFound_ThrowsUnauthorizedException()
        {
            // Arrange
            var request = new LoginRequest { CitizenId = "999999999999", Password = "Any" };
            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User>());

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => _authService.LoginAsync(request));
            ex.ErrorCode.Should().Be("INVALID_CREDENTIALS");
        }

        [Fact]
        public async Task UTCID03_LoginAsync_AccountSuspended_ThrowsUnauthorizedException()
        {
            // Arrange
            var request = new LoginRequest { CitizenId = "012345678901", Password = "Password@123" };
            var suspendedUser = new User { CitizenId = request.CitizenId, Status = "suspended", IsEmailVerified = true };
            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User> { suspendedUser });

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => _authService.LoginAsync(request));
            ex.ErrorCode.Should().Be("ACCOUNT_SUSPENDED");
        }

        [Fact]
        public async Task UTCID04_LoginAsync_WrongPassword_ThrowsUnauthorizedException()
        {
            // Arrange
            var request = new LoginRequest { CitizenId = "012345678901", Password = "Wrong" };
            var existingUser = new User { CitizenId = request.CitizenId, Status = "active", PasswordHash = "Hash", IsEmailVerified = true };
            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User> { existingUser });
            _mockPasswordHasher.Setup(p => p.Verify(request.Password, existingUser.PasswordHash)).Returns(false);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => _authService.LoginAsync(request));
            ex.ErrorCode.Should().Be("INVALID_CREDENTIALS");
        }

        [Fact]
        public async Task UTCID04b_LoginAsync_EmailNotVerified_ThrowsUnauthorizedException()
        {
            // Arrange
            var request = new LoginRequest { CitizenId = "012345678901", Password = "Password@123" };
            var unverifiedUser = new User
            {
                CitizenId = request.CitizenId,
                Status = "active",
                PasswordHash = "Hash",
                IsEmailVerified = false  // Chưa xác minh email
            };
            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User> { unverifiedUser });

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => _authService.LoginAsync(request));
            ex.ErrorCode.Should().Be("EMAIL_NOT_VERIFIED");
        }

        #endregion

        #region FN_AUTH_02: RegisterAsync

        [Fact]
        public async Task UTCID05_RegisterAsync_ValidRequest_CreatesUserAndWallet()
        {
            // Arrange
            var request = new RegisterRequest
            {
                CitizenId = "079201001234",
                FullName = "Nguyễn Văn Test",
                Email = "test@taxkeep.vn",
                PhoneNumber = "0901234567",
                Password = "Password@123"
            };

            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User>());
            _mockPasswordHasher.Setup(p => p.Hash(request.Password)).Returns("Hashed_Password");

            var mockWalletRepo = new Mock<IGenericRepository<UserWallet>>();
            _mockUnitOfWork.Setup(u => u.Repository<UserWallet>()).Returns(mockWalletRepo.Object);

            // Act
            var result = await _authService.RegisterAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.CitizenId.Should().Be(request.CitizenId);
            result.Email.Should().Be(request.Email.ToLower());
            result.UserRole.Should().Be("taxpayer");
            result.RoleId.Should().Be(1);
            _mockUserRepo.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Once);
            mockWalletRepo.Verify(w => w.AddAsync(It.IsAny<UserWallet>()), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task UTCID06_RegisterAsync_DuplicateCitizenId_ThrowsBadRequestException()
        {
            // Arrange
            var request = new RegisterRequest { CitizenId = "079201001234", Email = "test@taxkeep.vn" };
            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User> { new User { CitizenId = request.CitizenId } });

            // Act & Assert
            var ex = await Assert.ThrowsAsync<BadRequestException>(() => _authService.RegisterAsync(request));
            ex.ErrorCode.Should().Be("CITIZEN_ID_ALREADY_EXISTS");
        }

        [Fact]
        public async Task UTCID07_RegisterAsync_DuplicateEmail_ThrowsBadRequestException()
        {
            // Arrange
            var request = new RegisterRequest { CitizenId = "079201009999", Email = "exist@taxkeep.vn" };
            _mockUserRepo.SetupSequence(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User>())
                         .ReturnsAsync(new List<User> { new User { Email = request.Email } });

            // Act & Assert
            var ex = await Assert.ThrowsAsync<BadRequestException>(() => _authService.RegisterAsync(request));
            ex.ErrorCode.Should().Be("EMAIL_ALREADY_EXISTS");
        }

        #endregion

        #region FN_AUTH_03: ChangePasswordAsync

        [Fact]
        public async Task UTCID08_ChangePasswordAsync_Valid_Success()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User { UserId = userId, PasswordHash = "Old" };
            _mockUserRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);
            _mockPasswordHasher.Setup(p => p.Verify("OldPass", "Old")).Returns(true);
            _mockPasswordHasher.Setup(p => p.Verify("NewPass", "Old")).Returns(false);
            _mockPasswordHasher.Setup(p => p.Hash("NewPass")).Returns("NewHash");

            // Act
            await _authService.ChangePasswordAsync(userId, new ChangePasswordRequest { CurrentPassword = "OldPass", NewPassword = "NewPass" });

            // Assert
            user.PasswordHash.Should().Be("NewHash");
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }
        
        [Fact]
        public async Task UTCID09_ChangePasswordAsync_UserNotFound_ThrowsNotFoundException()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _mockUserRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync((User?)null);

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() =>
                _authService.ChangePasswordAsync(userId, new ChangePasswordRequest()));
        }

        [Fact]
        public async Task UTCID10_ChangePasswordAsync_WrongCurrentPassword_ThrowsBadRequestException()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User { UserId = userId, PasswordHash = "Old" };
            _mockUserRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);
            _mockPasswordHasher.Setup(p => p.Verify(It.IsAny<string>(), "Old")).Returns(false);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _authService.ChangePasswordAsync(userId, new ChangePasswordRequest { CurrentPassword = "Wrong" }));
            ex.ErrorCode.Should().Be("WRONG_CURRENT_PASSWORD");
        }

        [Fact]
        public async Task UTCID11_ChangePasswordAsync_SamePassword_ThrowsBadRequestException()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User { UserId = userId, PasswordHash = "Old" };
            _mockUserRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);
            _mockPasswordHasher.Setup(p => p.Verify(It.IsAny<string>(), "Old")).Returns(true);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _authService.ChangePasswordAsync(userId, new ChangePasswordRequest { CurrentPassword = "Pass", NewPassword = "Pass" }));
            ex.ErrorCode.Should().Be("SAME_PASSWORD");
        }

        #endregion

        #region FN_AUTH_04: VerifyEmailAsync

        [Fact]
        public async Task UTCID12_VerifyEmailAsync_ValidToken_Success()
        {
            // Arrange
            var token = "valid-token-123";
            var user = new User
            {
                UserId = Guid.NewGuid(),
                Email = "test@taxkeep.vn",
                EmailVerificationToken = token,
                EmailVerificationTokenExpiry = DateTimeOffset.UtcNow.AddHours(2),
                IsEmailVerified = false
            };

            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User> { user });

            // Act
            await _authService.VerifyEmailAsync(token);

            // Assert
            user.IsEmailVerified.Should().BeTrue();
            user.EmailVerificationToken.Should().BeNull();
            user.EmailVerificationTokenExpiry.Should().BeNull();
            _mockUserRepo.Verify(r => r.Update(user), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task UTCID13_VerifyEmailAsync_InvalidToken_ThrowsBadRequestException()
        {
            // Arrange
            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User>());

            // Act & Assert
            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _authService.VerifyEmailAsync("non-existent-token"));
            ex.ErrorCode.Should().Be("INVALID_TOKEN");
        }

        [Fact]
        public async Task UTCID14_VerifyEmailAsync_ExpiredToken_ThrowsBadRequestException()
        {
            // Arrange
            var token = "expired-token-123";
            var user = new User
            {
                UserId = Guid.NewGuid(),
                Email = "test@taxkeep.vn",
                EmailVerificationToken = token,
                EmailVerificationTokenExpiry = DateTimeOffset.UtcNow.AddMinutes(-10),
                IsEmailVerified = false
            };

            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User> { user });

            // Act & Assert
            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _authService.VerifyEmailAsync(token));
            ex.ErrorCode.Should().Be("TOKEN_EXPIRED");
        }

        #endregion

        #region FN_AUTH_05: ForgotPassword & ResetPassword

        [Fact]
        public async Task UTCID15_ForgotPasswordAsync_UserExists_GeneratesResetToken()
        {
            // Arrange
            var user = new User
            {
                UserId = Guid.NewGuid(),
                FullName = "Nguyễn Văn Test",
                Email = "test@taxkeep.vn",
                Status = "active"
            };

            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User> { user });

            // Act
            await _authService.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "test@taxkeep.vn" });

            // Assert
            user.PasswordResetToken.Should().NotBeNullOrWhiteSpace();
            user.PasswordResetTokenExpiry.Should().BeAfter(DateTimeOffset.UtcNow);
            _mockUserRepo.Verify(r => r.Update(user), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task UTCID16_ForgotPasswordAsync_UserNotFound_DoesNotThrow()
        {
            // Arrange
            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User>());

            // Act
            var act = () => _authService.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "notfound@taxkeep.vn" });

            // Assert (không throw để bảo mật tránh Account Enumeration)
            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task UTCID17_ValidateResetTokenAsync_ValidToken_ReturnsTrue()
        {
            // Arrange
            var token = "valid-reset-token";
            var user = new User
            {
                PasswordResetToken = token,
                PasswordResetTokenExpiry = DateTimeOffset.UtcNow.AddMinutes(10)
            };

            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User> { user });

            // Act
            var result = await _authService.ValidateResetTokenAsync(token);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task UTCID18_ValidateResetTokenAsync_ExpiredToken_ReturnsFalse()
        {
            // Arrange
            var token = "expired-reset-token";
            var user = new User
            {
                PasswordResetToken = token,
                PasswordResetTokenExpiry = DateTimeOffset.UtcNow.AddMinutes(-5)
            };

            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User> { user });

            // Act
            var result = await _authService.ValidateResetTokenAsync(token);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task UTCID19_ResetPasswordAsync_ValidRequest_Success()
        {
            // Arrange
            var token = "valid-reset-token";
            var user = new User
            {
                UserId = Guid.NewGuid(),
                PasswordHash = "OldHash",
                PasswordResetToken = token,
                PasswordResetTokenExpiry = DateTimeOffset.UtcNow.AddMinutes(10)
            };

            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User> { user });
            _mockPasswordHasher.Setup(p => p.Verify("NewPassword@123", "OldHash")).Returns(false);
            _mockPasswordHasher.Setup(p => p.Hash("NewPassword@123")).Returns("NewHashedPassword");

            var request = new ResetPasswordRequest
            {
                Token = token,
                NewPassword = "NewPassword@123",
                ConfirmNewPassword = "NewPassword@123"
            };

            // Act
            await _authService.ResetPasswordAsync(request);

            // Assert
            user.PasswordHash.Should().Be("NewHashedPassword");
            user.PasswordResetToken.Should().BeNull();
            user.PasswordResetTokenExpiry.Should().BeNull();
            user.IsEmailVerified.Should().BeTrue();
            _mockUserRepo.Verify(r => r.Update(user), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task UTCID20_ResetPasswordAsync_SamePassword_ThrowsBadRequestException()
        {
            // Arrange
            var token = "valid-reset-token";
            var user = new User
            {
                UserId = Guid.NewGuid(),
                PasswordHash = "CurrentHash",
                PasswordResetToken = token,
                PasswordResetTokenExpiry = DateTimeOffset.UtcNow.AddMinutes(10)
            };

            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User> { user });
            _mockPasswordHasher.Setup(p => p.Verify("CurrentPassword@123", "CurrentHash")).Returns(true);

            var request = new ResetPasswordRequest
            {
                Token = token,
                NewPassword = "CurrentPassword@123",
                ConfirmNewPassword = "CurrentPassword@123"
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _authService.ResetPasswordAsync(request));
            ex.ErrorCode.Should().Be("SAME_PASSWORD");
        }

        [Fact]
        public async Task UTCID21_ResetPasswordAsync_InvalidToken_ThrowsBadRequestException()
        {
            // Arrange
            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User>());

            var request = new ResetPasswordRequest
            {
                Token = "invalid-token",
                NewPassword = "NewPassword@123",
                ConfirmNewPassword = "NewPassword@123"
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _authService.ResetPasswordAsync(request));
            ex.ErrorCode.Should().Be("INVALID_TOKEN");
        }

        [Fact]
        public async Task UTCID22_ResetPasswordAsync_ExpiredToken_ThrowsBadRequestException()
        {
            // Arrange
            var token = "expired-token";
            var user = new User
            {
                UserId = Guid.NewGuid(),
                PasswordHash = "OldHash",
                PasswordResetToken = token,
                PasswordResetTokenExpiry = DateTimeOffset.UtcNow.AddMinutes(-1)
            };

            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User> { user });

            var request = new ResetPasswordRequest
            {
                Token = token,
                NewPassword = "NewPassword@123",
                ConfirmNewPassword = "NewPassword@123"
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _authService.ResetPasswordAsync(request));
            ex.ErrorCode.Should().Be("TOKEN_EXPIRED");
        }

        #endregion
    }
}
