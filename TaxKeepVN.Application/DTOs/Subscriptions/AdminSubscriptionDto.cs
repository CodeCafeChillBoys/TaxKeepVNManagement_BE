using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TaxKeepVN.Application.DTOs.Subscriptions
{
    /// <summary>
    /// Tham số tìm kiếm, phân trang và bộ lọc danh sách gói người dùng dành cho Admin
    /// </summary>
    public class AdminSubscriptionFilterRequest
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;

        /// <summary>Tìm kiếm theo Email, Họ tên hoặc Số điện thoại người dùng</summary>
        public string? Search { get; set; }

        /// <summary>Lọc theo ID gói cụ thể</summary>
        public Guid? PlanId { get; set; }

        /// <summary>Lọc theo slug gói (basic, standard, premium)</summary>
        public string? PlanSlug { get; set; }

        /// <summary>Lọc theo trạng thái gói (active, expired, cancelled)</summary>
        public string? Status { get; set; }

        /// <summary>Lọc theo chu kỳ (monthly, yearly, free)</summary>
        public string? BillingCycle { get; set; }

        /// <summary>Lọc những người dùng có gói sẽ hết hạn trong số ngày tới (ví dụ: 3, 7 ngày)</summary>
        public int? ExpiringInDays { get; set; }
    }

    /// <summary>
    /// Thông tin chi tiết một đăng ký gói của người dùng hiển thị cho Admin
    /// </summary>
    public class AdminUserSubscriptionResponse
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string UserFullName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string? UserPhoneNumber { get; set; }

        public Guid PlanId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public string PlanSlug { get; set; } = string.Empty;
        public string BillingCycle { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal AmountPaid { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public string? PaymentReference { get; set; }
        public DateTimeOffset StartsAt { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
        public int? RemainingDays { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }

        // Snapshot Quotas
        public int SnapshotMaxAiQuestionsPerMonth { get; set; }
        public int SnapshotChatHistoryRetentionDays { get; set; }
        public int SnapshotMaxOcrScansPerMonth { get; set; }
        public bool SnapshotCanExportPdf { get; set; }
        public bool SnapshotCanExportZip { get; set; }
        public int SnapshotBookingDiscountPercent { get; set; }
        public int SnapshotFreeConsultationsPerYear { get; set; }

        // Mức sử dụng thực tế trong tháng hiện tại
        public int? CurrentMonthAiQuestionsUsed { get; set; }
        public int? CurrentMonthOcrScansUsed { get; set; }
    }

    /// <summary>
    /// Thống kê doanh thu và tỷ lệ sử dụng các gói dịch vụ
    /// </summary>
    public class SubscriptionAnalyticsResponse
    {
        public decimal TotalRevenue { get; set; }
        public int TotalActiveSubscriptions { get; set; }
        public int TotalPaidTransactions { get; set; }

        /// <summary>Doanh thu phân bổ theo từng gói dịch vụ</summary>
        public List<PlanRevenueSummaryDto> RevenueByPlan { get; set; } = new();

        /// <summary>Doanh thu phân bổ theo chu kỳ (tháng vs năm)</summary>
        public List<CycleRevenueSummaryDto> RevenueByCycle { get; set; } = new();

        /// <summary>Phân bổ số lượng người dùng đang sử dụng từng gói (Active)</summary>
        public List<UserPlanDistributionDto> UserDistribution { get; set; } = new();

        /// <summary>Xu hướng doanh thu theo các tháng</summary>
        public List<MonthlyRevenueTrendDto> MonthlyTrends { get; set; } = new();
    }

    public class PlanRevenueSummaryDto
    {
        public Guid PlanId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public string PlanSlug { get; set; } = string.Empty;
        public decimal TotalRevenue { get; set; }
        public int PurchaseCount { get; set; }
    }

    public class CycleRevenueSummaryDto
    {
        public string BillingCycle { get; set; } = string.Empty;
        public decimal TotalRevenue { get; set; }
        public int PurchaseCount { get; set; }
    }

    public class UserPlanDistributionDto
    {
        public Guid PlanId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public string PlanSlug { get; set; } = string.Empty;
        public int ActiveUserCount { get; set; }
        public double Percentage { get; set; }
    }

    public class MonthlyRevenueTrendDto
    {
        public string YearMonth { get; set; } = string.Empty; // format: "2026-11"
        public decimal Revenue { get; set; }
        public int PurchaseCount { get; set; }
    }

    /// <summary>
    /// Yêu cầu tặng / gán gói cho một người dùng cụ thể
    /// </summary>
    public class GiftSubscriptionRequest
    {
        [Required(ErrorMessage = "UserId người nhận là bắt buộc.")]
        public Guid UserId { get; set; }

        [Required(ErrorMessage = "PlanId gói muốn tặng là bắt buộc.")]
        public Guid PlanId { get; set; }

        /// <summary>Thời hạn tặng tính theo ngày (mặc định 30 ngày)</summary>
        [Range(1, 3650, ErrorMessage = "Thời hạn tặng phải từ 1 đến 3650 ngày.")]
        public int DurationDays { get; set; } = 30;

        /// <summary>Lý do tặng (hiển thị trong thông báo cho user)</summary>
        [MaxLength(500, ErrorMessage = "Lý do không được vượt quá 500 ký tự.")]
        public string Reason { get; set; } = "Quà tặng tri ân từ Ban quản trị TaxKeepVN";
    }

    /// <summary>
    /// Yêu cầu gia hạn thêm ngày sử dụng cho gói hiện tại của user
    /// </summary>
    public class ExtendSubscriptionRequest
    {
        /// <summary>Số ngày muốn gia hạn thêm</summary>
        [Range(1, 3650, ErrorMessage = "Số ngày gia hạn phải từ 1 đến 3650 ngày.")]
        public int ExtensionDays { get; set; } = 30;

        /// <summary>Lý do gia hạn</summary>
        [MaxLength(500, ErrorMessage = "Lý do không được vượt quá 500 ký tự.")]
        public string Reason { get; set; } = "Gia hạn dịch vụ từ Ban quản trị TaxKeepVN";
    }

    /// <summary>
    /// Yêu cầu hoàn tiền gói đăng ký vào Ví cá nhân của người dùng
    /// </summary>
    public class RefundSubscriptionRequest
    {
        /// <summary>
        /// Số tiền muốn hoàn lại (VNĐ).
        /// Để trống hoặc null sẽ tự động hoàn 100% số tiền người dùng đã thanh toán gói đó.
        /// </summary>
        public decimal? RefundAmount { get; set; }

        /// <summary>Lý do hoàn tiền (bắt buộc để ghi sổ cái giao dịch và thông báo cho user)</summary>
        [Required(ErrorMessage = "Vui lòng nhập lý do hoàn tiền.")]
        [MaxLength(500, ErrorMessage = "Lý do hoàn tiền không vượt quá 500 ký tự.")]
        public string Reason { get; set; } = string.Empty;
    }
}

