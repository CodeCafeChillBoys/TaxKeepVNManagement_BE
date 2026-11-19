using System;

namespace TaxKeepVN.Domain.Entities
{
    public class User
    {
        public Guid UserId { get; set; }

        /// <summary>Mã số thuế cá nhân (MST), có thể chưa có khi đăng ký</summary>
        public string? TaxIdNumber { get; set; }

        /// <summary>Số căn cước công dân — dùng để đăng nhập</summary>
        public string CitizenId { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string? PhoneNumber { get; set; }

        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>Khóa ngoại FK trỏ tới bảng roles (Mặc định 1: taxpayer)</summary>
        public int RoleId { get; set; } = 1;

        /// <summary>taxpayer | admin | expert</summary>
        public string UserRole { get; set; } = "taxpayer";

        /// <summary>
        /// True khi user đã xác minh đây là mã số thuế cá nhân hợp lệ (eKYC hoặc nhập MST).
        /// Được set bởi ProfileService khi user cập nhật TaxIdNumber.
        /// </summary>
        public bool IsVerified { get; set; } = false;

        /// <summary>
        /// True khi user đã click link xác minh trong email đăng ký.
        /// Khác với IsVerified (xác minh MST) — đây là xác minh email.
        /// </summary>
        public bool IsEmailVerified { get; set; } = false;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        public DateOnly? DateOfBirth { get; set; }

        public string? Address { get; set; }

        /// <summary>active | inactive | suspended</summary>
        public string Status { get; set; } = "active";

        /// <summary>
        /// Token xác minh email (GUID ngẫu nhiên). Null sau khi đã xác minh thành công.
        /// </summary>
        public string? EmailVerificationToken { get; set; }

        /// <summary>
        /// Thời điểm hết hạn của token xác minh email (mặc định 24 giờ).
        /// </summary>
        public DateTimeOffset? EmailVerificationTokenExpiry { get; set; }

        /// <summary>
        /// Token đặt lại mật khẩu (GUID ngẫu nhiên). Null sau khi đã đổi mật khẩu thành công.
        /// </summary>
        public string? PasswordResetToken { get; set; }

        /// <summary>
        /// Thời điểm hết hạn của token đặt lại mật khẩu (mặc định 15 phút).
        /// </summary>
        public DateTimeOffset? PasswordResetTokenExpiry { get; set; }

        // Navigation properties
        public Role? Role { get; set; }
        public ICollection<TaxPeriod> TaxPeriods { get; set; } = new List<TaxPeriod>();
        public ICollection<ExpertApplication> ExpertApplications { get; set; } = new List<ExpertApplication>();
        public ExpertProfile? ExpertProfile { get; set; }
        public ICollection<ExpertReview> ExpertReviews { get; set; } = new List<ExpertReview>();
        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();

        // Subscription navigation
        public ICollection<UserSubscription> Subscriptions { get; set; } = new List<UserSubscription>();
        public ICollection<SubscriptionQuotaUsage> QuotaUsages { get; set; } = new List<SubscriptionQuotaUsage>();
    }
}