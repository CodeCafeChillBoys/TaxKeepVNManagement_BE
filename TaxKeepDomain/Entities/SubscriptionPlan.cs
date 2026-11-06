using System;
using System.Collections.Generic;

namespace TaxKeepVN.Domain.Entities
{
    /// <summary>
    /// Bảng cấu hình các gói dịch vụ (Basic / Standard / Premium).
    /// ZERO-HARDCODE: Mọi quota và giá do Admin cấu hình qua API.
    /// </summary>
    public class SubscriptionPlan
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>Tên hiển thị: "Basic", "Standard", "Premium"</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Slug không dấu: "basic", "standard", "premium"</summary>
        public string Slug { get; set; } = string.Empty;

        /// <summary>Mô tả ngắn gọn hiển thị cho người dùng</summary>
        public string? Description { get; set; }

        /// <summary>Giá theo tháng (VNĐ). 0 = miễn phí</summary>
        public decimal PriceMonthly { get; set; } = 0m;

        /// <summary>Giá theo năm (VNĐ). 0 = miễn phí</summary>
        public decimal PriceYearly { get; set; } = 0m;

        /// <summary>Số câu hỏi AI / tháng. -1 = không giới hạn</summary>
        public int MaxAiQuestionsPerMonth { get; set; } = 20;

        /// <summary>Số ngày lưu lịch sử chat AI. -1 = vĩnh viễn</summary>
        public int ChatHistoryRetentionDays { get; set; } = 30;

        /// <summary>Số lượt OCR chứng từ / tháng. -1 = không giới hạn</summary>
        public int MaxOcrScansPerMonth { get; set; } = 5;

        /// <summary>Số nguồn thu nhập tối đa. -1 = không giới hạn</summary>
        public int MaxIncomeSources { get; set; } = 1;

        /// <summary>Số người phụ thuộc tối đa. -1 = không giới hạn</summary>
        public int MaxDependents { get; set; } = 3;

        /// <summary>Có thể xuất PDF quyết toán không</summary>
        public bool CanExportPdf { get; set; } = false;

        /// <summary>Có thể xuất ZIP chứng từ không</summary>
        public bool CanExportZip { get; set; } = false;

        /// <summary>Phần trăm giảm giá khi đặt lịch tư vấn chuyên gia (0 = không giảm)</summary>
        public int BookingDiscountPercent { get; set; } = 0;

        /// <summary>Số ca tư vấn miễn phí / năm (0 = không có)</summary>
        public int FreeConsultationsPerYear { get; set; } = 0;

        /// <summary>Thứ tự sắp xếp hiển thị</summary>
        public int DisplayOrder { get; set; } = 0;

        /// <summary>Gói có đang được kích hoạt cho phép mua không</summary>
        public bool IsActive { get; set; } = true;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        // Navigation
        public ICollection<UserSubscription> UserSubscriptions { get; set; } = new List<UserSubscription>();
    }
}

