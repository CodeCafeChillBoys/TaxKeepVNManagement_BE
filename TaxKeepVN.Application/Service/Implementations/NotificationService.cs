using System;
using System.Linq;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs;
using TaxKeepVN.Application.DTOs.Common;
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

        public async Task<PagedResult<NotificationDto>> GetUserNotificationsAsync(Guid userId, NotificationQueryParameters query)
        {
            var repo = _unitOfWork.Repository<SystemNotification>();
            var notifications = await repo.FindAsync(n => n.UserId == userId);

            // Filter by isRead if provided
            if (query.IsRead.HasValue)
                notifications = notifications.Where(n => n.IsRead == query.IsRead.Value);

            // Searching by title or message
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var kw = query.Search.ToLower();
                notifications = notifications.Where(n =>
                    n.Title.ToLower().Contains(kw) ||
                    n.Message.ToLower().Contains(kw));
            }

            // Sorting
            notifications = ApplySort(notifications, query.Sort);

            var totalItems = notifications.Count();
            var items = notifications
                .Skip((query.Page - 1) * query.Size)
                .Take(query.Size)
                .Select(n => new NotificationDto
                {
                    NotificationId = n.NotificationId,
                    Title = n.Title,
                    Message = n.Message,
                    NotificationType = n.NotificationType,
                    IsRead = n.IsRead,
                    TargetActionUrl = n.TargetActionUrl,
                    CreatedAt = n.CreatedAt
                })
                .ToList();

            return new PagedResult<NotificationDto>
            {
                Items = items,
                Pagination = new PaginationMeta
                {
                    Page = query.Page,
                    PageSize = query.Size,
                    TotalItems = totalItems,
                    TotalPages = (int)Math.Ceiling(totalItems / (double)query.Size)
                }
            };
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

        private static System.Collections.Generic.IEnumerable<SystemNotification> ApplySort(
            System.Collections.Generic.IEnumerable<SystemNotification> source, string? sort)
        {
            if (string.IsNullOrWhiteSpace(sort))
                return source.OrderByDescending(n => n.CreatedAt);

            bool desc = sort.StartsWith("-");
            string field = sort.TrimStart('-').ToLower();

            return field switch
            {
                "title" => desc ? source.OrderByDescending(n => n.Title) : source.OrderBy(n => n.Title),
                "createdat" => desc ? source.OrderByDescending(n => n.CreatedAt) : source.OrderBy(n => n.CreatedAt),
                "isread" => desc ? source.OrderByDescending(n => n.IsRead) : source.OrderBy(n => n.IsRead),
                _ => source.OrderByDescending(n => n.CreatedAt)
            };
        }
    }
}
