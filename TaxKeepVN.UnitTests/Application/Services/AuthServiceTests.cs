using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
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
        private readonly AuthService _authService;

        public AuthServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockUserRepo = new Mock<IGenericRepository<User>>();
            _mockJwtTokenService = new Mock<IJwtTokenService>();
            _mockPasswordHasher = new Mock<IPasswordHasher>();

            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(_mockUserRepo.Object);

            _authService = new AuthService(
                _mockUnitOfWork.Object,
                _mockJwtTokenService.Object,
                _mockPasswordHasher.Object
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
                IsVerified = true
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
            var suspendedUser = new User { CitizenId = request.CitizenId, Status = "suspended" };
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
            var existingUser = new User { CitizenId = request.CitizenId, Status = "active", PasswordHash = "Hash" };
            _mockUserRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                         .ReturnsAsync(new List<User> { existingUser });
            _mockPasswordHasher.Setup(p => p.Verify(request.Password, existingUser.PasswordHash)).Returns(false);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => _authService.LoginAsync(request));
            ex.ErrorCode.Should().Be("INVALID_CREDENTIALS");
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
    }
}
