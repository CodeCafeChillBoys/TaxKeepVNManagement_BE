using System;
using System.ComponentModel.DataAnnotations;

namespace TaxKeepVN.Application.DTOs.Subscriptions
{
    public class SubscriptionPlanResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal PriceMonthly { get; set; }
        public decimal PriceYearly { get; set; }
        public int MaxAiQuestionsPerMonth { get; set; }
        public int ChatHistoryRetentionDays { get; set; }
        public int MaxOcrScansPerMonth { get; set; }
        public bool CanExportPdf { get; set; }
        public bool CanExportZip { get; set; }
        public int BookingDiscountPercent { get; set; }
        public int FreeConsultationsPerYear { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    public class CreateSubscriptionPlanRequest
    {
        [Required(ErrorMessage = "Tên gói không được để trống")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Slug không được để trống")]
        [MaxLength(50)]
        public string Slug { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Giá tháng phải >= 0")]
        public decimal PriceMonthly { get; set; } = 0m;

        [Range(0, double.MaxValue, ErrorMessage = "Giá năm phải >= 0")]
        public decimal PriceYearly { get; set; } = 0m;

        public int MaxAiQuestionsPerMonth { get; set; } = 20;
        public int ChatHistoryRetentionDays { get; set; } = 30;
        public int MaxOcrScansPerMonth { get; set; } = 5;
        public bool CanExportPdf { get; set; } = false;
        public bool CanExportZip { get; set; } = false;
        public int BookingDiscountPercent { get; set; } = 0;
        public int FreeConsultationsPerYear { get; set; } = 0;
        public int DisplayOrder { get; set; } = 0;
        public bool IsActive { get; set; } = true;
    }

    public class UpdateSubscriptionPlanRequest
    {
        [MaxLength(100)]
        public string? Name { get; set; }

        public string? Description { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Giá tháng phải >= 0")]
        public decimal? PriceMonthly { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Giá năm phải >= 0")]
        public decimal? PriceYearly { get; set; }

        public int? MaxAiQuestionsPerMonth { get; set; }
        public int? ChatHistoryRetentionDays { get; set; }
        public int? MaxOcrScansPerMonth { get; set; }
        public bool? CanExportPdf { get; set; }
        public bool? CanExportZip { get; set; }
        public int? BookingDiscountPercent { get; set; }
        public int? FreeConsultationsPerYear { get; set; }
        public int? DisplayOrder { get; set; }
        public bool? IsActive { get; set; }
    }
}

