using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Subscriptions;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    public class SubscriptionPlanService : ISubscriptionPlanService
    {
        private readonly IUnitOfWork _unitOfWork;

        public SubscriptionPlanService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<List<SubscriptionPlanResponse>> GetAllActivePlansAsync()
        {
            await SeedDefaultPlansIfEmptyAsync();

            var plans = await _unitOfWork.Repository<SubscriptionPlan>().FindAsync(p => p.IsActive);
            return plans.OrderBy(p => p.DisplayOrder).Select(MapToResponse).ToList();
        }

        public async Task<List<SubscriptionPlanResponse>> GetAllPlansForAdminAsync()
        {
            await SeedDefaultPlansIfEmptyAsync();

            var plans = await _unitOfWork.Repository<SubscriptionPlan>().GetAllAsync();
            return plans.OrderBy(p => p.DisplayOrder).Select(MapToResponse).ToList();
        }

        public async Task<SubscriptionPlanResponse> GetPlanByIdAsync(Guid id)
        {
            var plan = await _unitOfWork.Repository<SubscriptionPlan>().GetByIdAsync(id);
            if (plan == null)
            {
                throw new NotFoundException($"Không tìm thấy gói dịch vụ với Id = {id}");
            }
            return MapToResponse(plan);
        }

        public async Task<SubscriptionPlanResponse> CreatePlanAsync(CreateSubscriptionPlanRequest request)
        {
            var repo = _unitOfWork.Repository<SubscriptionPlan>();
            var existingSlug = await repo.FindAsync(p => p.Slug.ToLower() == request.Slug.ToLower());
            if (existingSlug.Any())
            {
                throw new ConflictException("SLUG_ALREADY_EXISTS", $"Slug '{request.Slug}' đã tồn tại trong hệ thống.");
            }

            var plan = new SubscriptionPlan
            {
                Id = Guid.NewGuid(),
                Name = request.Name.Trim(),
                Slug = request.Slug.Trim().ToLower(),
                Description = request.Description,
                PriceMonthly = request.PriceMonthly,
                PriceYearly = request.PriceYearly,
                MaxAiQuestionsPerMonth = request.MaxAiQuestionsPerMonth,
                ChatHistoryRetentionDays = request.ChatHistoryRetentionDays,
                MaxOcrScansPerMonth = request.MaxOcrScansPerMonth,
                CanExportPdf = request.CanExportPdf,
                CanExportZip = request.CanExportZip,
                BookingDiscountPercent = request.BookingDiscountPercent,
                FreeConsultationsPerYear = request.FreeConsultationsPerYear,
                DisplayOrder = request.DisplayOrder,
                IsActive = request.IsActive,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            await repo.AddAsync(plan);
            await _unitOfWork.SaveChangesAsync();
            return MapToResponse(plan);
        }

        public async Task<SubscriptionPlanResponse> UpdatePlanAsync(Guid id, UpdateSubscriptionPlanRequest request)
        {
            var repo = _unitOfWork.Repository<SubscriptionPlan>();
            var plan = await repo.GetByIdAsync(id);
            if (plan == null)
            {
                throw new NotFoundException($"Không tìm thấy gói dịch vụ với Id = {id}");
            }

            if (!string.IsNullOrWhiteSpace(request.Name)) plan.Name = request.Name.Trim();
            if (request.Description != null) plan.Description = request.Description;
            if (request.PriceMonthly.HasValue) plan.PriceMonthly = request.PriceMonthly.Value;
            if (request.PriceYearly.HasValue) plan.PriceYearly = request.PriceYearly.Value;
            if (request.MaxAiQuestionsPerMonth.HasValue) plan.MaxAiQuestionsPerMonth = request.MaxAiQuestionsPerMonth.Value;
            if (request.ChatHistoryRetentionDays.HasValue) plan.ChatHistoryRetentionDays = request.ChatHistoryRetentionDays.Value;
            if (request.MaxOcrScansPerMonth.HasValue) plan.MaxOcrScansPerMonth = request.MaxOcrScansPerMonth.Value;
            if (request.CanExportPdf.HasValue) plan.CanExportPdf = request.CanExportPdf.Value;
            if (request.CanExportZip.HasValue) plan.CanExportZip = request.CanExportZip.Value;
            if (request.BookingDiscountPercent.HasValue) plan.BookingDiscountPercent = request.BookingDiscountPercent.Value;
            if (request.FreeConsultationsPerYear.HasValue) plan.FreeConsultationsPerYear = request.FreeConsultationsPerYear.Value;
            if (request.DisplayOrder.HasValue) plan.DisplayOrder = request.DisplayOrder.Value;
            if (request.IsActive.HasValue) plan.IsActive = request.IsActive.Value;

            plan.UpdatedAt = DateTimeOffset.UtcNow;
            repo.Update(plan);
            await _unitOfWork.SaveChangesAsync();

            return MapToResponse(plan);
        }

        public async Task<bool> DeletePlanAsync(Guid id)
        {
            var repo = _unitOfWork.Repository<SubscriptionPlan>();
            var plan = await repo.GetByIdAsync(id);
            if (plan == null) throw new NotFoundException($"Không tìm thấy gói dịch vụ với Id = {id}");

            if (plan.Slug == "basic")
            {
                throw new BadRequestException("CANNOT_DELETE_BASIC_PLAN", "Gói Basic là gói mặc định của hệ thống, không được xóa.");
            }

            // Soft-delete bằng cách đặt IsActive = false
            plan.IsActive = false;
            plan.UpdatedAt = DateTimeOffset.UtcNow;
            repo.Update(plan);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task SeedDefaultPlansIfEmptyAsync()
        {
            var repo = _unitOfWork.Repository<SubscriptionPlan>();
            var all = await repo.GetAllAsync();
            if (all.Any()) return;

            var defaultPlans = new List<SubscriptionPlan>
            {
                new SubscriptionPlan
                {
                    Id = Guid.NewGuid(),
                    Name = "Gói Basic",
                    Slug = "basic",
                    Description = "Dành cho người dùng mới trải nghiệm, tự tra cứu cơ bản.",
                    PriceMonthly = 0m,
                    PriceYearly = 0m,
                    MaxAiQuestionsPerMonth = 20,
                    ChatHistoryRetentionDays = 30,
                    MaxOcrScansPerMonth = 5,
                    CanExportPdf = false,
                    CanExportZip = false,
                    BookingDiscountPercent = 0,
                    FreeConsultationsPerYear = 0,
                    DisplayOrder = 1,
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                },
                new SubscriptionPlan
                {
                    Id = Guid.NewGuid(),
                    Name = "Gói Standard",
                    Slug = "standard",
                    Description = "Dành cho người lao động có 1 nguồn thu nhập, ít chứng từ.",
                    PriceMonthly = 49000m,
                    PriceYearly = 490000m,
                    MaxAiQuestionsPerMonth = 100,
                    ChatHistoryRetentionDays = 365,
                    MaxOcrScansPerMonth = 30,
                    CanExportPdf = true,
                    CanExportZip = false,
                    BookingDiscountPercent = 0,
                    FreeConsultationsPerYear = 0,
                    DisplayOrder = 2,
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                },
                new SubscriptionPlan
                {
                    Id = Guid.NewGuid(),
                    Name = "Gói Premium",
                    Slug = "premium",
                    Description = "Dành cho người có nhiều nguồn thu nhập, nhiều chứng từ giảm trừ, kế toán.",
                    PriceMonthly = 99000m,
                    PriceYearly = 990000m,
                    MaxAiQuestionsPerMonth = -1, // Không giới hạn
                    ChatHistoryRetentionDays = -1, // Vĩnh viễn
                    MaxOcrScansPerMonth = 100,
                    CanExportPdf = true,
                    CanExportZip = true,
                    BookingDiscountPercent = 10,
                    FreeConsultationsPerYear = 1,
                    DisplayOrder = 3,
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                }
            };

            foreach (var p in defaultPlans)
            {
                await repo.AddAsync(p);
            }
            await _unitOfWork.SaveChangesAsync();
        }

        private static SubscriptionPlanResponse MapToResponse(SubscriptionPlan plan)
        {
            return new SubscriptionPlanResponse
            {
                Id = plan.Id,
                Name = plan.Name,
                Slug = plan.Slug,
                Description = plan.Description,
                PriceMonthly = plan.PriceMonthly,
                PriceYearly = plan.PriceYearly,
                MaxAiQuestionsPerMonth = plan.MaxAiQuestionsPerMonth,
                ChatHistoryRetentionDays = plan.ChatHistoryRetentionDays,
                MaxOcrScansPerMonth = plan.MaxOcrScansPerMonth,
                CanExportPdf = plan.CanExportPdf,
                CanExportZip = plan.CanExportZip,
                BookingDiscountPercent = plan.BookingDiscountPercent,
                FreeConsultationsPerYear = plan.FreeConsultationsPerYear,
                DisplayOrder = plan.DisplayOrder,
                IsActive = plan.IsActive,
                CreatedAt = plan.CreatedAt,
                UpdatedAt = plan.UpdatedAt
            };
        }
    }
}

