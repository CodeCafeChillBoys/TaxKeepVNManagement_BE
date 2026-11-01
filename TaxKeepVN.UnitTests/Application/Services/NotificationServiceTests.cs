using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using TaxKeepVN.Application.DTOs;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Implementations;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;
using Xunit;

namespace TaxKeepVN.UnitTests.Application.Services
{
    public class NotificationServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IGenericRepository<SystemNotification>> _mockRepo;
        private readonly NotificationService _service;

        public NotificationServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockRepo = new Mock<IGenericRepository<SystemNotification>>();

            _mockUnitOfWork.Setup(u => u.Repository<SystemNotification>()).Returns(_mockRepo.Object);

            _service = new NotificationService(_mockUnitOfWork.Object);
        }

        #region GetUserNotificationsAsync

        [Fact]
        public async Task UTCID01_GetUserNotificationsAsync_ReturnsPagedResult()
        {
            var userId = Guid.NewGuid();
            var notifications = new List<SystemNotification>
            {
                new SystemNotification
                {
                    NotificationId = Guid.NewGuid(),
                    UserId = userId,
                    Title = "Thông báo 1",
                    Message = "Nội dung 1",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                },
                new SystemNotification
                {
                    NotificationId = Guid.NewGuid(),
                    UserId = userId,
                    Title = "Thông báo 2",
                    Message = "Nội dung 2",
                    IsRead = true,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-10)
                }
            };

            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SystemNotification, bool>>>()))
                     .ReturnsAsync(notifications);

            var query = new NotificationQueryParameters { Page = 1, Size = 10 };
            var result = await _service.GetUserNotificationsAsync(userId, query);

            result.Should().NotBeNull();
            result.Items.Should().HaveCount(2);
            result.Pagination.TotalItems.Should().Be(2);
        }

        [Theory]
        [InlineData("title", "A Thông báo")]
        [InlineData("-title", "B Thông báo")]
        [InlineData("createdat", "A Thông báo")]
        [InlineData("-createdat", "B Thông báo")]
        [InlineData("isread", "A Thông báo")]
        [InlineData("-isread", "B Thông báo")]
        [InlineData("unknown_sort", "B Thông báo")]
        public async Task UTCID01_B_GetUserNotificationsAsync_SortOptions_SortsCorrectly(string sortOption, string expectedFirstTitle)
        {
            var userId = Guid.NewGuid();
            var notifications = new List<SystemNotification>
            {
                new SystemNotification
                {
                    NotificationId = Guid.NewGuid(),
                    UserId = userId,
                    Title = "A Thông báo",
                    Message = "Nội dung A",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow.AddHours(-2)
                },
                new SystemNotification
                {
                    NotificationId = Guid.NewGuid(),
                    UserId = userId,
                    Title = "B Thông báo",
                    Message = "Nội dung B",
                    IsRead = true,
                    CreatedAt = DateTime.UtcNow
                }
            };

            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SystemNotification, bool>>>()))
                     .ReturnsAsync(notifications);

            var query = new NotificationQueryParameters { Page = 1, Size = 10, Sort = sortOption };
            var result = await _service.GetUserNotificationsAsync(userId, query);

            result.Items.First().Title.Should().Be(expectedFirstTitle);
        }

        [Fact]
        public async Task UTCID01_C_GetUserNotificationsAsync_WithIsReadAndSearch_FiltersCorrectly()
        {
            var userId = Guid.NewGuid();
            var notifications = new List<SystemNotification>
            {
                new SystemNotification
                {
                    NotificationId = Guid.NewGuid(),
                    UserId = userId,
                    Title = "Thuế TNCN",
                    Message = "Nộp thuế",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                },
                new SystemNotification
                {
                    NotificationId = Guid.NewGuid(),
                    UserId = userId,
                    Title = "Lịch hẹn",
                    Message = "Tư vấn",
                    IsRead = true,
                    CreatedAt = DateTime.UtcNow
                }
            };

            _mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SystemNotification, bool>>>()))
                     .ReturnsAsync(notifications);

            var query = new NotificationQueryParameters
            {
                Page = 1,
                Size = 10,
                IsRead = false,
                Search = "TNCN"
            };

            var result = await _service.GetUserNotificationsAsync(userId, query);

            result.Items.Should().HaveCount(1);
            result.Items.First().Title.Should().Be("Thuế TNCN");
        }

        #endregion

        #region MarkAsReadAsync

        [Fact]
        public async Task UTCID02_MarkAsReadAsync_NotFound_ThrowsNotFoundException()
        {
            var notifId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            _mockRepo.Setup(r => r.GetByIdAsync(notifId)).ReturnsAsync((SystemNotification?)null);

            await Assert.ThrowsAsync<NotFoundException>(() => _service.MarkAsReadAsync(notifId, userId));
        }

        [Fact]
        public async Task UTCID03_MarkAsReadAsync_Forbidden_ThrowsForbiddenException()
        {
            var notifId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var hackerId = Guid.NewGuid();

            var notification = new SystemNotification { NotificationId = notifId, UserId = ownerId, IsRead = false };
            _mockRepo.Setup(r => r.GetByIdAsync(notifId)).ReturnsAsync(notification);

            await Assert.ThrowsAsync<ForbiddenException>(() => _service.MarkAsReadAsync(notifId, hackerId));
        }

        [Fact]
        public async Task UTCID04_MarkAsReadAsync_Valid_MarksAsRead()
        {
            var notifId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            var notification = new SystemNotification { NotificationId = notifId, UserId = userId, IsRead = false };
            _mockRepo.Setup(r => r.GetByIdAsync(notifId)).ReturnsAsync(notification);

            await _service.MarkAsReadAsync(notifId, userId);

            notification.IsRead.Should().BeTrue();
            _mockRepo.Verify(r => r.Update(notification), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        #endregion
    }
}
