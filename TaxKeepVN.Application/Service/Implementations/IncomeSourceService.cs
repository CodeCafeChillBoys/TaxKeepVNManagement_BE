using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.IncomeSources;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    public class IncomeSourceService : IIncomeSourceService
    {
        private readonly IUnitOfWork _unitOfWork;

        public IncomeSourceService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        #region Public Methods

        public async Task<PagedResult<IncomeSourceResponseDto>> GetAllByUserIdAsync(Guid userId, QueryParameters query)
        {
            var repo = _unitOfWork.Repository<IncomeSource>();
            var sources = await repo.FindAsync(s => s.TaxpayerId == userId);

            // Searching
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var kw = query.Search.ToLower();
                sources = sources.Where(s =>
                    s.CompanyName.ToLower().Contains(kw) ||
                    s.CompanyTaxCode.Contains(kw));
            }

            // Sorting: field or -field (DESC)
            sources = ApplySort(sources, query.Sort);

            var totalItems = sources.Count();
            var items = sources
                .Skip((query.Page - 1) * query.Size)
                .Take(query.Size)
                .Select(MapToDto)
                .ToList();

            return new PagedResult<IncomeSourceResponseDto>
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

        public async Task<IncomeSourceResponseDto> GetByIdAsync(Guid id, Guid userId)
        {
            var source = await GetAndVerifyOwnershipAsync(id, userId);
            return MapToDto(source);
        }

        public async Task<IncomeSourceResponseDto> CreateAsync(Guid userId, IncomeSourceCreateDto dto)
        {
            ValidateTaxCode(dto.CompanyTaxCode);

            var repo = _unitOfWork.Repository<IncomeSource>();

            // Check duplicate MST for same user
            var existing = await repo.FindAsync(s =>
                s.TaxpayerId == userId && s.CompanyTaxCode == dto.CompanyTaxCode.Trim());
            if (existing.Any())
                throw new ConflictException("DUPLICATE_TAX_CODE",
                    $"Mã số thuế '{dto.CompanyTaxCode}' đã được đăng ký. Mỗi tổ chức chi trả chỉ được khai báo một lần.");

            var source = new IncomeSource
            {
                Id = Guid.NewGuid(),
                TaxpayerId = userId,
                CompanyName = dto.CompanyName.Trim(),
                CompanyTaxCode = dto.CompanyTaxCode.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await repo.AddAsync(source);
            await _unitOfWork.SaveChangesAsync();
            return MapToDto(source);
        }

        public async Task<IncomeSourceResponseDto> UpdateAsync(Guid id, Guid userId, IncomeSourceUpdateDto dto)
        {
            var source = await GetAndVerifyOwnershipAsync(id, userId);
            ValidateTaxCode(dto.CompanyTaxCode);

            var repo = _unitOfWork.Repository<IncomeSource>();
            // Check duplicate MST for same user (exclude current record)
            var existing = await repo.FindAsync(s =>
                s.TaxpayerId == userId &&
                s.CompanyTaxCode == dto.CompanyTaxCode.Trim() &&
                s.Id != id);
            if (existing.Any())
                throw new ConflictException("DUPLICATE_TAX_CODE",
                    $"Mã số thuế '{dto.CompanyTaxCode}' đã được đăng ký ở một nơi chi trả khác.");

            source.CompanyName = dto.CompanyName.Trim();
            source.CompanyTaxCode = dto.CompanyTaxCode.Trim();
            source.IsActive = dto.IsActive;
            source.UpdatedAt = DateTime.UtcNow;

            repo.Update(source);
            await _unitOfWork.SaveChangesAsync();
            return MapToDto(source);
        }

        public async Task DeleteAsync(Guid id, Guid userId)
        {
            var source = await GetAndVerifyOwnershipAsync(id, userId);
            _unitOfWork.Repository<IncomeSource>().Remove(source);
            await _unitOfWork.SaveChangesAsync();
        }

        #endregion

        #region Private Helpers

        private async Task<IncomeSource> GetAndVerifyOwnershipAsync(Guid id, Guid userId)
        {
            var source = await _unitOfWork.Repository<IncomeSource>().GetByIdAsync(id);
            if (source == null)
                throw new NotFoundException("Không tìm thấy thông tin nơi chi trả thu nhập.");
            if (source.TaxpayerId != userId)
                throw new ForbiddenException("Bạn không có quyền thao tác trên dữ liệu này.");
            return source;
        }

        private static IncomeSourceResponseDto MapToDto(IncomeSource s) => new IncomeSourceResponseDto
        {
            Id = s.Id,
            CompanyName = s.CompanyName,
            CompanyTaxCode = s.CompanyTaxCode,
            IsActive = s.IsActive,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt
        };

        /// <summary>
        /// Validate MST theo chuẩn Tổng cục Thuế Việt Nam.
        /// Định dạng hợp lệ: 10 chữ số HOẶC 13 chữ số dạng XXXXXXXXXX-XXX.
        /// Checksum: 9 số đầu nhân trọng số [31,29,23,19,17,13,7,5,3] cộng lại,
        /// lấy (11 - (sum % 11)) % 11 phải bằng số thứ 10.
        /// </summary>
        private static void ValidateTaxCode(string taxCode)
        {
            if (string.IsNullOrWhiteSpace(taxCode))
                throw new BadRequestException("INVALID_TAX_CODE", "Mã số thuế không được để trống.");

            taxCode = taxCode.Trim();

            // Allow 10-digit or 13-digit (XXXXXXXXXX-XXX) format
            var regex10 = new Regex(@"^\d{10}$");
            var regex13 = new Regex(@"^\d{10}-\d{3}$");

            if (!regex10.IsMatch(taxCode) && !regex13.IsMatch(taxCode))
                throw new BadRequestException("INVALID_TAX_CODE_FORMAT",
                    "Định dạng mã số thuế không hợp lệ. MST phải có 10 chữ số (VD: 0101234567) hoặc 13 ký tự có dấu gạch ngang (VD: 0101234567-001).");

            // Checksum for the first 10 digits
            string tenDigits = taxCode.Substring(0, 10);
            int[] weights = { 31, 29, 23, 19, 17, 13, 7, 5, 3 };
            int sum = 0;
            for (int i = 0; i < 9; i++)
                sum += (tenDigits[i] - '0') * weights[i];

            int remainder = sum % 11;
            int checkDigit = remainder == 0 ? 0 : (11 - remainder) % 11;
            int actualCheckDigit = tenDigits[9] - '0';

            if (checkDigit != actualCheckDigit)
                throw new BadRequestException("INVALID_TAX_CODE_CHECKSUM",
                    "Mã số thuế không hợp lệ theo thuật toán kiểm tra của Tổng cục Thuế. Vui lòng kiểm tra lại.");
        }

        private static IEnumerable<IncomeSource> ApplySort(IEnumerable<IncomeSource> sources, string? sort)
        {
            if (string.IsNullOrWhiteSpace(sort))
                return sources.OrderByDescending(s => s.CreatedAt);

            bool desc = sort.StartsWith("-");
            string field = sort.TrimStart('-').ToLower();

            return field switch
            {
                "companyname" => desc ? sources.OrderByDescending(s => s.CompanyName) : sources.OrderBy(s => s.CompanyName),
                "companytaxcode" => desc ? sources.OrderByDescending(s => s.CompanyTaxCode) : sources.OrderBy(s => s.CompanyTaxCode),
                "createdat" => desc ? sources.OrderByDescending(s => s.CreatedAt) : sources.OrderBy(s => s.CreatedAt),
                "updatedat" => desc ? sources.OrderByDescending(s => s.UpdatedAt) : sources.OrderBy(s => s.UpdatedAt),
                _ => sources.OrderByDescending(s => s.CreatedAt) // Default
            };
        }

        #endregion
    }
}
