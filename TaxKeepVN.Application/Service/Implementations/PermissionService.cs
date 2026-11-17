using TaxKeepDomain.Entities;
using TaxKeepVN.Application.DTOs.Permissions;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    public class PermissionService : IPermissionService
    {
        private readonly IUnitOfWork _unitOfWork;

        public PermissionService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<IEnumerable<PermissionDto>> GetAllAsync()
        {
            var permissions = await _unitOfWork
                .Repository<Permission>()
                .GetAllAsync();

            return permissions
                .OrderBy(p => p.GroupName)
                .ThenBy(p => p.Code)
                .Select(MapToDto)
                .ToList();
        }

        public async Task<PermissionDto> GetByIdAsync(int id)
        {
            var entity = await FindPermissionAsync(id);
            return MapToDto(entity);
        }

        public async Task<PermissionDto> CreateAsync(CreatePermissionDto dto)
        {
            ValidateDisplayFields(dto.Name, dto.GroupName);

            if (string.IsNullOrWhiteSpace(dto.Code))
            {
                throw new BadRequestException(
                    "INVALID_PERMISSION_CODE",
                    "Mã quyền không được để trống.");
            }

            var repo = _unitOfWork.Repository<Permission>();
            var code = dto.Code.Trim().ToLowerInvariant();

            var existing = await repo.FindAsync(p => p.Code == code);

            if (existing.Any())
            {
                throw new ConflictException(
                    "PERMISSION_CODE_EXISTS",
                    $"Mã quyền '{code}' đã tồn tại.");
            }

            var entity = new Permission
            {
                Code = code,
                Name = dto.Name.Trim(),
                GroupName = dto.GroupName.Trim()
            };

            await repo.AddAsync(entity);
            await _unitOfWork.SaveChangesAsync();

            return MapToDto(entity);
        }

        public async Task<PermissionDto> UpdateAsync(
            int id,
            UpdatePermissionDto dto)
        {
            ValidateDisplayFields(dto.Name, dto.GroupName);

            var entity = await FindPermissionAsync(id);

            entity.Name = dto.Name.Trim();
            entity.GroupName = dto.GroupName.Trim();

            _unitOfWork.Repository<Permission>().Update(entity);
            await _unitOfWork.SaveChangesAsync();

            return MapToDto(entity);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var entity = await FindPermissionAsync(id);

            var assignments = await _unitOfWork
                .Repository<RolePermission>()
                .FindAsync(rp => rp.PermissionId == id);

            if (assignments.Any())
            {
                throw new ConflictException(
                    "PERMISSION_IN_USE",
                    "Quyền đang được gán cho role. Hãy thu hồi trước khi xóa.");
            }

            _unitOfWork.Repository<Permission>().Remove(entity);
            await _unitOfWork.SaveChangesAsync();

            return true;
        }

        private async Task<Permission> FindPermissionAsync(int id)
        {
            var permissions = await _unitOfWork
                .Repository<Permission>()
                .FindAsync(p => p.Id == id);

            return permissions.FirstOrDefault()
                ?? throw new NotFoundException(
                    $"Không tìm thấy quyền với Id = {id}.");
        }

        private static void ValidateDisplayFields(
            string name,
            string groupName)
        {
            if (string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(groupName))
            {
                throw new BadRequestException(
                    "INVALID_PERMISSION",
                    "Tên và nhóm quyền không được để trống.");
            }
        }

        private static PermissionDto MapToDto(Permission entity)
        {
            return new PermissionDto
            {
                Id = entity.Id,
                Code = entity.Code,
                Name = entity.Name,
                GroupName = entity.GroupName
            };
        }
    }
}