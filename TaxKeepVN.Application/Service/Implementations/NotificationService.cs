using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    public class NotificationService : INotificationService
    {
        private readonly IUnitOfWork _unitOfWork;

        public NotificationService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<List<NotificationDto>> GetUserNotificationsAsync(Guid userId)
        {
            var repo = _unitOfWork.Repository<SystemNotification>();
            var notifications = await repo.FindAsync(n => n.UserId == userId);
            
            return notifications.OrderByDescending(n => n.CreatedAt).Select(n => new NotificationDto
            {
                NotificationId = n.NotificationId,
                Title = n.Title,
                Message = n.Message,
                NotificationType = n.NotificationType,
                IsRead = n.IsRead,
                TargetActionUrl = n.TargetActionUrl,
                CreatedAt = n.CreatedAt
            }).ToList();
        }

        public async Task MarkAsReadAsync(Guid notificationId, Guid userId)
        {
            var repo = _unitOfWork.Repository<SystemNotification>();
            var notification = await repo.GetByIdAsync(notificationId);
            
            if (notification == null)
                throw new NotFoundException("Không tìm thấy thông báo.");

            if (notification.UserId != userId)
                throw new ForbiddenException("Bạn không có quyền thao tác trên thông báo này.");

            notification.IsRead = true;
            repo.Update(notification);
            await _unitOfWork.SaveChangesAsync();
        }
    }
}
