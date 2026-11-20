using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TaxKeepVN.Application.DTOs.MedicalClaims;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    public class BhytCatalogService : IBhytCatalogService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<BhytCatalogService> _logger;

        public BhytCatalogService(IUnitOfWork unitOfWork, ILogger<BhytCatalogService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<List<BhytCatalogItemDto>> GetCatalogAsync(string? type, string? search, bool? isActive)
        {
            var repo = _unitOfWork.Repository<BhytCoverageCatalog>();
            var items = await repo.FindAsync(c =>
                (string.IsNullOrEmpty(type) || c.CatalogType == type) &&
                (!isActive.HasValue || c.IsActive == isActive.Value));

            var result = items.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var normSearch = Normalize(search);
                result = result.Where(i =>
                    i.NormalizedName.Contains(normSearch) ||
                    (!string.IsNullOrEmpty(i.Code) && i.Code.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(i.ActiveIngredient) && Normalize(i.ActiveIngredient).Contains(normSearch)));
            }

            return result.OrderBy(i => i.Name).Select(MapToDto).ToList();
        }

        public async Task<BhytCatalogItemDto> GetByIdAsync(Guid id)
        {
            var repo = _unitOfWork.Repository<BhytCoverageCatalog>();
            var item = await repo.GetByIdAsync(id);
            if (item == null)
            {
                throw new NotFoundException("Không tìm thấy mục danh mục BHYT.");
            }
            return MapToDto(item);
        }

        public async Task<BhytCatalogItemDto> CreateItemAsync(CreateBhytCatalogItemRequest request)
        {
            var repo = _unitOfWork.Repository<BhytCoverageCatalog>();

            var entity = new BhytCoverageCatalog
            {
                Id = Guid.NewGuid(),
                CatalogType = request.CatalogType.Trim().ToUpperInvariant(),
                Code = request.Code?.Trim(),
                Name = request.Name.Trim(),
                NormalizedName = Normalize(request.Name),
                ActiveIngredient = request.ActiveIngredient?.Trim(),
                LegalDocumentRef = request.LegalDocumentRef?.Trim(),
                EffectiveFrom = request.EffectiveFrom,
                EffectiveTo = request.EffectiveTo,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await repo.AddAsync(entity);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created BHYT catalog item: Id={Id}, Code={Code}, Name={Name}", entity.Id, entity.Code, entity.Name);
            return MapToDto(entity);
        }

        public async Task<BhytCatalogItemDto> UpdateItemAsync(Guid id, UpdateBhytCatalogItemRequest request)
        {
            var repo = _unitOfWork.Repository<BhytCoverageCatalog>();
            var entity = await repo.GetByIdAsync(id);
            if (entity == null)
            {
                throw new NotFoundException("Không tìm thấy mục danh mục BHYT.");
            }

            entity.CatalogType = request.CatalogType.Trim().ToUpperInvariant();
            entity.Code = request.Code?.Trim();
            entity.Name = request.Name.Trim();
            entity.NormalizedName = Normalize(request.Name);
            entity.ActiveIngredient = request.ActiveIngredient?.Trim();
            entity.LegalDocumentRef = request.LegalDocumentRef?.Trim();
            entity.EffectiveFrom = request.EffectiveFrom;
            entity.EffectiveTo = request.EffectiveTo;
            entity.IsActive = request.IsActive;
            entity.UpdatedAt = DateTime.UtcNow;

            repo.Update(entity);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated BHYT catalog item: Id={Id}", entity.Id);
            return MapToDto(entity);
        }

        public async Task DeleteItemAsync(Guid id)
        {
            var repo = _unitOfWork.Repository<BhytCoverageCatalog>();
            var entity = await repo.GetByIdAsync(id);
            if (entity == null)
            {
                throw new NotFoundException("Không tìm thấy mục danh mục BHYT.");
            }

            repo.Remove(entity);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Deleted BHYT catalog item: Id={Id}", id);
        }

        private static BhytCatalogItemDto MapToDto(BhytCoverageCatalog entity)
        {
            return new BhytCatalogItemDto
            {
                Id = entity.Id,
                CatalogType = entity.CatalogType,
                Code = entity.Code,
                Name = entity.Name,
                NormalizedName = entity.NormalizedName,
                ActiveIngredient = entity.ActiveIngredient,
                LegalDocumentRef = entity.LegalDocumentRef,
                EffectiveFrom = entity.EffectiveFrom,
                EffectiveTo = entity.EffectiveTo,
                IsActive = entity.IsActive,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }

        private static string Normalize(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            var clean = stringBuilder.ToString().Normalize(NormalizationForm.FormC);
            clean = clean.Replace('đ', 'd').Replace('Đ', 'D');
            clean = Regex.Replace(clean, @"[^a-zA-Z0-9\s]", " ");
            clean = Regex.Replace(clean, @"\s+", " ").Trim().ToLowerInvariant();
            return clean;
        }
    }
}
