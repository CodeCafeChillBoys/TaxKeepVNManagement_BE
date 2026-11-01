using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using TaxKeepVN.Application.DTOs.Requests.Profile;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Implementations;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;
using Xunit;

namespace TaxKeepVN.UnitTests.Application.Services
{
    public class ProfileServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IGenericRepository<User>> _mockUserRepo;
        private readonly ProfileService _service;

        public ProfileServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockUserRepo = new Mock<IGenericRepository<User>>();

            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(_mockUserRepo.Object);

            _service = new ProfileService(_mockUnitOfWork.Object);
        }

        #region GetProfileAsync

        [Fact]
        public async Task UTCID01_GetProfileAsync_UserNotFound_ThrowsNotFoundException()
        {
            var userId = Guid.NewGuid();
            _mockUserRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync((User?)null);

            await Assert.ThrowsAsync<NotFoundException>(() => _service.GetProfileAsync(userId));
        }

        [Fact]
        public async Task UTCID02_GetProfileAsync_UserFound_ReturnsProfileResponse()
        {
            var userId = Guid.NewGuid();
            var user = new User
            {
                UserId = userId,
                FullName = "Nguyễn Văn A",
                Email = "a@gmail.com",
                CitizenId = "079201001234",
                UserRole = "taxpayer",
                Status = "active"
            };
            _mockUserRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

            var result = await _service.GetProfileAsync(userId);

            result.Should().NotBeNull();
            result.FullName.Should().Be("Nguyễn Văn A");
            result.CitizenId.Should().Be("079201001234");
        }

        #endregion

        #region UpdateProfileAsync

        [Fact]
        public async Task UTCID03_UpdateProfileAsync_UserNotFound_ThrowsNotFoundException()
        {
            var userId = Guid.NewGuid();
            _mockUserRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync((User?)null);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _service.UpdateProfileAsync(userId, new UpdateProfileRequest()));
        }

        [Fact]
        public async Task UTCID04_UpdateProfileAsync_TaxId12DigitsUnconfirmed_ThrowsBadRequestException()
        {
            var userId = Guid.NewGuid();
            var user = new User { UserId = userId };
            _mockUserRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

            var request = new UpdateProfileRequest
            {
                TaxIdNumber = "079201001234", // 12 digits (CCCD)
                IsTaxRegisteredConfirmed = false // Chưa xác nhận thủ tục thuế
            };

            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.UpdateProfileAsync(userId, request));
            ex.ErrorCode.Should().Be("TAX_REGISTRATION_CONFIRMATION_REQUIRED");
        }

        [Fact]
        public async Task UTCID05_UpdateProfileAsync_Valid_UpdatesAndReturnsResponse()
        {
            var userId = Guid.NewGuid();
            var user = new User { UserId = userId, FullName = "Old Name" };
            _mockUserRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

            var request = new UpdateProfileRequest
            {
                FullName = "New Name",
                PhoneNumber = "0909999999",
                Address = "Hà Nội"
            };

            var result = await _service.UpdateProfileAsync(userId, request);

            result.Should().NotBeNull();
            result.FullName.Should().Be("New Name");
            user.FullName.Should().Be("New Name");
            _mockUserRepo.Verify(r => r.Update(user), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task UTCID06_UpdateProfileAsync_TaxId12DigitsConfirmed_SetsVerifiedTrue()
        {
            var userId = Guid.NewGuid();
            var user = new User { UserId = userId, IsVerified = false };
            _mockUserRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

            var request = new UpdateProfileRequest
            {
                TaxIdNumber = "079201001234",
                IsTaxRegisteredConfirmed = true
            };

            var result = await _service.UpdateProfileAsync(userId, request);

            result.IsVerified.Should().BeTrue();
            user.IsVerified.Should().BeTrue();
            user.TaxIdNumber.Should().Be("079201001234");
        }

        [Fact]
        public async Task UTCID07_UpdateProfileAsync_RequestTaxIdNullButUserHasTaxId_SetsVerifiedTrue()
        {
            var userId = Guid.NewGuid();
            var user = new User { UserId = userId, TaxIdNumber = "0312345678", IsVerified = false };
            _mockUserRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

            var request = new UpdateProfileRequest
            {
                FullName = "Updated Name"
            };

            var result = await _service.UpdateProfileAsync(userId, request);

            result.IsVerified.Should().BeTrue();
            user.IsVerified.Should().BeTrue();
        }

        #endregion
    }
}
