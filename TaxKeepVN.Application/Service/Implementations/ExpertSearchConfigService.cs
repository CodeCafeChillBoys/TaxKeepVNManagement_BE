using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using TaxKeepVN.Application.DTOs.SearchConfigs;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    public class ExpertSearchConfigService : IExpertSearchConfigService
    {
        private const string CacheKey = "taxkeep:expert_search:config";
        private readonly IUnitOfWork _unitOfWork;
        private readonly IRedisCacheService? _redisCache;

        public ExpertSearchConfigService(IUnitOfWork unitOfWork, IRedisCacheService? redisCache = null)
        {
            _unitOfWork = unitOfWork;
            _redisCache = redisCache;
        }

        public async Task<ExpertSearchCriteriaConfigDto> GetConfigAsync()
        {
            if (_redisCache != null)
            {
                var cached = await _redisCache.GetAsync<ExpertSearchCriteriaConfigDto>(CacheKey);
                if (cached != null)
                    return cached;
            }

            var repo = _unitOfWork.Repository<ExpertSearchConfiguration>();
            var allConfigs = await repo.GetAllAsync();
            var entity = allConfigs.OrderBy(c => c.Id).FirstOrDefault();

            if (entity == null)
            {
                return new ExpertSearchCriteriaConfigDto();
            }

            var dto = MapToDto(entity);
            if (_redisCache != null)
            {
                await _redisCache.SetAsync(CacheKey, dto, TimeSpan.FromHours(24));
            }

            return dto;
        }

        public async Task<ExpertSearchCriteriaConfigDto> UpdateConfigAsync(ExpertSearchCriteriaConfigDto newConfig)
        {
            var repo = _unitOfWork.Repository<ExpertSearchConfiguration>();
            var allConfigs = await repo.GetAllAsync();
            var entity = allConfigs.OrderBy(c => c.Id).FirstOrDefault();

            if (entity == null)
            {
                entity = new ExpertSearchConfiguration();
                MapFromDto(newConfig, entity);
                entity.UpdatedAt = DateTimeOffset.UtcNow;
                await repo.AddAsync(entity);
            }
            else
            {
                MapFromDto(newConfig, entity);
                entity.UpdatedAt = DateTimeOffset.UtcNow;
                repo.Update(entity);
            }

            await _unitOfWork.SaveChangesAsync();
            if (_redisCache != null) await _redisCache.RemoveAsync(CacheKey);

            return newConfig;
        }

        public async Task<ExpertSearchCriteriaConfigDto> ResetToDefaultsAsync()
        {
            return await UpdateConfigAsync(new ExpertSearchCriteriaConfigDto());
        }

        private static ExpertSearchCriteriaConfigDto MapToDto(ExpertSearchConfiguration entity)
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            List<int> featuredSpecs;
            try
            {
                featuredSpecs = string.IsNullOrWhiteSpace(entity.FeaturedSpecializationIds)
                    ? new List<int>()
                    : JsonSerializer.Deserialize<List<int>>(entity.FeaturedSpecializationIds, options) ?? new List<int>();
            }
            catch
            {
                featuredSpecs = new List<int>();
            }

            List<ExpertSortBy> allowedSorts;
            try
            {
                allowedSorts = string.IsNullOrWhiteSpace(entity.AllowedSortOptions)
                    ? new List<ExpertSortBy>()
                    : JsonSerializer.Deserialize<List<ExpertSortBy>>(entity.AllowedSortOptions, options) ?? new List<ExpertSortBy>();
            }
            catch
            {
                allowedSorts = new List<ExpertSortBy>
                {
                    ExpertSortBy.Recommended,
                    ExpertSortBy.RatingDesc,
                    ExpertSortBy.CompletedSessionsDesc,
                    ExpertSortBy.FeeAsc,
                    ExpertSortBy.FeeDesc,
                    ExpertSortBy.ExperienceDesc
                };
            }

            return new ExpertSearchCriteriaConfigDto
            {
                DefaultSortBy = entity.DefaultSortBy,
                RequireVerifiedCertificate = entity.RequireVerifiedCertificate,
                FeaturedSpecializationIds = featuredSpecs,
                AllowedSortOptions = allowedSorts,
                SearchableFields = new SearchableFieldsConfig
                {
                    SearchInFullName = entity.SearchInFullName,
                    SearchInJobTitle = entity.SearchInJobTitle,
                    SearchInCompanyName = entity.SearchInCompanyName,
                    SearchInBio = entity.SearchInBio,
                    SearchInSpecializations = entity.SearchInSpecializations,
                    SearchInCertificates = entity.SearchInCertificates
                },
                RankingWeights = new RankingWeightsConfig
                {
                    RatingWeight = entity.RatingWeight,
                    CompletedConsultationsWeight = entity.CompletedConsultationsWeight,
                    ReviewsCountWeight = entity.ReviewsCountWeight,
                    SlotAvailabilityWeight = entity.SlotAvailabilityWeight,
                    VerifiedCertificateWeight = entity.VerifiedCertificateWeight
                }
            };
        }

        private static void MapFromDto(ExpertSearchCriteriaConfigDto dto, ExpertSearchConfiguration entity)
        {
            entity.DefaultSortBy = dto.DefaultSortBy;
            entity.RequireVerifiedCertificate = dto.RequireVerifiedCertificate;
            entity.FeaturedSpecializationIds = JsonSerializer.Serialize(dto.FeaturedSpecializationIds ?? new List<int>());
            entity.AllowedSortOptions = JsonSerializer.Serialize(dto.AllowedSortOptions ?? new List<ExpertSortBy>());

            entity.SearchInFullName = dto.SearchableFields.SearchInFullName;
            entity.SearchInJobTitle = dto.SearchableFields.SearchInJobTitle;
            entity.SearchInCompanyName = dto.SearchableFields.SearchInCompanyName;
            entity.SearchInBio = dto.SearchableFields.SearchInBio;
            entity.SearchInSpecializations = dto.SearchableFields.SearchInSpecializations;
            entity.SearchInCertificates = dto.SearchableFields.SearchInCertificates;

            entity.RatingWeight = dto.RankingWeights.RatingWeight;
            entity.CompletedConsultationsWeight = dto.RankingWeights.CompletedConsultationsWeight;
            entity.ReviewsCountWeight = dto.RankingWeights.ReviewsCountWeight;
            entity.SlotAvailabilityWeight = dto.RankingWeights.SlotAvailabilityWeight;
            entity.VerifiedCertificateWeight = dto.RankingWeights.VerifiedCertificateWeight;
        }
    }
}
