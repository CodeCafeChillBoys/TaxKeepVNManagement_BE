using System;

namespace TaxKeepVN.Application.DTOs
{
    public class NotificationDto
    {
        public Guid NotificationId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string NotificationType { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public string? TargetActionUrl { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
