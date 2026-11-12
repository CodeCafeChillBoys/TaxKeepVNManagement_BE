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

        public async Task<PagedResult<IncomeSourceResponseDto>> GetAllByUserIdAsync(Guid userId, IncomeSourceQueryParameters query)
        {
            var repo = _unitOfWork.Repository<IncomeSource>();
            var sources = await repo.FindAsync(s => s.TaxpayerId == userId);

            // Filter by TaxYear if specified
            if (query.TaxYear.HasValue)
                sources = sources.Where(s => s.TaxYear == query.TaxYear.Value);

            // Filter by IsActive if specified
            if (query.IsActive.HasValue)
                sources = sources.Where(s => s.IsActive == query.IsActive.Value);

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

        public async Task<IncomeSourceSummaryDto> GetSummaryByUserIdAsync(Guid userId, int taxYear)
        {
            var repo = _unitOfWork.Repository<IncomeSource>();
            var sources = (await repo.FindAsync(s => s.TaxpayerId == userId && s.TaxYear == taxYear)).ToList();

            return new IncomeSourceSummaryDto
            {
                TaxYear = taxYear,
                TotalIncome = sources.Sum(s => s.TotalIncome),
                TotalTaxWithheld = sources.Sum(s => s.TaxWithheld),
                TotalInsuranceDeducted = sources.Sum(s => s.InsuranceDeducted),
                TotalSources = sources.Count
            };
        }

        public async Task<IncomeSourceResponseDto> GetByIdAsync(Guid id, Guid userId)
        {
            var source = await GetAndVerifyOwnershipAsync(id, userId);
            return MapToDto(source);
        }

        public async Task<IncomeSourceResponseDto> CreateAsync(Guid userId, IncomeSourceCreateDto dto)
        {
            var taxCode = dto.ResolvedTaxCode;
            ValidateTaxCode(taxCode);

            var repo = _unitOfWork.Repository<IncomeSource>();

            // Check duplicate MST for same user in the same tax year
            var existing = await repo.FindAsync(s =>
                s.TaxpayerId == userId &&
                s.CompanyTaxCode == taxCode &&
                s.TaxYear == dto.TaxYear);
            if (existing.Any())
                throw new ConflictException("DUPLICATE_TAX_CODE",
                    $"Mã số thuế '{taxCode}' đã được đăng ký cho năm tính thuế {dto.TaxYear}. Mỗi tổ chức chi trả chỉ được khai báo một lần trong năm.");

            var source = new IncomeSource
            {
                Id = Guid.NewGuid(),
                TaxpayerId = userId,
                CompanyName = dto.CompanyName.Trim(),
                CompanyTaxCode = taxCode,
                TaxYear = dto.TaxYear,
                TotalIncome = dto.TotalIncome,
                TaxWithheld = dto.TaxWithheld,
                InsuranceDeducted = dto.InsuranceDeducted,
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
            var taxCode = dto.ResolvedTaxCode;
            ValidateTaxCode(taxCode);

            var repo = _unitOfWork.Repository<IncomeSource>();
            // Check duplicate MST for same user in the same tax year (exclude current record)
            var existing = await repo.FindAsync(s =>
                s.TaxpayerId == userId &&
                s.CompanyTaxCode == taxCode &&
                s.TaxYear == dto.TaxYear &&
                s.Id != id);
            if (existing.Any())
                throw new ConflictException("DUPLICATE_TAX_CODE",
                    $"Mã số thuế '{taxCode}' đã được đăng ký cho năm {dto.TaxYear} ở một nơi chi trả khác.");

            source.CompanyName = dto.CompanyName.Trim();
            source.CompanyTaxCode = taxCode;
            source.TaxYear = dto.TaxYear;
            source.TotalIncome = dto.TotalIncome;
            source.TaxWithheld = dto.TaxWithheld;
            source.InsuranceDeducted = dto.InsuranceDeducted;
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

        public async Task<IncomeSourceCrossCheckResponseDto> CrossCheckAsync(Guid userId, IncomeSourceCrossCheckRequestDto request)
        {
            // 1. Fetch all incomes for this user, year, and organization
            var incomes = await _unitOfWork.Repository<Income>().FindAsync(x =>
                x.UserId == userId &&
                x.Year == request.TaxYear &&
                x.OrganizationName.ToLower() == request.CompanyName.ToLower());

            // 2. Sum them up
            var summedTotalIncome = incomes.Sum(x => x.TotalTaxableIncome);
            var summedTaxWithheld = incomes.Sum(x => x.TaxAlreadyDeducted);
            var summedInsurance = incomes.Sum(x => x.InsuranceDeducted);

            // 3. Compare with Certificate
            var diffTotalIncome = request.CertificateTotalIncome - summedTotalIncome;
            var diffTaxWithheld = request.CertificateTaxWithheld - summedTaxWithheld;
            var diffInsurance = request.CertificateInsuranceDeducted - summedInsurance;

            var response = new IncomeSourceCrossCheckResponseDto
            {
                SummedTotalIncome = summedTotalIncome,
                SummedTaxWithheld = summedTaxWithheld,
                SummedInsuranceDeducted = summedInsurance,
                DiffTotalIncome = diffTotalIncome,
                DiffTaxWithheld = diffTaxWithheld,
                DiffInsuranceDeducted = diffInsurance,
                IsMatch = true
            };

            // 4. Generate mismatch messages if there's any discrepancy
            // We allow a small tolerance for rounding differences (e.g., 1000 VND)
            decimal tolerance = 1000;

            if (Math.Abs(diffTotalIncome) > tolerance)
            {
                response.IsMatch = false;
                response.MismatchMessages.Add($"Tổng thu nhập chênh lệch: {Math.Abs(diffTotalIncome):N0} VNĐ (Chứng từ: {request.CertificateTotalIncome:N0}, Hệ thống: {summedTotalIncome:N0})");
            }
            
            if (Math.Abs(diffTaxWithheld) > tolerance)
            {
                response.IsMatch = false;
                response.MismatchMessages.Add($"Thuế đã khấu trừ chênh lệch: {Math.Abs(diffTaxWithheld):N0} VNĐ (Chứng từ: {request.CertificateTaxWithheld:N0}, Hệ thống: {summedTaxWithheld:N0})");
            }

            if (Math.Abs(diffInsurance) > tolerance)
            {
                response.IsMatch = false;
                response.MismatchMessages.Add($"Bảo hiểm đã đóng chênh lệch: {Math.Abs(diffInsurance):N0} VNĐ (Chứng từ: {request.CertificateInsuranceDeducted:N0}, Hệ thống: {summedInsurance:N0})");
            }

            return response;
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
            CompanyTaxId = s.CompanyTaxCode,
            CompanyTaxCode = s.CompanyTaxCode,
            TaxYear = s.TaxYear,
            TotalIncome = s.TotalIncome,
            TaxWithheld = s.TaxWithheld,
            InsuranceDeducted = s.InsuranceDeducted,
            IsActive = s.IsActive,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt
        };

        /// <summary>
        /// Validate MST theo chuẩn Tổng cục Thuế Việt Nam.
        /// Định dạng hợp lệ: 10 chữ số HOẶC 13 chữ số dạng XXXXXXXXXX-XXX.
        /// Checksum: 9 số đầu nhân trọng số [31,29,23,19,17,13,7,5,3] cộng lại,
        /// lấy 10 - (sum % 11) (nếu kết quả >= 10 thì là 0) phải bằng số thứ 10.
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
                    "Định dạng mã số thuế không hợp lệ. MST phải có 10 chữ số (VD: 0101248141) hoặc 13 ký tự có dấu gạch ngang (VD: 0101248141-001).");

            // Checksum for the first 10 digits
            string tenDigits = taxCode.Substring(0, 10);
            int[] weights = { 31, 29, 23, 19, 17, 13, 7, 5, 3 };
            int sum = 0;
            for (int i = 0; i < 9; i++)
                sum += (tenDigits[i] - '0') * weights[i];

            int remainder = sum % 11;
            int checkDigit = 10 - remainder;
            if (checkDigit >= 10)
                checkDigit = 0;
            int actualCheckDigit = tenDigits[9] - '0';

            bool isValid = (checkDigit == actualCheckDigit) || (remainder == 0 && (actualCheckDigit == 0 || actualCheckDigit == 1));

            if (!isValid)
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
                "companytaxid" => desc ? sources.OrderByDescending(s => s.CompanyTaxCode) : sources.OrderBy(s => s.CompanyTaxCode),
                "taxyear" => desc ? sources.OrderByDescending(s => s.TaxYear) : sources.OrderBy(s => s.TaxYear),
                "totalincome" => desc ? sources.OrderByDescending(s => s.TotalIncome) : sources.OrderBy(s => s.TotalIncome),
                "taxwithheld" => desc ? sources.OrderByDescending(s => s.TaxWithheld) : sources.OrderBy(s => s.TaxWithheld),
                "insurancededucted" => desc ? sources.OrderByDescending(s => s.InsuranceDeducted) : sources.OrderBy(s => s.InsuranceDeducted),
                "createdat" => desc ? sources.OrderByDescending(s => s.CreatedAt) : sources.OrderBy(s => s.CreatedAt),
                "updatedat" => desc ? sources.OrderByDescending(s => s.UpdatedAt) : sources.OrderBy(s => s.UpdatedAt),
                _ => sources.OrderByDescending(s => s.CreatedAt) // Default
            };
        }

        #endregion
    }
}
