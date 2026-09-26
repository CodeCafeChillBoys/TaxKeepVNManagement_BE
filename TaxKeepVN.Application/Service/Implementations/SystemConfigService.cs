using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.SystemConfigs;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    public class SystemConfigService : ISystemConfigService
    {
        private readonly IUnitOfWork _unitOfWork;

        public SystemConfigService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<PagedResult<SystemConfigResponseDto>> GetAllAsync(SystemConfigQueryParameters query)
        {
            var repo = _unitOfWork.Repository<SystemConfig>();
            var configs = await repo.GetAllAsync();

            // 1. Filter by IsActive
            if (query.IsActive.HasValue)
            {
                configs = configs.Where(c => c.IsActive == query.IsActive.Value);
            }

            // 2. Searching by ConfigKey or Description
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var kw = query.Search.Trim().ToLower();
                configs = configs.Where(c =>
                    c.ConfigKey.ToLower().Contains(kw) ||
                    (!string.IsNullOrEmpty(c.Description) && c.Description.ToLower().Contains(kw)));
            }

            // 3. Sorting
            configs = ApplySort(configs, query.Sort);

            // 4. Pagination
            var totalItems = configs.Count();
            var items = configs
                .Skip((query.Page - 1) * query.Size)
                .Take(query.Size)
                .Select(c => new SystemConfigResponseDto
                {
                    ConfigId = c.ConfigId,
                    ConfigKey = c.ConfigKey,
                    ConfigValue = c.ConfigValue,
                    Description = c.Description,
                    IsActive = c.IsActive,
                    UpdatedAt = c.UpdatedAt
                })
                .ToList();

            return new PagedResult<SystemConfigResponseDto>
            {
                Items = items,
                Pagination = new PaginationMeta
                {
                    Page = query.Page,
                    PageSize = query.Size,
                    TotalItems = totalItems,
                    TotalPages = (int)Math.Ceiling(totalItems / (double)query.Size)
                }
            };
        }

        private static IEnumerable<SystemConfig> ApplySort(IEnumerable<SystemConfig> query, string? sort)
        {
            if (string.IsNullOrWhiteSpace(sort))
                return query.OrderBy(c => c.ConfigKey);

            return sort.Trim().ToLower() switch
            {
                "-configkey" or "-key" => query.OrderByDescending(c => c.ConfigKey),
                "configkey" or "key" => query.OrderBy(c => c.ConfigKey),
                "-updatedat" => query.OrderByDescending(c => c.UpdatedAt),
                "updatedat" => query.OrderBy(c => c.UpdatedAt),
                "-createdat" => query.OrderByDescending(c => c.CreatedAt),
                "createdat" => query.OrderBy(c => c.CreatedAt),
                _ => query.OrderBy(c => c.ConfigKey)
            };
        }

        public async Task<SystemConfigResponseDto?> GetByKeyAsync(string key)
        {
            var repo = _unitOfWork.Repository<SystemConfig>();
            var configs = await repo.FindAsync(c => c.ConfigKey == key && c.IsActive);
            var config = configs.FirstOrDefault();

            if (config == null) return null;

            return new SystemConfigResponseDto
            {
                ConfigId = config.ConfigId,
                ConfigKey = config.ConfigKey,
                ConfigValue = config.ConfigValue,
                Description = config.Description,
                IsActive = config.IsActive,
                UpdatedAt = config.UpdatedAt
            };
        }

        public async Task<string> GetRequiredConfigValueAsync(string key)
        {
            var config = await GetByKeyAsync(key);
            if (config == null || string.IsNullOrWhiteSpace(config.ConfigValue))
            {
                throw new InvalidOperationException($"Lỗi cấu hình hệ thống: Không tìm thấy tham số '{key}' trong CSDL hoặc giá trị bị rỗng. Vui lòng thiết lập cấu hình trước khi chạy tiến trình.");
            }
            return config.ConfigValue.Trim();
        }

        public async Task<SystemConfigResponseDto> CreateAsync(SystemConfigCreateDto dto)
        {
            var repo = _unitOfWork.Repository<SystemConfig>();
            var existing = await repo.FindAsync(c => c.ConfigKey == dto.ConfigKey.Trim());
            if (existing.Any())
            {
                throw new InvalidOperationException($"Cấu hình với key '{dto.ConfigKey}' đã tồn tại trong hệ thống. Vui lòng sử dụng API PUT để cập nhật.");
            }

            var config = new SystemConfig
            {
                ConfigId = Guid.NewGuid(),
                ConfigKey = dto.ConfigKey.Trim(),
                ConfigValue = dto.ConfigValue.Trim(),
                Description = dto.Description,
                IsActive = dto.IsActive,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            await repo.AddAsync(config);
            await _unitOfWork.SaveChangesAsync();

            return new SystemConfigResponseDto
            {
                ConfigId = config.ConfigId,
                ConfigKey = config.ConfigKey,
                ConfigValue = config.ConfigValue,
                Description = config.Description,
                IsActive = config.IsActive,
                UpdatedAt = config.UpdatedAt
            };
        }

        public async Task<SystemConfigResponseDto> UpdateAsync(string key, SystemConfigUpdateDto dto)
        {
            var repo = _unitOfWork.Repository<SystemConfig>();
            var configs = await repo.FindAsync(c => c.ConfigKey == key);
            var config = configs.FirstOrDefault();

            if (config == null)
            {
                config = new SystemConfig
                {
                    ConfigId = Guid.NewGuid(),
                    ConfigKey = key,
                    ConfigValue = dto.ConfigValue,
                    Description = dto.Description,
                    IsActive = dto.IsActive ?? true,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                await repo.AddAsync(config);
            }
            else
            {
                config.ConfigValue = dto.ConfigValue;
                if (!string.IsNullOrEmpty(dto.Description))
                {
                    config.Description = dto.Description;
                }
                if (dto.IsActive.HasValue)
                {
                    config.IsActive = dto.IsActive.Value;
                }
                config.UpdatedAt = DateTimeOffset.UtcNow;
                repo.Update(config);
            }

            await _unitOfWork.SaveChangesAsync();

            return new SystemConfigResponseDto
            {
                ConfigId = config.ConfigId,
                ConfigKey = config.ConfigKey,
                ConfigValue = config.ConfigValue,
                Description = config.Description,
                IsActive = config.IsActive,
                UpdatedAt = config.UpdatedAt
            };
        }
    }
}
