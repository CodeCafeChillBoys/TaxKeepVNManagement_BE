using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.SystemConfigs;
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
                configs = configs.Where(c => c.IsActive == query.IsActive.Value);

            // 2. Filter by TaxYear or AppliesFromYear
            if (query.TaxYear.HasValue)
            {
                // Khi lọc theo năm tính thuế cụ thể:
                // Với mỗi key, lấy đúng config hiệu lực cho năm đó
                var targetYear = query.TaxYear.Value;
                configs = configs
                    .GroupBy(c => c.ConfigKey)
                    .Select(g =>
                    {
                        var match = g
                            .Where(c => c.AppliesFromYear.HasValue && c.AppliesFromYear.Value <= targetYear)
                            .OrderByDescending(c => c.AppliesFromYear!.Value)
                            .FirstOrDefault();
                        return match ?? g.FirstOrDefault(c => c.AppliesFromYear == null);
                    })
                    .Where(c => c != null)!;
            }
            else if (query.AppliesFromYear.HasValue)
            {
                if (query.AppliesFromYear.Value == 0)
                    configs = configs.Where(c => c.AppliesFromYear == null);
                else
                    configs = configs.Where(c => c.AppliesFromYear == query.AppliesFromYear.Value);
            }

            // 3. Searching by ConfigKey or Description
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var kw = query.Search.Trim().ToLower();
                configs = configs.Where(c =>
                    c.ConfigKey.ToLower().Contains(kw) ||
                    (!string.IsNullOrEmpty(c.Description) && c.Description.ToLower().Contains(kw)));
            }

            // 4. Sorting
            configs = ApplySort(configs, query.Sort);

            // 5. Pagination
            var totalItems = configs.Count();
            var items = configs
                .Skip((query.Page - 1) * query.Size)
                .Take(query.Size)
                .Select(c => ToDto(c))
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
                return query.OrderBy(c => c.ConfigKey).ThenBy(c => c.AppliesFromYear);

            return sort.Trim().ToLower() switch
            {
                "-configkey" or "-key" => query.OrderByDescending(c => c.ConfigKey),
                "configkey" or "key"   => query.OrderBy(c => c.ConfigKey).ThenBy(c => c.AppliesFromYear),
                "-updatedat"           => query.OrderByDescending(c => c.UpdatedAt),
                "updatedat"            => query.OrderBy(c => c.UpdatedAt),
                "-createdat"           => query.OrderByDescending(c => c.CreatedAt),
                "createdat"            => query.OrderBy(c => c.CreatedAt),
                _                      => query.OrderBy(c => c.ConfigKey).ThenBy(c => c.AppliesFromYear)
            };
        }

        public async Task<SystemConfigResponseDto?> GetByKeyAsync(string key)
        {
            var repo = _unitOfWork.Repository<SystemConfig>();
            var configs = await repo.FindAsync(c => c.ConfigKey == key && c.IsActive);
            // Trả về row "chung" (AppliesFromYear = null) hoặc row mới nhất
            var config = configs
                .OrderByDescending(c => c.AppliesFromYear ?? int.MinValue)
                .FirstOrDefault();

            return config == null ? null : ToDto(config);
        }

        /// <summary>
        /// Lấy config chung (không theo năm). Dùng cho các tham số không thay đổi theo năm
        /// như tỉ lệ bảo hiểm, deadline quyết toán...
        /// </summary>
        public async Task<string> GetRequiredConfigValueAsync(string key)
        {
            var repo = _unitOfWork.Repository<SystemConfig>();
            var configs = await repo.FindAsync(c => c.ConfigKey == key && c.IsActive);
            // Ưu tiên row chung (AppliesFromYear = null); nếu không có thì lấy row mới nhất
            var config = configs.FirstOrDefault(c => c.AppliesFromYear == null)
                      ?? configs.OrderByDescending(c => c.AppliesFromYear).FirstOrDefault();

            if (config == null || string.IsNullOrWhiteSpace(config.ConfigValue))
                throw new InvalidOperationException(
                    $"Lỗi cấu hình hệ thống: Không tìm thấy tham số '{key}' trong CSDL hoặc giá trị bị rỗng. " +
                    "Vui lòng thiết lập cấu hình trước khi chạy tiến trình.");

            return config.ConfigValue.Trim();
        }

        /// <summary>
        /// Lấy config phù hợp nhất với năm thuế đang tính.
        /// Logic: ưu tiên row có AppliesFromYear lớn nhất mà vẫn &lt;= taxYear.
        ///        Fallback về row có AppliesFromYear = NULL (config chung).
        ///        Ví dụ taxYear=2024: chọn AppliesFromYear=NULL (7 bậc cũ), bỏ qua 2026.
        ///         taxYear=2026: chọn AppliesFromYear=2026 (5 bậc mới).
        ///         taxYear=2027: chọn AppliesFromYear=2026 (vẫn dùng luật 2026).
        /// </summary>
        public async Task<string> GetRequiredConfigValueAsync(string key, int taxYear)
        {
            var repo = _unitOfWork.Repository<SystemConfig>();
            var allConfigs = await repo.FindAsync(c => c.ConfigKey == key && c.IsActive);

            // Tìm config có applies_from_year phù hợp nhất (lớn nhất mà vẫn <= taxYear)
            var bestMatch = allConfigs
                .Where(c => c.AppliesFromYear.HasValue && c.AppliesFromYear.Value <= taxYear)
                .OrderByDescending(c => c.AppliesFromYear!.Value)
                .FirstOrDefault();

            // Fallback về config chung nếu không tìm được config theo năm
            bestMatch ??= allConfigs.FirstOrDefault(c => c.AppliesFromYear == null);

            if (bestMatch == null || string.IsNullOrWhiteSpace(bestMatch.ConfigValue))
                throw new InvalidOperationException(
                    $"Lỗi cấu hình hệ thống: Không tìm thấy tham số '{key}' phù hợp với năm thuế {taxYear}. " +
                    "Vui lòng thiết lập cấu hình trong Admin.");

            return bestMatch.ConfigValue.Trim();
        }

        public async Task<SystemConfigResponseDto> CreateAsync(SystemConfigCreateDto dto)
        {
            var repo = _unitOfWork.Repository<SystemConfig>();

            // Kiểm tra trùng (config_key, applies_from_year) — unique composite key
            var existing = await repo.FindAsync(c =>
                c.ConfigKey == dto.ConfigKey.Trim() &&
                c.AppliesFromYear == dto.AppliesFromYear);
            if (existing.Any())
            {
                var yearLabel = dto.AppliesFromYear.HasValue
                    ? $"năm hiệu lực {dto.AppliesFromYear}"
                    : "config chung (không có năm)";
                throw new InvalidOperationException(
                    $"Cấu hình với key '{dto.ConfigKey}' và {yearLabel} đã tồn tại. Vui lòng dùng API PUT để cập nhật.");
            }

            var config = new SystemConfig
            {
                ConfigId = Guid.NewGuid(),
                ConfigKey = dto.ConfigKey.Trim(),
                ConfigValue = dto.ConfigValue.Trim(),
                Description = dto.Description,
                AppliesFromYear = dto.AppliesFromYear,
                IsActive = dto.IsActive,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            await repo.AddAsync(config);
            await _unitOfWork.SaveChangesAsync();

            return ToDto(config);
        }

        public async Task<SystemConfigResponseDto?> GetByIdAsync(Guid id)
        {
            var repo = _unitOfWork.Repository<SystemConfig>();
            var config = await repo.GetByIdAsync(id);
            return config == null ? null : ToDto(config);
        }

        public async Task<SystemConfigResponseDto> UpdateByIdAsync(Guid id, SystemConfigUpdateDto dto)
        {
            var repo = _unitOfWork.Repository<SystemConfig>();
            var config = await repo.GetByIdAsync(id);
            if (config == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy cấu hình với Id '{id}'.");
            }

            // Kiểm tra trùng unique index (config_key, applies_from_year) nếu có đổi applies_from_year
            if (dto.AppliesFromYear != config.AppliesFromYear)
            {
                var duplicate = await repo.FindAsync(c =>
                    c.ConfigId != id &&
                    c.ConfigKey == config.ConfigKey &&
                    c.AppliesFromYear == dto.AppliesFromYear);
                if (duplicate.Any())
                {
                    var yearLabel = dto.AppliesFromYear.HasValue
                        ? $"năm hiệu lực {dto.AppliesFromYear}"
                        : "config chung";
                    throw new InvalidOperationException($"Cấu hình '{config.ConfigKey}' với {yearLabel} đã tồn tại.");
                }
                config.AppliesFromYear = dto.AppliesFromYear;
            }

            config.ConfigValue = dto.ConfigValue.Trim();
            if (!string.IsNullOrEmpty(dto.Description))
                config.Description = dto.Description;
            if (dto.IsActive.HasValue)
                config.IsActive = dto.IsActive.Value;
            config.UpdatedAt = DateTimeOffset.UtcNow;

            repo.Update(config);
            await _unitOfWork.SaveChangesAsync();
            return ToDto(config);
        }

        public async Task<bool> DeleteByIdAsync(Guid id)
        {
            var repo = _unitOfWork.Repository<SystemConfig>();
            var config = await repo.GetByIdAsync(id);
            if (config == null) return false;

            repo.Remove(config);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<SystemConfigResponseDto> UpdateAsync(string key, SystemConfigUpdateDto dto)
        {
            var repo = _unitOfWork.Repository<SystemConfig>();
            var configs = await repo.FindAsync(c => c.ConfigKey == key);
            
            // Nếu DTO có truyền AppliesFromYear thì tìm đúng row của năm đó,
            // ngược lại ưu tiên row chung (AppliesFromYear == null)
            var config = dto.AppliesFromYear.HasValue
                ? configs.FirstOrDefault(c => c.AppliesFromYear == dto.AppliesFromYear.Value)
                : configs.FirstOrDefault(c => c.AppliesFromYear == null) ?? configs.FirstOrDefault();

            if (config == null)
            {
                config = new SystemConfig
                {
                    ConfigId = Guid.NewGuid(),
                    ConfigKey = key,
                    ConfigValue = dto.ConfigValue.Trim(),
                    Description = dto.Description,
                    AppliesFromYear = dto.AppliesFromYear,
                    IsActive = dto.IsActive ?? true,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                await repo.AddAsync(config);
            }
            else
            {
                config.ConfigValue = dto.ConfigValue.Trim();
                if (!string.IsNullOrEmpty(dto.Description))
                    config.Description = dto.Description;
                if (dto.AppliesFromYear.HasValue)
                    config.AppliesFromYear = dto.AppliesFromYear;
                if (dto.IsActive.HasValue)
                    config.IsActive = dto.IsActive.Value;
                config.UpdatedAt = DateTimeOffset.UtcNow;
                repo.Update(config);
            }

            await _unitOfWork.SaveChangesAsync();
            return ToDto(config);
        }

        // ── Helper ──────────────────────────────────────────────────────────────
        private static SystemConfigResponseDto ToDto(SystemConfig c) => new()
        {
            ConfigId = c.ConfigId,
            ConfigKey = c.ConfigKey,
            ConfigValue = c.ConfigValue,
            Description = c.Description,
            AppliesFromYear = c.AppliesFromYear,
            IsActive = c.IsActive,
            UpdatedAt = c.UpdatedAt
        };
    }
}
