using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TaxKeepVN.Application.DTOs.Consultations;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Implementations;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Domain.IRepositories;
using Xunit;

namespace TaxKeepVN.UnitTests.Application.Services
{
    public class ConsultationChatServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<ILogger<ConsultationChatService>> _mockLogger;
        private readonly Mock<IConsultationRealtimeNotifier> _mockNotifier;
        private readonly ConsultationChatService _service;

        private readonly Guid _taxpayerId = Guid.NewGuid();
        private readonly Guid _expertUserId = Guid.NewGuid();
        private readonly Guid _expertProfileId = Guid.NewGuid();
        private readonly Guid _expertSlotId = Guid.NewGuid();
        private readonly Guid _bookingId = Guid.NewGuid();

        private readonly Booking _defaultBooking;
        private readonly ExpertSlot _defaultSlot;
        private readonly ExpertProfile _defaultProfile;
        private readonly User _defaultTaxpayer;
        private readonly User _defaultExpertUser;
        private readonly ConsultationPlatformPolicy _defaultPolicy;

        public ConsultationChatServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockLogger = new Mock<ILogger<ConsultationChatService>>();
            _mockNotifier = new Mock<IConsultationRealtimeNotifier>();

            _service = new ConsultationChatService(_mockUnitOfWork.Object, _mockLogger.Object, _mockNotifier.Object);

            _defaultPolicy = new ConsultationPlatformPolicy
            {
                Id = 1,
                IsActive = true,
                SessionOpenBeforeMinutes = 60, // Mở trước 60 phút để test không bị chặn ROOM_NOT_OPEN_YET
                SessionNoShowMinutes = 15,
                ChatMessageMaxLength = 4000,
                ChatAllowedAttachmentTypes = "IMAGE,PDF,DOCUMENT",
                ChatMaxAttachmentUrlLength = 1000,
                ChatHistoryDefaultPageSize = 50,
                ChatHistoryMaxPageSize = 100,
                ChatRoomUrlBase = "https://meet.jit.si"
            };

            var policyRepo = new Mock<IGenericRepository<ConsultationPlatformPolicy>>();
            policyRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationPlatformPolicy, bool>>>()))
                      .ReturnsAsync(new List<ConsultationPlatformPolicy> { _defaultPolicy });
            _mockUnitOfWork.Setup(u => u.Repository<ConsultationPlatformPolicy>()).Returns(policyRepo.Object);

            var sysConfigRepo = new Mock<IGenericRepository<SystemConfig>>();
            sysConfigRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SystemConfig, bool>>>()))
                         .ReturnsAsync(new List<SystemConfig>());
            _mockUnitOfWork.Setup(u => u.Repository<SystemConfig>()).Returns(sysConfigRepo.Object);

            _defaultProfile = new ExpertProfile
            {
                Id = _expertProfileId,
                UserId = _expertUserId,
                JobTitle = "Chuyên gia tư vấn thuế cao cấp",
                LatestApplicationId = Guid.NewGuid()
            };

            _defaultSlot = new ExpertSlot
            {
                Id = _expertSlotId,
                ExpertProfileId = _expertProfileId,
                SlotDate = DateOnly.FromDateTime(DateTime.UtcNow),
                StartTime = TimeOnly.FromDateTime(DateTime.UtcNow.AddMinutes(5)),
                EndTime = TimeOnly.FromDateTime(DateTime.UtcNow.AddMinutes(65)),
                SessionType = SessionType.CHAT
            };

            _defaultBooking = new Booking
            {
                Id = _bookingId,
                BookingCode = "BK-20261118-TEST",
                UserId = _taxpayerId,
                ExpertSlotId = _expertSlotId,
                SpecializationId = 1,
                SessionType = SessionType.CHAT,
                DurationMinutes = 60,
                Fee = 200000m,
                TopicTitle = "Tư vấn bóc tách lương OT",
                Status = BookingStatus.CONFIRMED
            };

            _defaultTaxpayer = new User
            {
                UserId = _taxpayerId,
                FullName = "Nguyễn Văn Người Nộp Thuế",
                Email = "taxpayer@taxkeep.vn"
            };

            _defaultExpertUser = new User
            {
                UserId = _expertUserId,
                FullName = "Trần Thị Chuyên Gia",
                Email = "expert@taxkeep.vn"
            };

            SetupMockRepo(_defaultBooking);
            SetupMockRepo(_defaultSlot);
            SetupMockRepo(_defaultProfile);

            var userRepo = new Mock<IGenericRepository<User>>();
            userRepo.Setup(r => r.GetByIdAsync(_taxpayerId)).ReturnsAsync(_defaultTaxpayer);
            userRepo.Setup(r => r.GetByIdAsync(_expertUserId)).ReturnsAsync(_defaultExpertUser);
            _mockUnitOfWork.Setup(u => u.Repository<User>()).Returns(userRepo.Object);

            var msgRepo = new Mock<IGenericRepository<ConsultationMessage>>();
            msgRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationMessage, bool>>>()))
                   .ReturnsAsync(new List<ConsultationMessage>());
            _mockUnitOfWork.Setup(u => u.Repository<ConsultationMessage>()).Returns(msgRepo.Object);

            var sessionRepo = new Mock<IGenericRepository<ConsultationSession>>();
            sessionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationSession, bool>>>()))
                       .ReturnsAsync(new List<ConsultationSession>());
            _mockUnitOfWork.Setup(u => u.Repository<ConsultationSession>()).Returns(sessionRepo.Object);

            var appRepo = new Mock<IGenericRepository<ExpertApplication>>();
            appRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                   .ReturnsAsync(new ExpertApplication { AvatarUrl = "https://cdn.taxkeep.vn/avatar.png" });
            _mockUnitOfWork.Setup(u => u.Repository<ExpertApplication>()).Returns(appRepo.Object);

            var specRepo = new Mock<IGenericRepository<Specialization>>();
            specRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Specialization, bool>>>()))
                    .ReturnsAsync(new List<Specialization> { new Specialization { Id = 1, Name = "Thuế TNCN Cá Nhân" } });
            _mockUnitOfWork.Setup(u => u.Repository<Specialization>()).Returns(specRepo.Object);
        }

        private void SetupMockRepo<T>(T entity) where T : class
        {
            var repo = new Mock<IGenericRepository<T>>();
            repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(entity);
            _mockUnitOfWork.Setup(u => u.Repository<T>()).Returns(repo.Object);
        }

        [Fact]
        public async Task SendMessageAsync_ValidTaxpayerMessage_ReturnsMessageDtoAndCallsNotifier()
        {
            // Arrange
            var request = new SendConsultationMessageRequest
            {
                Content = "Chào chuyên gia, tôi đã bổ sung hợp đồng lao động ca đêm.",
                AttachmentUrl = "https://cdn.taxkeep.vn/contract.pdf",
                AttachmentType = "PDF"
            };

            // Act
            var result = await _service.SendMessageAsync(_taxpayerId, "taxpayer", _bookingId, request);

            // Assert
            result.Should().NotBeNull();
            result.BookingId.Should().Be(_bookingId);
            result.SenderId.Should().Be(_taxpayerId);
            result.SenderRole.Should().Be("taxpayer");
            result.SenderName.Should().Be(_defaultTaxpayer.FullName);
            result.Content.Should().Be(request.Content);
            result.AttachmentUrl.Should().Be(request.AttachmentUrl);
            result.IsMine.Should().BeTrue();

            _mockNotifier.Verify(n => n.NotifyChatMessageReceivedAsync(_bookingId, It.IsAny<ConsultationMessageDto>()), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.AtLeastOnce);
        }

        [Fact]
        public async Task SendMessageAsync_EmptyContent_ThrowsBadRequestException()
        {
            // Arrange
            var request = new SendConsultationMessageRequest { Content = "   " };

            // Act & Assert
            await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.SendMessageAsync(_taxpayerId, "taxpayer", _bookingId, request));
        }

        [Fact]
        public async Task SendMessageAsync_ContentExceedsAdminConfiguredLength_ThrowsBadRequestException()
        {
            // Arrange: Admin cấu hình độ dài tối đa là 50 ký tự
            _defaultPolicy.ChatMessageMaxLength = 50;
            var request = new SendConsultationMessageRequest
            {
                Content = new string('A', 60)
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.SendMessageAsync(_taxpayerId, "taxpayer", _bookingId, request));
            ex.ErrorCode.Should().Be("CONTENT_TOO_LONG");
        }

        [Fact]
        public async Task SendMessageAsync_AttachmentTypeNotAllowedByAdmin_ThrowsBadRequestException()
        {
            // Arrange: Admin cấu hình chỉ cho phép IMAGE,PDF
            _defaultPolicy.ChatAllowedAttachmentTypes = "IMAGE,PDF";
            var request = new SendConsultationMessageRequest
            {
                Content = "Gửi anh file nén mã nguồn",
                AttachmentUrl = "https://cdn.taxkeep.vn/source.zip",
                AttachmentType = "ZIP"
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.SendMessageAsync(_taxpayerId, "taxpayer", _bookingId, request));
            ex.ErrorCode.Should().Be("INVALID_ATTACHMENT_TYPE");
        }

        [Fact]
        public async Task SendMessageAsync_SystemConfigOverridesPlatformPolicy()
        {
            // Arrange: SystemConfig ghi đè CHAT_MESSAGE_MAX_LENGTH thành 30 ký tự
            var sysConfigs = new List<SystemConfig>
            {
                new SystemConfig
                {
                    ConfigKey = "CHAT_MESSAGE_MAX_LENGTH",
                    ConfigValue = "30",
                    IsActive = true
                }
            };

            var sysRepo = new Mock<IGenericRepository<SystemConfig>>();
            sysRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<SystemConfig, bool>>>()))
                   .ReturnsAsync(sysConfigs);
            _mockUnitOfWork.Setup(u => u.Repository<SystemConfig>()).Returns(sysRepo.Object);

            var request = new SendConsultationMessageRequest
            {
                Content = new string('B', 35)
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.SendMessageAsync(_taxpayerId, "taxpayer", _bookingId, request));
            ex.ErrorCode.Should().Be("CONTENT_TOO_LONG");
        }

        [Fact]
        public async Task SendMessageAsync_UnauthorizedUser_ThrowsForbiddenException()
        {
            // Arrange
            var unauthorizedUserId = Guid.NewGuid();
            var request = new SendConsultationMessageRequest { Content = "Hello" };

            // Act & Assert
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                _service.SendMessageAsync(unauthorizedUserId, "taxpayer", _bookingId, request));
        }

        [Fact]
        public async Task SendMessageAsync_PendingPaymentStatus_ThrowsBadRequestException()
        {
            // Arrange
            _defaultBooking.Status = BookingStatus.PENDING_PAYMENT;
            var request = new SendConsultationMessageRequest { Content = "Hello" };

            // Act & Assert
            await Assert.ThrowsAsync<BadRequestException>(() =>
                _service.SendMessageAsync(_taxpayerId, "taxpayer", _bookingId, request));
        }

        [Fact]
        public async Task GetChatHistoryAsync_ValidTaxpayer_ReturnsChatHistoryWithExpertPartnerAndPaging()
        {
            // Arrange
            var mockMessages = new List<ConsultationMessage>
            {
                new ConsultationMessage
                {
                    Id = Guid.NewGuid(),
                    BookingId = _bookingId,
                    SenderId = _taxpayerId,
                    SenderRole = "taxpayer",
                    SenderName = _defaultTaxpayer.FullName,
                    Content = "Câu hỏi 1",
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
                },
                new ConsultationMessage
                {
                    Id = Guid.NewGuid(),
                    BookingId = _bookingId,
                    SenderId = _expertUserId,
                    SenderRole = "expert",
                    SenderName = _defaultExpertUser.FullName,
                    Content = "Trả lời 1 từ chuyên gia",
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-3)
                }
            };

            var msgRepo = new Mock<IGenericRepository<ConsultationMessage>>();
            msgRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationMessage, bool>>>()))
                   .ReturnsAsync(mockMessages);
            _mockUnitOfWork.Setup(u => u.Repository<ConsultationMessage>()).Returns(msgRepo.Object);

            var query = new ConsultationMessageQueryParameters { Page = 1, Size = 10, Sort = "createdAt" };

            // Act
            var result = await _service.GetChatHistoryAsync(_taxpayerId, "taxpayer", _bookingId, query);

            // Assert
            result.Should().NotBeNull();
            result.BookingId.Should().Be(_bookingId);
            result.Partner.UserId.Should().Be(_expertUserId);
            result.Partner.Role.Should().Be("expert");
            result.Messages.Items.Should().HaveCount(2);
            result.Messages.Pagination.TotalItems.Should().Be(2);
            result.Messages.Items[0].IsMine.Should().BeTrue();
            result.Messages.Items[1].IsMine.Should().BeFalse();
        }

        [Fact]
        public async Task GetChatHistoryAsync_DefaultPageSize_UsesAdminConfiguredDefault()
        {
            // Arrange: Admin đặt ChatHistoryDefaultPageSize = 25
            _defaultPolicy.ChatHistoryDefaultPageSize = 25;

            var msgRepo = new Mock<IGenericRepository<ConsultationMessage>>();
            msgRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationMessage, bool>>>()))
                   .ReturnsAsync(new List<ConsultationMessage>());
            _mockUnitOfWork.Setup(u => u.Repository<ConsultationMessage>()).Returns(msgRepo.Object);

            var query = new ConsultationMessageQueryParameters { Page = 1, Size = 0 };

            // Act
            var result = await _service.GetChatHistoryAsync(_taxpayerId, "taxpayer", _bookingId, query);

            // Assert
            result.Messages.Pagination.PageSize.Should().Be(25);
        }

        [Fact]
        public async Task GetMessageByIdAsync_ExistingMessage_ReturnsMessageDto()
        {
            // Arrange
            var messageId = Guid.NewGuid();
            var message = new ConsultationMessage
            {
                Id = messageId,
                BookingId = _bookingId,
                SenderId = _taxpayerId,
                SenderRole = "taxpayer",
                SenderName = _defaultTaxpayer.FullName,
                Content = "Tin nhắn cần tra cứu",
                CreatedAt = DateTimeOffset.UtcNow
            };

            var msgRepo = new Mock<IGenericRepository<ConsultationMessage>>();
            msgRepo.Setup(r => r.GetByIdAsync(messageId)).ReturnsAsync(message);
            _mockUnitOfWork.Setup(u => u.Repository<ConsultationMessage>()).Returns(msgRepo.Object);

            // Act
            var result = await _service.GetMessageByIdAsync(_taxpayerId, "taxpayer", _bookingId, messageId);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(messageId);
            result.Content.Should().Be(message.Content);
            result.IsMine.Should().BeTrue();
        }

        [Fact]
        public async Task MarkMessagesAsReadAsync_UpdatesUnreadMessages()
        {
            // Arrange
            var unreadMsg = new ConsultationMessage
            {
                Id = Guid.NewGuid(),
                BookingId = _bookingId,
                SenderId = _expertUserId,
                IsRead = false
            };

            var msgRepo = new Mock<IGenericRepository<ConsultationMessage>>();
            msgRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<ConsultationMessage, bool>>>()))
                   .ReturnsAsync(new List<ConsultationMessage> { unreadMsg });
            _mockUnitOfWork.Setup(u => u.Repository<ConsultationMessage>()).Returns(msgRepo.Object);

            // Act
            var result = await _service.MarkMessagesAsReadAsync(_taxpayerId, "taxpayer", _bookingId);

            // Assert
            result.Should().BeTrue();
            unreadMsg.IsRead.Should().BeTrue();
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task SendTypingIndicatorAsync_CallsNotifier()
        {
            // Act
            var result = await _service.SendTypingIndicatorAsync(_taxpayerId, "taxpayer", _bookingId, true);

            // Assert
            result.Should().BeTrue();
            _mockNotifier.Verify(n => n.NotifyTypingAsync(_bookingId, _taxpayerId, "taxpayer", true), Times.Once);
        }
    }
}
