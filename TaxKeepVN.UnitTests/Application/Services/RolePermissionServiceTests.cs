using System.Linq.Expressions;
using Moq;
using TaxKeepDomain.Entities;
using TaxKeepVN.Application.DTOs.Permissions;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Implementations;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.UnitTests.Application.Services;

public class RolePermissionServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Save_ReconcilesSelection_WithoutDuplicatingExistingPermissions(bool clearAll)
    {
        var (service, unit, assignments) = Create();
        var selected = clearAll ? new List<int>() : new List<int> { 2, 3, 3 };
        var result = await service.UpdatePermissionsAsync(3, new() { PermissionIds = selected });

        assignments.Verify(r => r.Remove(It.Is<RolePermission>(p => p.PermissionId == 1)), Times.Once);
        assignments.Verify(r => r.Remove(It.Is<RolePermission>(p => p.PermissionId == 2)), clearAll ? Times.Once() : Times.Never());
        assignments.Verify(r => r.AddAsync(It.Is<RolePermission>(p => p.RoleId == 3 && p.PermissionId == 3)), clearAll ? Times.Never() : Times.Once());
        unit.Verify(u => u.SaveChangesAsync(), Times.Once);
        Assert.Equal(clearAll ? 0 : 2, result.Permissions.Count(p => p.Granted));
    }

    [Fact]
    public async Task Save_UnknownPermission_DoesNotMutateOrSave()
    {
        var (service, unit, assignments) = Create();
        await Assert.ThrowsAsync<BadRequestException>(() => service.UpdatePermissionsAsync(3, new() { PermissionIds = new() { 999 } }));
        assignments.Verify(r => r.Remove(It.IsAny<RolePermission>()), Times.Never);
        assignments.Verify(r => r.AddAsync(It.IsAny<RolePermission>()), Times.Never);
        unit.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task Get_ReturnsAllPermissions_WithGrantedFlags()
    {
        var (service, _, _) = Create();
        var result = await service.GetPermissionsAsync(3);
        Assert.Equal(3, result.Permissions.Count);
        Assert.True(result.Permissions.Single(p => p.Id == 1).Granted);
        Assert.False(result.Permissions.Single(p => p.Id == 3).Granted);
    }

    [Fact]
    public async Task Save_InvalidatesPermissionCache_WhenCacheServiceProvided()
    {
        var mockCache = new Mock<IPermissionCacheService>();
        var (service, unit, assignments) = Create(mockCache.Object);

        await service.UpdatePermissionsAsync(3, new() { PermissionIds = new List<int> { 1 } });

        mockCache.Verify(c => c.InvalidateRolePermissionsAsync(3), Times.Once);
    }

    private static (RolePermissionService, Mock<IUnitOfWork>, Mock<IGenericRepository<RolePermission>>) Create(IPermissionCacheService? cacheService = null)
    {
        var unit = new Mock<IUnitOfWork>();
        var roles = new Mock<IGenericRepository<Role>>();
        var permissions = new Mock<IGenericRepository<Permission>>();
        var assignments = new Mock<IGenericRepository<RolePermission>>();
        unit.Setup(u => u.Repository<Role>()).Returns(roles.Object);
        unit.Setup(u => u.Repository<Permission>()).Returns(permissions.Object);
        unit.Setup(u => u.Repository<RolePermission>()).Returns(assignments.Object);
        roles.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Role, bool>>>()))
            .ReturnsAsync(new[] { new Role { Id = 3, Name = "Expert" } });
        permissions.Setup(r => r.GetAllAsync()).ReturnsAsync(Enumerable.Range(1, 3)
            .Select(id => new Permission { Id = id, Code = $"document.p{id}" }).ToList());
        assignments.Setup(r => r.FindAsync(It.IsAny<Expression<Func<RolePermission, bool>>>()))
            .ReturnsAsync(new[] { new RolePermission { RoleId = 3, PermissionId = 1 }, new RolePermission { RoleId = 3, PermissionId = 2 } });
        assignments.Setup(r => r.AddAsync(It.IsAny<RolePermission>())).Returns(Task.CompletedTask);
        unit.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        return (new RolePermissionService(unit.Object, cacheService), unit, assignments);
    }
}
