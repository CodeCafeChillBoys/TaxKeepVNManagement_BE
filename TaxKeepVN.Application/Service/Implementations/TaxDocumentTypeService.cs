using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TaxKeepVN.Application.Constants;
using TaxKeepVN.Application.DTOs.TaxDocumentTypes;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    public class TaxDocumentTypeService : ITaxDocumentTypeService
    {
        private const string CachePrefix = "taxkeep:doc_types:";
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<TaxDocumentTypeService> _logger;
        private readonly IRedisCacheService? _redisCache;

        public TaxDocumentTypeService(
            IUnitOfWork unitOfWork,
            ILogger<TaxDocumentTypeService> logger,
            IRedisCacheService? redisCache = null)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _redisCache = redisCache;
        }

        public async Task<IEnumerable<TaxDocumentTypeDto>> GetAllAsync(TaxDocumentTypeQueryParameters? query = null)
        {
            string? cacheKey = null;
            if (query == null || (!query.IsTaxEligible.HasValue && string.IsNullOrWhiteSpace(query.Search)))
            {
                cacheKey = $"{CachePrefix}all";
            }
            else if (query.IsTaxEligible == true && string.IsNullOrWhiteSpace(query.Search))
            {
                cacheKey = $"{CachePrefix}tax_eligible";
            }

            if (cacheKey != null && _redisCache != null)
            {
                var cached = await _redisCache.GetAsync<List<TaxDocumentTypeDto>>(cacheKey);
                if (cached != null && cached.Count > 0)
                    return cached;
            }

            var repo = _unitOfWork.Repository<TaxDocumentType>();
            var all = await repo.GetAllAsync();
            var queryable = all.AsQueryable();

            if (query != null)
            {
                if (query.IsTaxEligible.HasValue)
                {
                    queryable = queryable.Where(t => t.IsTaxEligible == query.IsTaxEligible.Value);
                }

                if (!string.IsNullOrWhiteSpace(query.Search))
                {
                    var search = query.Search.Trim().ToLowerInvariant();
                    queryable = queryable.Where(t =>
                        t.Code.ToLowerInvariant().Contains(search) ||
                        t.Name.ToLowerInvariant().Contains(search));
                }
            }

            var result = queryable.Select(t => new TaxDocumentTypeDto
            {
                Code = t.Code,
                Name = t.Name,
                Description = t.Description,
                IsTaxEligible = t.IsTaxEligible
            }).ToList();

            if (cacheKey != null && _redisCache != null && result.Count > 0)
            {
                await _redisCache.SetAsync(cacheKey, result, TimeSpan.FromHours(24));
            }

            return result;
        }

        public async Task<TaxDocumentTypeDto> GetByCodeAsync(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new BadRequestException(ErrorCodes.InvalidCode, ErrorMessages.DocTypeCodeRequired);

            var normalizedCode = code.Trim().ToUpperInvariant();
            var cacheKey = $"{CachePrefix}{normalizedCode}";

            if (_redisCache != null)
            {
                var cached = await _redisCache.GetAsync<TaxDocumentTypeDto>(cacheKey);
                if (cached != null)
                    return cached;
            }

            var repo = _unitOfWork.Repository<TaxDocumentType>();
            var item = (await repo.FindAsync(t => t.Code == normalizedCode)).FirstOrDefault();

            if (item == null)
            {
                throw new NotFoundException(ErrorMessages.DocTypeNotFound(code));
            }

            var dto = new TaxDocumentTypeDto
            {
                Code = item.Code,
                Name = item.Name,
                Description = item.Description,
                IsTaxEligible = item.IsTaxEligible
            };

            if (_redisCache != null)
            {
                await _redisCache.SetAsync(cacheKey, dto, TimeSpan.FromHours(24));
            }

            return dto;
        }

        public async Task<TaxDocumentTypeDto> CreateAsync(CreateTaxDocumentTypeDto dto)
        {
            var normalizedCode = dto.Code.Trim().ToUpperInvariant();
            var repo = _unitOfWork.Repository<TaxDocumentType>();

            var existing = (await repo.FindAsync(t => t.Code == normalizedCode)).FirstOrDefault();
            if (existing != null)
            {
                throw new ConflictException(ErrorCodes.DuplicateCode, ErrorMessages.DuplicateDocTypeCode(normalizedCode));
            }

            var entity = new TaxDocumentType
            {
                Code = normalizedCode,
                Name = dto.Name.Trim(),
                Description = dto.Description?.Trim(),
                IsTaxEligible = dto.IsTaxEligible
            };

            await repo.AddAsync(entity);
            await _unitOfWork.SaveChangesAsync();
            if (_redisCache != null) await _redisCache.RemoveByPrefixAsync(CachePrefix);

            _logger.LogInformation("Created TaxDocumentType '{Code}' - '{Name}'", entity.Code, entity.Name);

            return new TaxDocumentTypeDto
            {
                Code = entity.Code,
                Name = entity.Name,
                Description = entity.Description,
                IsTaxEligible = entity.IsTaxEligible
            };
        }

        public async Task<TaxDocumentTypeDto> UpdateAsync(string code, UpdateTaxDocumentTypeDto dto)
        {
            var normalizedCode = code.Trim().ToUpperInvariant();
            var repo = _unitOfWork.Repository<TaxDocumentType>();

            var entity = (await repo.FindAsync(t => t.Code == normalizedCode)).FirstOrDefault();
            if (entity == null)
            {
                throw new NotFoundException(ErrorMessages.DocTypeNotFound(code));
            }

            entity.Name = dto.Name.Trim();
            entity.Description = dto.Description?.Trim();
            entity.IsTaxEligible = dto.IsTaxEligible;

            repo.Update(entity);
            await _unitOfWork.SaveChangesAsync();
            if (_redisCache != null) await _redisCache.RemoveByPrefixAsync(CachePrefix);

            _logger.LogInformation("Updated TaxDocumentType '{Code}'", entity.Code);

            return new TaxDocumentTypeDto
            {
                Code = entity.Code,
                Name = entity.Name,
                Description = entity.Description,
                IsTaxEligible = entity.IsTaxEligible
            };
        }
    }
}
