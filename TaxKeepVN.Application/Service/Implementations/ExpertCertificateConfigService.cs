using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TaxKeepVN.Application.DTOs.ExpertCertificateConfigurations;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    public class ExpertCertificateConfigService : IExpertCertificateConfigService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ExpertCertificateConfigService> _logger;

        public ExpertCertificateConfigService(IUnitOfWork unitOfWork, ILogger<ExpertCertificateConfigService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<IEnumerable<ExpertCertificateConfigDto>> GetAllAsync(ExpertCertificateConfigQueryParameters? query = null)
        {
            var repo = _unitOfWork.Repository<ExpertCertificateConfiguration>();
            var all = await repo.GetAllAsync();
            var queryable = all.AsQueryable();

            if (query != null)
            {
                if (query.IsActive.HasValue)
                {
                    queryable = queryable.Where(c => c.IsActive == query.IsActive.Value);
                }

                if (!string.IsNullOrWhiteSpace(query.Search))
                {
                    var keyword = query.Search.Trim().ToLower();
                    queryable = queryable.Where(c => c.Name.ToLower().Contains(keyword) || c.Code.ToLower().Contains(keyword));
                }
            }

            return queryable
                .OrderBy(c => c.DisplayOrder)
                .ThenBy(c => c.Id)
                .Select(c => MapToDto(c))
                .ToList();
        }

        public async Task<ExpertCertificateConfigDto> GetByIdAsync(int id)
        {
            var repo = _unitOfWork.Repository<ExpertCertificateConfiguration>();
            var list = await repo.FindAsync(c => c.Id == id);
            var item = list.FirstOrDefault();
            if (item == null)
            {
                throw new NotFoundException($"Không tìm thấy cấu hình loại chứng chỉ với Id = {id}.");
            }
            return MapToDto(item);
        }

        public async Task<ExpertCertificateConfigDto> CreateAsync(CreateExpertCertificateConfigRequest request)
        {
            var code = request.Code.Trim().ToUpperInvariant();
            var repo = _unitOfWork.Repository<ExpertCertificateConfiguration>();

            var existing = await repo.FindAsync(c => c.Code == code);
            if (existing.Any())
            {
                throw new ConflictException("CERTIFICATE_CONFIG_EXISTS", $"Mã chứng chỉ '{code}' đã tồn tại trong hệ thống.");
            }

            var entity = new ExpertCertificateConfiguration
            {
                Code = code,
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                DefaultIssuingAuthority = request.DefaultIssuingAuthority?.Trim(),
                RequiresExpiryDate = request.RequiresExpiryDate,
                AiKeywords = request.AiKeywords?.Trim(),
                DisplayOrder = request.DisplayOrder,
                IsActive = request.IsActive,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            await repo.AddAsync(entity);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Admin tạo mới loại chứng chỉ chuyên gia: {Code} - {Name}", entity.Code, entity.Name);
            return MapToDto(entity);
        }

        public async Task<ExpertCertificateConfigDto> UpdateAsync(int id, UpdateExpertCertificateConfigRequest request)
        {
            var repo = _unitOfWork.Repository<ExpertCertificateConfiguration>();
            var list = await repo.FindAsync(c => c.Id == id);
            var item = list.FirstOrDefault();
            if (item == null)
            {
                throw new NotFoundException($"Không tìm thấy cấu hình loại chứng chỉ với Id = {id}.");
            }

            item.Name = request.Name.Trim();
            item.Description = request.Description?.Trim();
            item.DefaultIssuingAuthority = request.DefaultIssuingAuthority?.Trim();
            item.RequiresExpiryDate = request.RequiresExpiryDate;
            item.AiKeywords = request.AiKeywords?.Trim();
            item.DisplayOrder = request.DisplayOrder;
            item.IsActive = request.IsActive;
            item.UpdatedAt = DateTimeOffset.UtcNow;

            repo.Update(item);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Admin cập nhật loại chứng chỉ chuyên gia Id {Id}: {Code} - {Name}", item.Id, item.Code, item.Name);
            return MapToDto(item);
        }

        public async Task<ExpertCertificateConfigDto> ToggleActiveAsync(int id)
        {
            var repo = _unitOfWork.Repository<ExpertCertificateConfiguration>();
            var list = await repo.FindAsync(c => c.Id == id);
            var item = list.FirstOrDefault();
            if (item == null)
            {
                throw new NotFoundException($"Không tìm thấy cấu hình loại chứng chỉ với Id = {id}.");
            }

            item.IsActive = !item.IsActive;
            item.UpdatedAt = DateTimeOffset.UtcNow;

            repo.Update(item);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Admin thay đổi trạng thái loại chứng chỉ Id {Id} ({Code}) thành {IsActive}", item.Id, item.Code, item.IsActive);
            return MapToDto(item);
        }

        public async Task DeleteAsync(int id)
        {
            var repo = _unitOfWork.Repository<ExpertCertificateConfiguration>();
            var list = await repo.FindAsync(c => c.Id == id);
            var item = list.FirstOrDefault();
            if (item == null)
            {
                throw new NotFoundException($"Không tìm thấy cấu hình loại chứng chỉ với Id = {id}.");
            }

            // Kiểm tra xem đã có chuyên gia nào dùng mã chứng chỉ này chưa
            var certRepo = _unitOfWork.Repository<ExpertApplicationCertificate>();
            var usedCerts = await certRepo.FindAsync(c => c.CertificateType == item.Code);
            if (usedCerts.Any())
            {
                throw new BadRequestException("CANNOT_DELETE_IN_USE",
                    $"Không thể xóa loại chứng chỉ '{item.Code}' vì đã có {usedCerts.Count()} hồ sơ chuyên gia đang sử dụng. Bạn có thể chọn vô hiệu hóa (IsActive = false).");
            }

            repo.Remove(item);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Admin xóa loại chứng chỉ chuyên gia Id {Id}: {Code}", item.Id, item.Code);
        }

        private static ExpertCertificateConfigDto MapToDto(ExpertCertificateConfiguration entity)
        {
            return new ExpertCertificateConfigDto
            {
                Id = entity.Id,
                Code = entity.Code,
                Name = entity.Name,
                Description = entity.Description,
                DefaultIssuingAuthority = entity.DefaultIssuingAuthority,
                RequiresExpiryDate = entity.RequiresExpiryDate,
                AiKeywords = entity.AiKeywords,
                DisplayOrder = entity.DisplayOrder,
                IsActive = entity.IsActive,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }
    }
}