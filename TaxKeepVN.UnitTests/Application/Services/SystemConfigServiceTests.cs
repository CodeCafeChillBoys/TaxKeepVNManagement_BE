using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.SystemConfigs;
using TaxKeepVN.Application.Service.Implementations;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;
using Xunit;

namespace TaxKeepVN.UnitTests.Application.Services
{
    public class SystemConfigServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IGenericRepository<SystemConfig>> _mockRepo;
        private readonly SystemConfigService _service;

        public SystemConfigServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockRepo = new Mock<IGenericRepository<SystemConfig>>();

            _mockUnitOfWork.Setup(u => u.Repository<SystemConfig>()).Returns(_mockRepo.Object);

            _service = new SystemConfigService(_mockUnitOfWork.Object);
        }

        #region GetAllAsync

        [Fact]
        public async Task UTCID01_GetAllAsync_WithoutYear_ReturnsPaged()
        {
            var data = new List<SystemConfig>
            {
                new SystemConfig { ConfigId = Guid.NewGuid(), ConfigKey = "KEY_1", IsActive = true },
                new SystemConfig { ConfigId = Guid.NewGuid(), ConfigKey = "KEY_2", IsActive = true }
            };

            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(data);

            var query = new SystemConfigQueryParameters { Page = 1, Size = 10 };
            var result = await _service.GetAllAsync(query);

            result.Should().NotBeNull();
            result.Items.Should().HaveCount(2);
        }

        [Fact]
        public async Task UTCID02_GetAllAsync_WithTaxYear_ResolvesEffectiveConfig()
        {
            var data = new List<SystemConfig>
            {
                new SystemConfig { ConfigId = Guid.NewGuid(), ConfigKey = "PIT_DEDUCTION", ConfigValue = "11000000", AppliesFromYear = 2024, IsActive = true },
                new SystemConfig { ConfigId = Guid.NewGuid(), ConfigKey = "PIT_DEDUCTION", ConfigValue = "15000000", AppliesFromYear = 2026, IsActive = true }
            };

            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(data);

            var query = new SystemConfigQueryParameters { TaxYear = 2025, Page = 1, Size = 10 };
            var result = await _service.GetAllAsync(query);

            result.Items.Should().HaveCount(1);
            result.Items.First().ConfigValue.Should().Be("11000000");
        }

        #endregion

        #region GetRequiredConfigValueAsync

        [Fact]
        public async Task UTCID03_GetRequiredConfigValueAsync_NotFound_ThrowsInvalidOperationException()
        {
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SystemConfig, bool>>>()))
                     .ReturnsAsync(new List<SystemConfig>());

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.GetRequiredConfigValueAsync("UNKNOWN_KEY"));
        }

        [Fact]
        public async Task UTCID04_GetRequiredConfigValueAsync_WithTaxYear_ResolvesBestMatch()
        {
            var configs = new List<SystemConfig>
            {
                new SystemConfig { ConfigKey = "TAX_RATE", ConfigValue = "10", AppliesFromYear = 2024, IsActive = true },
                new SystemConfig { ConfigKey = "TAX_RATE", ConfigValue = "15", AppliesFromYear = 2026, IsActive = true }
            };

            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SystemConfig, bool>>>()))
                     .ReturnsAsync(configs);

            var value = await _service.GetRequiredConfigValueAsync("TAX_RATE", 2025);

            value.Should().Be("10");
        }

        #endregion

        #region CreateAsync

        [Fact]
        public async Task UTCID05_CreateAsync_DuplicateKeyAndYear_ThrowsInvalidOperationException()
        {
            var existing = new SystemConfig { ConfigKey = "RATE", AppliesFromYear = 2025 };
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SystemConfig, bool>>>()))
                     .ReturnsAsync(new List<SystemConfig> { existing });

            var dto = new SystemConfigCreateDto { ConfigKey = "RATE", ConfigValue = "20", AppliesFromYear = 2025 };

            await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAsync(dto));
        }

        [Fact]
        public async Task UTCID06_CreateAsync_Valid_CreatesSuccessfully()
        {
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SystemConfig, bool>>>()))
                     .ReturnsAsync(new List<SystemConfig>());

            var dto = new SystemConfigCreateDto
            {
                ConfigKey = "NEW_KEY",
                ConfigValue = "NEW_VAL",
                AppliesFromYear = 2025,
                IsActive = true
            };

            var result = await _service.CreateAsync(dto);

            result.Should().NotBeNull();
            result.ConfigKey.Should().Be("NEW_KEY");
            _mockRepo.Verify(r => r.AddAsync(It.IsAny<SystemConfig>()), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion

        #region DeleteByIdAsync

        [Fact]
        public async Task UTCID07_DeleteByIdAsync_NotFound_ReturnsFalse()
        {
            _mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((SystemConfig?)null);

            var result = await _service.DeleteByIdAsync(Guid.NewGuid());

            result.Should().BeFalse();
        }

        [Fact]
        public async Task UTCID08_DeleteByIdAsync_Found_DeletesAndReturnsTrue()
        {
            var config = new SystemConfig { ConfigId = Guid.NewGuid() };
            _mockRepo.Setup(r => r.GetByIdAsync(config.ConfigId)).ReturnsAsync(config);

            var result = await _service.DeleteByIdAsync(config.ConfigId);

            result.Should().BeTrue();
            _mockRepo.Verify(r => r.Remove(config), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion

        #region GetByIdAsync

        [Fact]
        public async Task UTCID09_GetByIdAsync_Found_ReturnsDto()
        {
            var id = Guid.NewGuid();
            var config = new SystemConfig { ConfigId = id, ConfigKey = "KEY", ConfigValue = "VAL" };
            _mockRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(config);

            var result = await _service.GetByIdAsync(id);

            result.Should().NotBeNull();
            result!.ConfigKey.Should().Be("KEY");
        }

        #endregion

        #region UpdateByIdAsync

        [Fact]
        public async Task UTCID10_UpdateByIdAsync_NotFound_ThrowsKeyNotFoundException()
        {
            _mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((SystemConfig?)null);

            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                _service.UpdateByIdAsync(Guid.NewGuid(), new SystemConfigUpdateDto { ConfigValue = "VAL" }));
        }

        [Fact]
        public async Task UTCID11_UpdateByIdAsync_Valid_UpdatesSuccessfully()
        {
            var id = Guid.NewGuid();
            var config = new SystemConfig { ConfigId = id, ConfigKey = "KEY", ConfigValue = "OLD", AppliesFromYear = 2024 };
            _mockRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(config);

            var dto = new SystemConfigUpdateDto
            {
                ConfigValue = "NEW",
                AppliesFromYear = 2024,
                Description = "Updated description"
            };

            var result = await _service.UpdateByIdAsync(id, dto);

            result.Should().NotBeNull();
            result.ConfigValue.Should().Be("NEW");
            _mockRepo.Verify(r => r.Update(config), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion

        #region UpdateAsync

        [Fact]
        public async Task UTCID12_UpdateAsync_ExistingKey_UpdatesConfig()
        {
            var config = new SystemConfig { ConfigId = Guid.NewGuid(), ConfigKey = "KEY", ConfigValue = "OLD" };
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SystemConfig, bool>>>()))
                     .ReturnsAsync(new List<SystemConfig> { config });

            var dto = new SystemConfigUpdateDto { ConfigValue = "UPDATED", IsActive = true };

            var result = await _service.UpdateAsync("KEY", dto);

            result.Should().NotBeNull();
            result.ConfigValue.Should().Be("UPDATED");
            _mockRepo.Verify(r => r.Update(config), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task UTCID13_UpdateByIdAsync_DuplicateYear_ThrowsInvalidOperationException()
        {
            var id = Guid.NewGuid();
            var config = new SystemConfig { ConfigId = id, ConfigKey = "KEY", ConfigValue = "OLD", AppliesFromYear = 2024 };
            _mockRepo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(config);

            var duplicate = new SystemConfig { ConfigId = Guid.NewGuid(), ConfigKey = "KEY", AppliesFromYear = 2025 };
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SystemConfig, bool>>>()))
                     .ReturnsAsync(new List<SystemConfig> { duplicate });

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.UpdateByIdAsync(id, new SystemConfigUpdateDto { ConfigValue = "NEW", AppliesFromYear = 2025 }));
            ex.Message.Should().Contain("đã tồn tại");
        }

        [Fact]
        public async Task UTCID14_DeleteByIdAsync_NotFoundAndSuccess()
        {
            _mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((SystemConfig?)null);
            var resFalse = await _service.DeleteByIdAsync(Guid.NewGuid());
            resFalse.Should().BeFalse();

            var config = new SystemConfig { ConfigId = Guid.NewGuid() };
            _mockRepo.Setup(r => r.GetByIdAsync(config.ConfigId)).ReturnsAsync(config);
            var resTrue = await _service.DeleteByIdAsync(config.ConfigId);
            resTrue.Should().BeTrue();
            _mockRepo.Verify(r => r.Remove(config), Times.Once);
        }

        [Fact]
        public async Task UTCID15_UpdateAsync_NewKey_CreatesConfig()
        {
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SystemConfig, bool>>>()))
                     .ReturnsAsync(new List<SystemConfig>());

            var dto = new SystemConfigUpdateDto { ConfigValue = "BRAND_NEW", AppliesFromYear = 2026, Description = "New desc" };
            var result = await _service.UpdateAsync("NEW_KEY", dto);

            result.Should().NotBeNull();
            result.ConfigKey.Should().Be("NEW_KEY");
            result.ConfigValue.Should().Be("BRAND_NEW");
            _mockRepo.Verify(r => r.AddAsync(It.IsAny<SystemConfig>()), Times.Once);
        }

        [Fact]
        public async Task UTCID16_GetByKeyAsync_ReturnsConfig()
        {
            var config = new SystemConfig { ConfigKey = "MY_KEY", ConfigValue = "MY_VAL", IsActive = true };
            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SystemConfig, bool>>>()))
                     .ReturnsAsync(new List<SystemConfig> { config });

            var result = await _service.GetByKeyAsync("MY_KEY");
            result.Should().NotBeNull();
            result!.ConfigValue.Should().Be("MY_VAL");

            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SystemConfig, bool>>>()))
                     .ReturnsAsync(new List<SystemConfig>());
            var resNull = await _service.GetByKeyAsync("ABSENT");
            resNull.Should().BeNull();
        }

        [Fact]
        public async Task UTCID17_GetAllAsync_SortOrders()
        {
            var configs = new List<SystemConfig>
            {
                new SystemConfig { ConfigKey = "B", UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-5), CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5) },
                new SystemConfig { ConfigKey = "A", UpdatedAt = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow }
            };
            _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(configs);

            var res1 = await _service.GetAllAsync(new SystemConfigQueryParameters { Sort = "-configkey" });
            res1.Items.First().ConfigKey.Should().Be("B");

            var res2 = await _service.GetAllAsync(new SystemConfigQueryParameters { Sort = "configkey" });
            res2.Items.First().ConfigKey.Should().Be("A");

            var res3 = await _service.GetAllAsync(new SystemConfigQueryParameters { Sort = "-updatedat" });
            res3.Items.First().ConfigKey.Should().Be("A");

            var res4 = await _service.GetAllAsync(new SystemConfigQueryParameters { Sort = "createdat" });
            res4.Items.First().ConfigKey.Should().Be("B");
        }

        #endregion
    }
}
