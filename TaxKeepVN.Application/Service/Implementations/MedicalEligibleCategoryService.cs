using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TaxKeepVN.Application.DTOs.MedicalCategories;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    public class MedicalEligibleCategoryService : IMedicalEligibleCategoryService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<MedicalEligibleCategoryService> _logger;

        public MedicalEligibleCategoryService(
            IUnitOfWork unitOfWork,
            ILogger<MedicalEligibleCategoryService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<IEnumerable<MedicalEligibleCategoryDto>> GetAllAsync(MedicalCategoryQueryParameters? query = null)
        {
            var repo = _unitOfWork.Repository<MedicalEligibleCategory>();
            var all = await repo.GetAllAsync();
            var queryable = all.AsQueryable();

            bool includeDeleted = query?.IncludeDeleted ?? false;
            if (!includeDeleted)
            {
                queryable = queryable.Where(c => !c.IsDeleted);
            }

            if (query != null)
            {
                if (query.IsEligible.HasValue)
                {
                    queryable = queryable.Where(c => c.IsEligible == query.IsEligible.Value);
                }

                if (!string.IsNullOrWhiteSpace(query.SectionCode))
                {
                    var section = query.SectionCode.Trim().ToUpperInvariant();
                    queryable = queryable.Where(c => c.SectionCode.ToUpperInvariant() == section);
                }

                if (!string.IsNullOrWhiteSpace(query.Search))
                {
                    var search = query.Search.Trim().ToLowerInvariant();
                    queryable = queryable.Where(c =>
                        c.CategoryCode.ToLowerInvariant().Contains(search) ||
                        c.CategoryName.ToLowerInvariant().Contains(search) ||
                        (c.Keywords != null && c.Keywords.ToLowerInvariant().Contains(search)) ||
                        (c.Description != null && c.Description.ToLowerInvariant().Contains(search)));
                }
            }

            return queryable
                .OrderBy(c => c.SectionCode)
                .ThenBy(c => c.CategoryCode)
                .Select(MapToDto)
                .ToList();
        }

        public async Task<MedicalEligibleCategoryDto> GetByIdAsync(Guid id)
        {
            var repo = _unitOfWork.Repository<MedicalEligibleCategory>();
            var item = await repo.GetByIdAsync(id);

            if (item == null || item.IsDeleted)
            {
                throw new NotFoundException($"Không tìm thấy danh mục chi phí y tế với mã '{id}'.");
            }

            return MapToDto(item);
        }

        public async Task<MedicalEligibleCategoryDto> CreateAsync(CreateMedicalEligibleCategoryDto dto)
        {
            var normalizedCode = dto.CategoryCode.Trim().ToUpperInvariant();
            var repo = _unitOfWork.Repository<MedicalEligibleCategory>();

            var existing = (await repo.FindAsync(c => c.CategoryCode == normalizedCode)).FirstOrDefault();
            if (existing != null)
            {
                if (existing.IsDeleted)
                {
                    // Nếu đã từng có nhưng bị xóa mềm, tái kích hoạt lại với thông tin mới
                    existing.IsDeleted = false;
                    existing.DeletedAt = null;
                    existing.CategoryName = dto.CategoryName.Trim();
                    existing.SectionCode = dto.SectionCode.Trim().ToUpperInvariant();
                    existing.IsEligible = dto.IsEligible;
                    existing.Description = dto.Description?.Trim();
                    existing.Keywords = dto.Keywords?.Trim();
                    existing.UpdatedAt = DateTime.UtcNow;

                    repo.Update(existing);
                    await _unitOfWork.SaveChangesAsync();
                    _logger.LogInformation("Re-activated soft-deleted MedicalEligibleCategory '{CategoryCode}'", existing.CategoryCode);
                    return MapToDto(existing);
                }

                throw new ConflictException("DUPLICATE_CATEGORY_CODE", $"Mã danh mục chi phí '{normalizedCode}' đã tồn tại.");
            }

            var entity = new MedicalEligibleCategory
            {
                Id = Guid.NewGuid(),
                CategoryCode = normalizedCode,
                CategoryName = dto.CategoryName.Trim(),
                SectionCode = dto.SectionCode.Trim().ToUpperInvariant(),
                IsEligible = dto.IsEligible,
                Description = dto.Description?.Trim(),
                Keywords = dto.Keywords?.Trim(),
                IsDeleted = false,
                DeletedAt = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await repo.AddAsync(entity);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created MedicalEligibleCategory '{CategoryCode}' - '{CategoryName}'", entity.CategoryCode, entity.CategoryName);
            return MapToDto(entity);
        }

        public async Task<MedicalEligibleCategoryDto> UpdateAsync(Guid id, UpdateMedicalEligibleCategoryDto dto)
        {
            var repo = _unitOfWork.Repository<MedicalEligibleCategory>();
            var entity = await repo.GetByIdAsync(id);

            if (entity == null || entity.IsDeleted)
            {
                throw new NotFoundException($"Không tìm thấy danh mục chi phí y tế với mã '{id}'.");
            }

            if (!string.IsNullOrWhiteSpace(dto.CategoryName))
                entity.CategoryName = dto.CategoryName.Trim();

            if (!string.IsNullOrWhiteSpace(dto.SectionCode))
                entity.SectionCode = dto.SectionCode.Trim().ToUpperInvariant();

            if (dto.IsEligible.HasValue)
                entity.IsEligible = dto.IsEligible.Value;

            if (dto.Description != null)
                entity.Description = dto.Description.Trim();

            if (dto.Keywords != null)
                entity.Keywords = dto.Keywords.Trim();

            entity.UpdatedAt = DateTime.UtcNow;

            repo.Update(entity);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated MedicalEligibleCategory '{CategoryCode}'", entity.CategoryCode);
            return MapToDto(entity);
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var repo = _unitOfWork.Repository<MedicalEligibleCategory>();
            var entity = await repo.GetByIdAsync(id);

            if (entity == null || entity.IsDeleted)
            {
                throw new NotFoundException($"Không tìm thấy danh mục chi phí y tế với mã '{id}'.");
            }

            // Xóa mềm: đánh dấu IsDeleted = true và lưu DeletedAt, KHÔNG xóa vĩnh viễn khỏi Database
            entity.IsDeleted = true;
            entity.DeletedAt = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;

            repo.Update(entity);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Soft-deleted MedicalEligibleCategory '{CategoryCode}' ({Id})", entity.CategoryCode, id);
            return true;
        }

        public async Task<bool> RestoreAsync(Guid id)
        {
            var repo = _unitOfWork.Repository<MedicalEligibleCategory>();
            var entity = await repo.GetByIdAsync(id);

            if (entity == null)
            {
                throw new NotFoundException($"Không tìm thấy danh mục chi phí y tế với mã '{id}'.");
            }

            if (!entity.IsDeleted)
            {
                return true; // Đang hoạt động, không cần khôi phục
            }

            // Khôi phục danh mục đã xóa mềm
            entity.IsDeleted = false;
            entity.DeletedAt = null;
            entity.UpdatedAt = DateTime.UtcNow;

            repo.Update(entity);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Restored soft-deleted MedicalEligibleCategory '{CategoryCode}' ({Id})", entity.CategoryCode, id);
            return true;
        }

        private static MedicalEligibleCategoryDto MapToDto(MedicalEligibleCategory c) => new MedicalEligibleCategoryDto
        {
            Id = c.Id,
            CategoryCode = c.CategoryCode,
            CategoryName = c.CategoryName,
            SectionCode = c.SectionCode,
            IsEligible = c.IsEligible,
            Description = c.Description,
            Keywords = c.Keywords,
            IsDeleted = c.IsDeleted,
            DeletedAt = c.DeletedAt,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt
        };
    }
}
