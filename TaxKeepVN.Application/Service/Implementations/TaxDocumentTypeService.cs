using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TaxKeepVN.Application.DTOs.TaxDocumentTypes;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    public class TaxDocumentTypeService : ITaxDocumentTypeService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<TaxDocumentTypeService> _logger;

        public TaxDocumentTypeService(IUnitOfWork unitOfWork, ILogger<TaxDocumentTypeService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<IEnumerable<TaxDocumentTypeDto>> GetAllAsync(TaxDocumentTypeQueryParameters? query = null)
        {
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

            return queryable.Select(t => new TaxDocumentTypeDto
            {
                Code = t.Code,
                Name = t.Name,
                IsTaxEligible = t.IsTaxEligible
            }).ToList();
        }

        public async Task<TaxDocumentTypeDto> GetByCodeAsync(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new BadRequestException("INVALID_CODE", "Mã loại chứng từ không được để trống.");

            var normalizedCode = code.Trim().ToUpperInvariant();
            var repo = _unitOfWork.Repository<TaxDocumentType>();
            var item = (await repo.FindAsync(t => t.Code == normalizedCode)).FirstOrDefault();

            if (item == null)
            {
                throw new NotFoundException($"Không tìm thấy loại chứng từ với mã '{code}'.");
            }

            return new TaxDocumentTypeDto
            {
                Code = item.Code,
                Name = item.Name,
                IsTaxEligible = item.IsTaxEligible
            };
        }

        public async Task<TaxDocumentTypeDto> CreateAsync(CreateTaxDocumentTypeDto dto)
        {
            var normalizedCode = dto.Code.Trim().ToUpperInvariant();
            var repo = _unitOfWork.Repository<TaxDocumentType>();

            var existing = (await repo.FindAsync(t => t.Code == normalizedCode)).FirstOrDefault();
            if (existing != null)
            {
                throw new ConflictException("DUPLICATE_CODE", $"Mã loại chứng từ '{normalizedCode}' đã tồn tại trong hệ thống.");
            }

            var entity = new TaxDocumentType
            {
                Code = normalizedCode,
                Name = dto.Name.Trim(),
                IsTaxEligible = dto.IsTaxEligible
            };

            await repo.AddAsync(entity);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created TaxDocumentType '{Code}' - '{Name}'", entity.Code, entity.Name);

            return new TaxDocumentTypeDto
            {
                Code = entity.Code,
                Name = entity.Name,
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
                throw new NotFoundException($"Không tìm thấy loại chứng từ với mã '{code}'.");
            }

            entity.Name = dto.Name.Trim();
            entity.IsTaxEligible = dto.IsTaxEligible;

            repo.Update(entity);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated TaxDocumentType '{Code}'", entity.Code);

            return new TaxDocumentTypeDto
            {
                Code = entity.Code,
                Name = entity.Name,
                IsTaxEligible = entity.IsTaxEligible
            };
        }

        public async Task<TaxDocumentType> EnsureExistsAsync(string code, string? defaultName = null)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Mã loại chứng từ không được rỗng.", nameof(code));

            var normalizedCode = code.Trim().ToUpperInvariant();
            var repo = _unitOfWork.Repository<TaxDocumentType>();

            var existing = (await repo.FindAsync(t => t.Code == normalizedCode)).FirstOrDefault();
            if (existing != null)
            {
                return existing;
            }

            var newType = new TaxDocumentType
            {
                Code = normalizedCode,
                Name = !string.IsNullOrWhiteSpace(defaultName) ? defaultName.Trim() : normalizedCode,
                IsTaxEligible = true
            };

            await repo.AddAsync(newType);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Auto-created TaxDocumentType '{Code}' for OCR classification mapping", normalizedCode);
            return newType;
        }
    }
}
