using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using TaxKeepDomain.Entities;
using TaxKeepVN.Application.DTOs.Permissions;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Implementations;
using TaxKeepVN.Domain.IRepositories;
using Xunit;

namespace TaxKeepVN.UnitTests.Application.Services
{
    public class PermissionServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IGenericRepository<Permission>> _mockPermissionRepo;
        private readonly Mock<IGenericRepository<RolePermission>> _mockRolePermissionRepo;
        private readonly PermissionService _service;

        public PermissionServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockPermissionRepo = new Mock<IGenericRepository<Permission>>();
            _mockRolePermissionRepo = new Mock<IGenericRepository<RolePermission>>();

            _mockUnitOfWork.Setup(u => u.Repository<Permission>()).Returns(_mockPermissionRepo.Object);
            _mockUnitOfWork.Setup(u => u.Repository<RolePermission>()).Returns(_mockRolePermissionRepo.Object);

            _service = new PermissionService(_mockUnitOfWork.Object);
        }

        #region GetAllAsync

        [Fact]
        public async Task GetAllAsync_ReturnsAllPermissionsOrderedByGroupNameThenCode()
        {
            // Arrange
            var list = new List<Permission>
            {
                new() { Id = 1, GroupName = "User", Code = "user.view", Name = "Xem người dùng" },
                new() { Id = 2, GroupName = "Document", Code = "doc.upload", Name = "Tải tài liệu" },
                new() { Id = 3, GroupName = "Document", Code = "doc.delete", Name = "Xóa tài liệu" },
                new() { Id = 4, GroupName = "Admin", Code = "admin.config", Name = "Cấu hình" }
            };
            _mockPermissionRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(list);

            // Act
            var result = (await _service.GetAllAsync()).ToList();

            // Assert
            result.Should().HaveCount(4);
            result[0].GroupName.Should().Be("Admin");
            result[0].Code.Should().Be("admin.config");
            result[1].GroupName.Should().Be("Document");
            result[1].Code.Should().Be("doc.delete");
            result[2].GroupName.Should().Be("Document");
            result[2].Code.Should().Be("doc.upload");
            result[3].GroupName.Should().Be("User");
            result[3].Code.Should().Be("user.view");
        }

        #endregion

        #region GetByIdAsync

        [Fact]
        public async Task GetByIdAsync_WhenPermissionExists_ReturnsMappedDto()
        {
            // Arrange
            var permission = new Permission
            {
                Id = 10,
                Code = "tax.settle",
                Name = "Quyết toán thuế",
                GroupName = "Tax"
            };
            _mockPermissionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Permission, bool>>>()))
                               .ReturnsAsync(new List<Permission> { permission });

            // Act
            var result = await _service.GetByIdAsync(10);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(10);
            result.Code.Should().Be("tax.settle");
            result.Name.Should().Be("Quyết toán thuế");
            result.GroupName.Should().Be("Tax");
        }

        [Fact]
        public async Task GetByIdAsync_WhenPermissionNotFound_ThrowsNotFoundException()
        {
            // Arrange
            _mockPermissionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Permission, bool>>>()))
                               .ReturnsAsync(new List<Permission>());

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() => _service.GetByIdAsync(99));
        }

        #endregion

        #region CreateAsync

        [Theory]
        [InlineData("", "ValidGroup")]
        [InlineData("   ", "ValidGroup")]
        [InlineData("ValidName", "")]
        [InlineData("ValidName", "   ")]
        public async Task CreateAsync_WhenNameOrGroupNameIsWhitespace_ThrowsBadRequestException(string name, string groupName)
        {
            // Arrange
            var dto = new CreatePermissionDto
            {
                Code = "doc.read",
                Name = name,
                GroupName = groupName
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<BadRequestException>(() => _service.CreateAsync(dto));
            ex.ErrorCode.Should().Be("INVALID_PERMISSION");
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task CreateAsync_WhenCodeIsWhitespace_ThrowsBadRequestException(string code)
        {
            // Arrange
            var dto = new CreatePermissionDto
            {
                Code = code,
                Name = "Tên quyền",
                GroupName = "Nhóm quyền"
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<BadRequestException>(() => _service.CreateAsync(dto));
            ex.ErrorCode.Should().Be("INVALID_PERMISSION_CODE");
        }

        [Fact]
        public async Task CreateAsync_WhenCodeAlreadyExists_ThrowsConflictException()
        {
            // Arrange
            var dto = new CreatePermissionDto
            {
                Code = "doc.view",
                Name = "Xem tài liệu",
                GroupName = "Document"
            };
            var existing = new Permission { Id = 1, Code = "doc.view", Name = "Xem", GroupName = "Document" };
            _mockPermissionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Permission, bool>>>()))
                               .ReturnsAsync(new List<Permission> { existing });

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(dto));
            ex.ErrorCode.Should().Be("PERMISSION_CODE_EXISTS");
        }

        [Fact]
        public async Task CreateAsync_WhenValidDto_CreatesPermissionAndReturnsDto()
        {
            // Arrange
            var dto = new CreatePermissionDto
            {
                Code = "  DOC.CREATE  ",
                Name = "  Tạo tài liệu  ",
                GroupName = "  Document  "
            };
            _mockPermissionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Permission, bool>>>()))
                               .ReturnsAsync(new List<Permission>());

            // Act
            var result = await _service.CreateAsync(dto);

            // Assert
            result.Code.Should().Be("doc.create");
            result.Name.Should().Be("Tạo tài liệu");
            result.GroupName.Should().Be("Document");

            _mockPermissionRepo.Verify(r => r.AddAsync(It.Is<Permission>(p =>
                p.Code == "doc.create" &&
                p.Name == "Tạo tài liệu" &&
                p.GroupName == "Document")), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion

        #region UpdateAsync

        [Theory]
        [InlineData("", "ValidGroup")]
        [InlineData("   ", "ValidGroup")]
        [InlineData("ValidName", "")]
        [InlineData("ValidName", "   ")]
        public async Task UpdateAsync_WhenNameOrGroupNameIsWhitespace_ThrowsBadRequestException(string name, string groupName)
        {
            // Arrange
            var dto = new UpdatePermissionDto
            {
                Name = name,
                GroupName = groupName
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<BadRequestException>(() => _service.UpdateAsync(1, dto));
            ex.ErrorCode.Should().Be("INVALID_PERMISSION");
        }

        [Fact]
        public async Task UpdateAsync_WhenPermissionNotFound_ThrowsNotFoundException()
        {
            // Arrange
            var dto = new UpdatePermissionDto
            {
                Name = "Tên mới",
                GroupName = "Nhóm mới"
            };
            _mockPermissionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Permission, bool>>>()))
                               .ReturnsAsync(new List<Permission>());

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() => _service.UpdateAsync(123, dto));
        }

        [Fact]
        public async Task UpdateAsync_WhenValidDto_UpdatesFieldsAndReturnsDto()
        {
            // Arrange
            var existing = new Permission
            {
                Id = 5,
                Code = "doc.edit",
                Name = "Tên cũ",
                GroupName = "Nhóm cũ"
            };
            _mockPermissionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Permission, bool>>>()))
                               .ReturnsAsync(new List<Permission> { existing });

            var dto = new UpdatePermissionDto
            {
                Name = "  Tên Mới  ",
                GroupName = "  Nhóm Mới  "
            };

            // Act
            var result = await _service.UpdateAsync(5, dto);

            // Assert
            result.Id.Should().Be(5);
            result.Code.Should().Be("doc.edit");
            result.Name.Should().Be("Tên Mới");
            result.GroupName.Should().Be("Nhóm Mới");

            existing.Name.Should().Be("Tên Mới");
            existing.GroupName.Should().Be("Nhóm Mới");

            _mockPermissionRepo.Verify(r => r.Update(existing), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion

        #region DeleteAsync

        [Fact]
        public async Task DeleteAsync_WhenPermissionNotFound_ThrowsNotFoundException()
        {
            // Arrange
            _mockPermissionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Permission, bool>>>()))
                               .ReturnsAsync(new List<Permission>());

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() => _service.DeleteAsync(99));
        }

        [Fact]
        public async Task DeleteAsync_WhenPermissionIsInUse_ThrowsConflictException()
        {
            // Arrange
            var permission = new Permission { Id = 1, Code = "doc.view" };
            _mockPermissionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Permission, bool>>>()))
                               .ReturnsAsync(new List<Permission> { permission });

            var rolePermission = new RolePermission { RoleId = 1, PermissionId = 1 };
            _mockRolePermissionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<RolePermission, bool>>>()))
                                   .ReturnsAsync(new List<RolePermission> { rolePermission });

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ConflictException>(() => _service.DeleteAsync(1));
            ex.ErrorCode.Should().Be("PERMISSION_IN_USE");
        }

        [Fact]
        public async Task DeleteAsync_WhenPermissionNotInUse_RemovesPermissionAndReturnsTrue()
        {
            // Arrange
            var permission = new Permission { Id = 2, Code = "doc.delete" };
            _mockPermissionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Permission, bool>>>()))
                               .ReturnsAsync(new List<Permission> { permission });

            _mockRolePermissionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<RolePermission, bool>>>()))
                                   .ReturnsAsync(new List<RolePermission>());

            // Act
            var result = await _service.DeleteAsync(2);

            // Assert
            result.Should().BeTrue();
            _mockPermissionRepo.Verify(r => r.Remove(permission), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion
    }
}
