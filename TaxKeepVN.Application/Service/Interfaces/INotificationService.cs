using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Common;

namespace TaxKeepVN.Application.Service.Interfaces
{
    public interface INotificationService
    {
        Task<PagedResult<DTOs.NotificationDto>> GetUserNotificationsAsync(Guid userId, NotificationQueryParameters query);
        Task MarkAsReadAsync(Guid notificationId, Guid userId);
    }
}
