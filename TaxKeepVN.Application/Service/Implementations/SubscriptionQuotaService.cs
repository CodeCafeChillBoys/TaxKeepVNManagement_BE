using System;
using System.Linq;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Subscriptions;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    public class SubscriptionQuotaService : ISubscriptionQuotaService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IUserSubscriptionService _userSubService;

        public SubscriptionQuotaService(
            IUnitOfWork unitOfWork,
            IUserSubscriptionService userSubService)
        {
            _unitOfWork = unitOfWork;
            _userSubService = userSubService;
        }

        public async Task<SubscriptionQuotaStatusResponse> GetQuotaStatusAsync(Guid userId)
        {
            var sub = await _userSubService.GetOrCreateActiveSubscriptionEntityAsync(userId);
            var currentMonth = DateTime.UtcNow.ToString("yyyy-MM");
            var usage = await GetOrCreateCurrentUsageAsync(userId, currentMonth);

            int? aiRemaining = sub.SnapshotMaxAiQuestionsPerMonth == -1 ? null : Math.Max(0, sub.SnapshotMaxAiQuestionsPerMonth - usage.AiQuestionsUsed);
            int? ocrRemaining = sub.SnapshotMaxOcrScansPerMonth == -1 ? null : Math.Max(0, sub.SnapshotMaxOcrScansPerMonth - usage.OcrScansUsed);

            int? remainingDays = null;
            if (sub.ExpiresAt.HasValue)
            {
                var diff = (sub.ExpiresAt.Value - DateTimeOffset.UtcNow).TotalDays;
                remainingDays = Math.Max(0, (int)Math.Ceiling(diff));
            }

            return new SubscriptionQuotaStatusResponse
            {
                PlanName = sub.Plan?.Name ?? "Basic",
                PlanSlug = sub.Plan?.Slug ?? "basic",
                BillingCycle = sub.BillingCycle,
                ExpiresAt = sub.ExpiresAt,
                RemainingDays = remainingDays,
                AiQuestionsUsed = usage.AiQuestionsUsed,
                MaxAiQuestionsPerMonth = sub.SnapshotMaxAiQuestionsPerMonth,
                AiQuestionsRemaining = aiRemaining,
                OcrScansUsed = usage.OcrScansUsed,
                MaxOcrScansPerMonth = sub.SnapshotMaxOcrScansPerMonth,
                OcrScansRemaining = ocrRemaining,
                CanExportPdf = sub.SnapshotCanExportPdf,
                CanExportZip = sub.SnapshotCanExportZip,
                BookingDiscountPercent = sub.SnapshotBookingDiscountPercent,
                FreeConsultationsPerYear = sub.SnapshotFreeConsultationsPerYear,
                ChatHistoryRetentionDays = sub.SnapshotChatHistoryRetentionDays
            };
        }

        public async Task CheckAndConsumeAiQuestionAsync(Guid userId)
        {
            var sub = await _userSubService.GetOrCreateActiveSubscriptionEntityAsync(userId);

            // -1 nghĩa là không giới hạn
            if (sub.SnapshotMaxAiQuestionsPerMonth == -1) return;

            var currentMonth = DateTime.UtcNow.ToString("yyyy-MM");
            var usage = await GetOrCreateCurrentUsageAsync(userId, currentMonth);

            if (usage.AiQuestionsUsed >= sub.SnapshotMaxAiQuestionsPerMonth)
            {
                throw new BadRequestException(
                    "AI_QUESTION_QUOTA_EXCEEDED",
                    $"Bạn đã sử dụng hết hạn mức {sub.SnapshotMaxAiQuestionsPerMonth} câu hỏi AI trong tháng này của gói {sub.Plan?.Name ?? "hiện tại"}. Vui lòng nâng cấp gói dịch vụ để tiếp tục hỏi đáp.");
            }

            usage.AiQuestionsUsed += 1;
            usage.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<SubscriptionQuotaUsage>().Update(usage);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task CheckAndConsumeOcrScanAsync(Guid userId)
        {
            var sub = await _userSubService.GetOrCreateActiveSubscriptionEntityAsync(userId);

            // -1 nghĩa là không giới hạn
            if (sub.SnapshotMaxOcrScansPerMonth == -1) return;

            var currentMonth = DateTime.UtcNow.ToString("yyyy-MM");
            var usage = await GetOrCreateCurrentUsageAsync(userId, currentMonth);

            if (usage.OcrScansUsed >= sub.SnapshotMaxOcrScansPerMonth)
            {
                throw new BadRequestException(
                    "OCR_QUOTA_EXCEEDED",
                    $"Bạn đã sử dụng hết hạn mức {sub.SnapshotMaxOcrScansPerMonth} lượt quét OCR trong tháng này của gói {sub.Plan?.Name ?? "hiện tại"}. Vui lòng nâng cấp gói dịch vụ để quét thêm chứng từ.");
            }

            usage.OcrScansUsed += 1;
            usage.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<SubscriptionQuotaUsage>().Update(usage);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task CheckCanExportPdfAsync(Guid userId)
        {
            var sub = await _userSubService.GetOrCreateActiveSubscriptionEntityAsync(userId);
            if (!sub.SnapshotCanExportPdf)
            {
                throw new BadRequestException(
                    "EXPORT_PDF_NOT_ALLOWED",
                    $"Gói {sub.Plan?.Name ?? "Basic"} chỉ hỗ trợ xem kết quả trên giao diện, không hỗ trợ xuất file PDF Quyết toán Mẫu 02/QTT-TNCN. Vui lòng nâng cấp lên gói Standard hoặc Premium để mở khóa tính năng này.");
            }
        }

        public async Task CheckCanExportZipAsync(Guid userId)
        {
            var sub = await _userSubService.GetOrCreateActiveSubscriptionEntityAsync(userId);
            if (!sub.SnapshotCanExportZip)
            {
                throw new BadRequestException(
                    "EXPORT_ZIP_NOT_ALLOWED",
                    $"Tính năng tải trọn bộ file ZIP chứng từ quyết toán chỉ dành riêng cho gói Premium. Vui lòng nâng cấp gói để sử dụng.");
            }
        }

        public async Task<int> GetBookingDiscountPercentAsync(Guid userId)
        {
            var sub = await _userSubService.GetOrCreateActiveSubscriptionEntityAsync(userId);
            return sub.SnapshotBookingDiscountPercent;
        }

        private async Task<SubscriptionQuotaUsage> GetOrCreateCurrentUsageAsync(Guid userId, string periodMonth)
        {
            var repo = _unitOfWork.Repository<SubscriptionQuotaUsage>();
            var usages = await repo.FindAsync(u => u.UserId == userId && u.PeriodMonth == periodMonth);
            var usage = usages.FirstOrDefault();

            if (usage != null) return usage;

            var newUsage = new SubscriptionQuotaUsage
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PeriodMonth = periodMonth,
                AiQuestionsUsed = 0,
                OcrScansUsed = 0,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            await repo.AddAsync(newUsage);
            await _unitOfWork.SaveChangesAsync();
            return newUsage;
        }
    }
}

