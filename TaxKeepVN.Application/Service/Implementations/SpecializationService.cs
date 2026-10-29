using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TaxKeepVN.Application.DTOs.Specializations;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    public class SpecializationService : ISpecializationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<SpecializationService> _logger;

        public SpecializationService(IUnitOfWork unitOfWork, ILogger<SpecializationService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<IEnumerable<SpecializationDto>> GetAllAsync(SpecializationQueryParameters? query = null)
        {
            var repo = _unitOfWork.Repository<Specialization>();
            var all = await repo.GetAllAsync();
            var queryable = all.AsQueryable();

            if (query != null)
            {
                if (query.IsActive.HasValue)
                {
                    queryable = queryable.Where(s => s.IsActive == query.IsActive.Value);
                }

                if (!string.IsNullOrWhiteSpace(query.Search))
                {
                    var keyword = query.Search.Trim().ToLowerInvariant();
                    queryable = queryable.Where(s =>
                        s.Code.ToLowerInvariant().Contains(keyword) ||
                        s.Name.ToLowerInvariant().Contains(keyword));
                }
            }

            return queryable
                .OrderBy(s => s.Name)
                .Select(s => new SpecializationDto
                {
                    Id = s.Id,
                    Code = s.Code,
                    Name = s.Name,
                    Description = s.Description,
                    IsActive = s.IsActive
                }).ToList();
        }

        public async Task<SpecializationDto> GetByIdAsync(int id)
        {
            var repo = _unitOfWork.Repository<Specialization>();
            var list = await repo.FindAsync(s => s.Id == id);
            var item = list.FirstOrDefault();

            if (item == null)
            {
                throw new NotFoundException($"Không tìm thấy lĩnh vực chuyên môn với Id = {id}.");
            }

            return new SpecializationDto
            {
                Id = item.Id,
                Code = item.Code,
                Name = item.Name,
                Description = item.Description,
                IsActive = item.IsActive
            };
        }

        public async Task<SpecializationDto> GetByCodeAsync(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new BadRequestException("INVALID_CODE", "Mã lĩnh vực không được để trống.");

            var normalizedCode = code.Trim().ToUpperInvariant();
            var repo = _unitOfWork.Repository<Specialization>();
            var list = await repo.FindAsync(s => s.Code == normalizedCode);
            var item = list.FirstOrDefault();

            if (item == null)
            {
                throw new NotFoundException($"Không tìm thấy lĩnh vực chuyên môn với mã '{normalizedCode}'.");
            }

            return new SpecializationDto
            {
                Id = item.Id,
                Code = item.Code,
                Name = item.Name,
                Description = item.Description,
                IsActive = item.IsActive
            };
        }

        public async Task<SpecializationDto> CreateAsync(CreateSpecializationDto dto)
        {
            var repo = _unitOfWork.Repository<Specialization>();
            var normalizedCode = dto.Code.Trim().ToUpperInvariant();

            // Kiểm tra trùng Code (ví dụ nếu đã có PIT thì không cho tạo lại)
            var existing = await repo.FindAsync(s => s.Code == normalizedCode);
            if (existing.Any())
            {
                throw new ConflictException("SPECIALIZATION_CODE_EXISTS", $"Mã lĩnh vực chuyên môn '{normalizedCode}' đã tồn tại.");
            }

            var entity = new Specialization
            {
                Code = normalizedCode,
                Name = dto.Name.Trim(),
                Description = dto.Description?.Trim(),
                IsActive = dto.IsActive
            };

            await repo.AddAsync(entity);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Đã tạo mới lĩnh vực chuyên môn: {Code} - {Name}", entity.Code, entity.Name);

            return new SpecializationDto
            {
                Id = entity.Id,
                Code = entity.Code,
                Name = entity.Name,
                Description = entity.Description,
                IsActive = entity.IsActive
            };
        }

        public async Task<SpecializationDto> UpdateAsync(int id, UpdateSpecializationDto dto)
        {
            var repo = _unitOfWork.Repository<Specialization>();
            var list = await repo.FindAsync(s => s.Id == id);
            var entity = list.FirstOrDefault();

            if (entity == null)
            {
                throw new NotFoundException($"Không tìm thấy lĩnh vực chuyên môn với Id = {id}.");
            }

            entity.Name = dto.Name.Trim();
            entity.Description = dto.Description?.Trim();
            entity.IsActive = dto.IsActive;

            repo.Update(entity);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Đã cập nhật lĩnh vực chuyên môn Id = {Id}", id);

            return new SpecializationDto
            {
                Id = entity.Id,
                Code = entity.Code,
                Name = entity.Name,
                Description = entity.Description,
                IsActive = entity.IsActive
            };
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var repo = _unitOfWork.Repository<Specialization>();
            var list = await repo.FindAsync(s => s.Id == id);
            var entity = list.FirstOrDefault();

            if (entity == null)
            {
                throw new NotFoundException($"Không tìm thấy lĩnh vực chuyên môn với Id = {id}.");
            }

            // Kiểm tra xem đã có hồ sơ chuyên gia nào chọn lĩnh vực này chưa
            var appSpecRepo = _unitOfWork.Repository<ExpertApplicationSpecialization>();
            var isInUse = (await appSpecRepo.FindAsync(x => x.SpecializationId == id)).Any();

            if (isInUse)
            {
                // Nếu đã có hồ sơ dùng, ta chỉ tắt trạng thái (Soft delete) để không gãy dữ liệu quan hệ
                entity.IsActive = false;
                repo.Update(entity);
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Lĩnh vực Id = {Id} đang có hồ sơ sử dụng, chuyển sang IsActive = false.", id);
                return true;
            }

            // Nếu chưa ai dùng, cho phép xóa hẳn khỏi DB
            repo.Remove(entity);
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Đã xóa hoàn toàn lĩnh vực Id = {Id}", id);
            return true;
        }
    }
}