using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaxKeepVN.Domain.Entities
{
    [Table("system_notifications")]
    public class SystemNotification
    {
        [Key]
        [Column("notification_id")]
        public Guid NotificationId { get; set; }

        [Column("user_id")]
        public Guid UserId { get; set; }

        [Column("title")]
        [MaxLength(255)]
        public string Title { get; set; } = string.Empty;

        [Column("message")]
        public string Message { get; set; } = string.Empty;

        [Column("notification_type")]
        [MaxLength(100)]
        public string NotificationType { get; set; } = string.Empty;

        [Column("is_read")]
        public bool IsRead { get; set; }

        [Column("target_action_url")]
        public string? TargetActionUrl { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
