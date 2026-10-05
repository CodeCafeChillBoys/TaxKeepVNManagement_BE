using Microsoft.Extensions.Logging;
using TaxKeepVN.Application.DTOs.ConsultationFeeConfigurations;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    public class ConsultationFeeConfigService : IConsultationFeeConfigService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ConsultationFeeConfigService> _logger;

        public ConsultationFeeConfigService(IUnitOfWork unitOfWork, ILogger<ConsultationFeeConfigService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<IEnumerable<ConsultationFeeConfigDto>> GetAllAsync(ConsultationFeeConfigQueryParameters? query = null)
        {
            var repo = _unitOfWork.Repository<ConsultationFeeConfiguration>();
            var all = await repo.GetAllAsync();
            var queryable = all.AsQueryable();

            if (query != null)
            {
                if (query.SessionType.HasValue)
                {
                    queryable = queryable.Where(c => c.SessionType == query.SessionType.Value);
                }

                if (query.IsActive.HasValue)
                {
                    queryable = queryable.Where(c => c.IsActive == query.IsActive.Value);
                }
            }

            return queryable
                .OrderBy(c => c.SessionType)
                .ThenBy(c => c.DurationMinutes)
                .Select(c => new ConsultationFeeConfigDto
                {
                    Id = c.Id,
                    SessionType = c.SessionType,
                    DurationMinutes = c.DurationMinutes,
                    MinFee = c.MinFee,
                    MaxFee = c.MaxFee,
                    IsActive = c.IsActive
                })
                .ToList();
        }

        public async Task<ConsultationFeeConfigDto> GetByIdAsync(int id)
        {
            var repo = _unitOfWork.Repository<ConsultationFeeConfiguration>();
            var list = await repo.FindAsync(c => c.Id == id);
            var item = list.FirstOrDefault();

            if (item == null)
            {
                throw new NotFoundException($"Không tìm thấy cấu hình mức phí tư vấn với Id = {id}.");
            }

            return new ConsultationFeeConfigDto
            {
                Id = item.Id,
                SessionType = item.SessionType,
                DurationMinutes = item.DurationMinutes,
                MinFee = item.MinFee,
                MaxFee = item.MaxFee,
                IsActive = item.IsActive
            };
        }

        public async Task<ConsultationFeeConfigDto> CreateAsync(CreateConsultationFeeConfigDto dto)
        {
            if (dto.MaxFee < dto.MinFee)
            {
                throw new BadRequestException("INVALID_FEE_RANGE", "Mức phí trần (MaxFee) phải lớn hơn hoặc bằng mức phí sàn (MinFee).");
            }

            var repo = _unitOfWork.Repository<ConsultationFeeConfiguration>();
            var existing = await repo.FindAsync(c => c.SessionType == dto.SessionType && c.DurationMinutes == dto.DurationMinutes);
            if (existing.Any())
            {
                throw new ConflictException("FEE_CONFIG_EXISTS", $"Cấu hình mức phí cho hình thức {dto.SessionType} ({dto.DurationMinutes} phút) đã tồn tại.");
            }

            var entity = new ConsultationFeeConfiguration
            {
                SessionType = dto.SessionType,
                DurationMinutes = dto.DurationMinutes,
                MinFee = dto.MinFee,
                MaxFee = dto.MaxFee,
                IsActive = dto.IsActive
            };

            await repo.AddAsync(entity);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Đã tạo mới cấu hình mức phí: Id={Id}, {SessionType} - {DurationMinutes}m, Min={MinFee}, Max={MaxFee}",
                entity.Id, entity.SessionType, entity.DurationMinutes, entity.MinFee, entity.MaxFee);

            return new ConsultationFeeConfigDto
            {
                Id = entity.Id,
                SessionType = entity.SessionType,
                DurationMinutes = entity.DurationMinutes,
                MinFee = entity.MinFee,
                MaxFee = entity.MaxFee,
                IsActive = entity.IsActive
            };
        }

        public async Task<ConsultationFeeConfigDto> UpdateAsync(int id, UpdateConsultationFeeConfigDto dto)
        {
            if (dto.MaxFee < dto.MinFee)
            {
                throw new BadRequestException("INVALID_FEE_RANGE", "Mức phí trần (MaxFee) phải lớn hơn hoặc bằng mức phí sàn (MinFee).");
            }

            var repo = _unitOfWork.Repository<ConsultationFeeConfiguration>();
            var list = await repo.FindAsync(c => c.Id == id);
            var entity = list.FirstOrDefault();

            if (entity == null)
            {
                throw new NotFoundException($"Không tìm thấy cấu hình mức phí tư vấn với Id = {id}.");
            }

            // Kiểm tra trùng lặp với bản ghi khác
            var duplicate = await repo.FindAsync(c => c.Id != id && c.SessionType == dto.SessionType && c.DurationMinutes == dto.DurationMinutes);
            if (duplicate.Any())
            {
                throw new ConflictException("FEE_CONFIG_EXISTS", $"Cấu hình mức phí cho hình thức {dto.SessionType} ({dto.DurationMinutes} phút) đã tồn tại ở bản ghi khác.");
            }

            entity.SessionType = dto.SessionType;
            entity.DurationMinutes = dto.DurationMinutes;
            entity.MinFee = dto.MinFee;
            entity.MaxFee = dto.MaxFee;
            entity.IsActive = dto.IsActive;

            repo.Update(entity);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Đã cập nhật cấu hình mức phí Id={Id}: {SessionType} - {DurationMinutes}m, Min={MinFee}, Max={MaxFee}",
                entity.Id, entity.SessionType, entity.DurationMinutes, entity.MinFee, entity.MaxFee);

            return new ConsultationFeeConfigDto
            {
                Id = entity.Id,
                SessionType = entity.SessionType,
                DurationMinutes = entity.DurationMinutes,
                MinFee = entity.MinFee,
                MaxFee = entity.MaxFee,
                IsActive = entity.IsActive
            };
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var repo = _unitOfWork.Repository<ConsultationFeeConfiguration>();
            var list = await repo.FindAsync(c => c.Id == id);
            var entity = list.FirstOrDefault();

            if (entity == null)
            {
                throw new NotFoundException($"Không tìm thấy cấu hình mức phí tư vấn với Id = {id}.");
            }

            // Kiểm tra xem đã có đề xuất phí trong hồ sơ chuyên gia sử dụng cấu hình này chưa
            var feePropRepo = _unitOfWork.Repository<ExpertApplicationFeeProposal>();
            var isInUse = (await feePropRepo.FindAsync(p => p.SessionType == entity.SessionType && p.DurationMinutes == entity.DurationMinutes)).Any();

            if (isInUse)
            {
                // Nếu đã có hồ sơ sử dụng, chuyển sang không hoạt động (soft-delete) để bảo toàn dữ liệu lịch sử
                entity.IsActive = false;
                repo.Update(entity);
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Cấu hình mức phí Id={Id} đang được hồ sơ sử dụng, chuyển sang IsActive = false.", id);
                return true;
            }

            // Nếu chưa hồ sơ nào dùng, cho phép xóa hẳn khỏi DB
            repo.Remove(entity);
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Đã xóa hoàn toàn cấu hình mức phí Id={Id}.", id);
            return true;
        }
    }
}
