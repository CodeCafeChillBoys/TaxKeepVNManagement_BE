using System;
using System.ComponentModel.DataAnnotations;

namespace TaxKeepVN.Application.DTOs.Subscriptions
{
    public class PurchaseSubscriptionRequest
    {
        [Required(ErrorMessage = "Vui lòng chọn gói dịch vụ")]
        public Guid PlanId { get; set; }

        /// <summary>monthly | yearly</summary>
        [Required(ErrorMessage = "Vui lòng chọn chu kỳ thanh toán (monthly hoặc yearly)")]
        public string BillingCycle { get; set; } = "monthly";

        /// <summary>WALLET | VNPAY | VIETQR</summary>
        [Required(ErrorMessage = "Vui lòng chọn phương thức thanh toán (WALLET, VNPAY, VIETQR)")]
        public string PaymentMethod { get; set; } = "WALLET";

        /// <summary>URL chuyển hướng khi thanh toán xong trên cổng VNPay/VietQR (Frontend cung cấp)</summary>
        public string? ReturnUrl { get; set; }

        /// <summary>URL chuyển hướng khi người dùng hủy trên cổng thanh toán</summary>
        public string? CancelUrl { get; set; }
    }

    public class SubscriptionPurchaseResponse
    {
        public Guid? SubscriptionId { get; set; }
        public long OrderCode { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
        public string? CheckoutUrl { get; set; }
        public string? QrCode { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class UserSubscriptionResponse
    {
        public Guid Id { get; set; }
        public Guid PlanId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public string PlanSlug { get; set; } = string.Empty;
        public string BillingCycle { get; set; } = "free";
        public string Status { get; set; } = "active";
        public decimal AmountPaid { get; set; }
        public string PaymentMethod { get; set; } = "FREE";
        public DateTimeOffset StartsAt { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
        public int? RemainingDays { get; set; }

        // Snapshot Quotas
        public int MaxAiQuestionsPerMonth { get; set; }
        public int ChatHistoryRetentionDays { get; set; }
        public int MaxOcrScansPerMonth { get; set; }
        public bool CanExportPdf { get; set; }
        public bool CanExportZip { get; set; }
        public int BookingDiscountPercent { get; set; }
        public int FreeConsultationsPerYear { get; set; }
    }

    public class SubscriptionQuotaStatusResponse
    {
        public string PlanName { get; set; } = "Basic";
        public string PlanSlug { get; set; } = "basic";
        public string BillingCycle { get; set; } = "free";
        public DateTimeOffset? ExpiresAt { get; set; }
        public int? RemainingDays { get; set; }

        // AI Questions
        public int AiQuestionsUsed { get; set; }
        public int MaxAiQuestionsPerMonth { get; set; }
        public int? AiQuestionsRemaining { get; set; } // null if unlimited (-1)
        public bool IsAiQuestionsUnlimited => MaxAiQuestionsPerMonth == -1;

        // OCR Scans
        public int OcrScansUsed { get; set; }
        public int MaxOcrScansPerMonth { get; set; }
        public int? OcrScansRemaining { get; set; } // null if unlimited (-1)
        public bool IsOcrScansUnlimited => MaxOcrScansPerMonth == -1;

        // Features
        public bool CanExportPdf { get; set; }
        public bool CanExportZip { get; set; }
        public int BookingDiscountPercent { get; set; }
        public int FreeConsultationsPerYear { get; set; }
        public int ChatHistoryRetentionDays { get; set; }
    }
}

