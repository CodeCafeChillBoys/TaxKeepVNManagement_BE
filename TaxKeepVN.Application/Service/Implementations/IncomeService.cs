using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs.Requests.Income;
using TaxKeepVN.Application.DTOs.Responses.Income;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    public class IncomeService : IIncomeService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _fileStorageService;

        public IncomeService(IUnitOfWork unitOfWork, IFileStorageService fileStorageService)
        {
            _unitOfWork = unitOfWork;
            _fileStorageService = fileStorageService;
        }

        public async Task<IncomeResponseDto> CreateIncomeAsync(Guid userId, CreateIncomeRequest request)
        {
            var existingIncome = await _unitOfWork.Repository<Income>().FindAsync(x => 
                x.UserId == userId && 
                x.OrganizationName == request.OrganizationName && 
                x.Month == request.Month && 
                x.Year == request.Year);
                
            if (existingIncome.Any())
            {
                throw new BadRequestException("DUPLICATE_INCOME", $"Đã tồn tại phiếu lương của tổ chức/công ty '{request.OrganizationName}' trong tháng {request.Month}/{request.Year}.");
            }

            string? fileUrl = null;
            if (request.PayslipFile != null && request.PayslipFile.Length > 0)
            {
                fileUrl = await _fileStorageService.SaveFileAsync(request.PayslipFile, "incomes");
            }

            var income = new Income
            {
                UserId = userId,
                OrganizationName = request.OrganizationName,
                TaxIdNumber = request.TaxIdNumber,
                Month = request.Month,
                Year = request.Year,
                TotalTaxableIncome = request.TotalTaxableIncome,
                InsuranceDeducted = request.InsuranceDeducted,
                TaxAlreadyDeducted = request.TaxAlreadyDeducted,
                PayslipFileUrl = fileUrl,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var repository = _unitOfWork.Repository<Income>();
            await repository.AddAsync(income);
            await _unitOfWork.SaveChangesAsync();

            return MapToDto(income);
        }

        public async Task<IncomeResponseDto> GetIncomeByIdAsync(Guid userId, Guid id)
        {
            var repository = _unitOfWork.Repository<Income>();
            var incomes = await repository.FindAsync(i => i.Id == id && i.UserId == userId);
            var income = incomes.FirstOrDefault();

            if (income == null)
            {
                throw new NotFoundException("Không tìm thấy thông tin thu nhập.");
            }

            return MapToDto(income);
        }

        public async Task<List<IncomeGroupedResponseDto>> GetMyIncomesAsync(Guid userId, int year)
        {
            var repository = _unitOfWork.Repository<Income>();
            var incomes = await repository.FindAsync(i => i.UserId == userId && i.Year == year);
            
            var orderedIncomes = incomes.OrderBy(i => i.Month).ToList();

            var grouped = orderedIncomes
                .GroupBy(i => new { i.OrganizationName, i.TaxIdNumber })
                .Select(g => new IncomeGroupedResponseDto
                {
                    OrganizationName = g.Key.OrganizationName,
                    TaxIdNumber = g.Key.TaxIdNumber,
                    TotalIncomeCompany = g.Sum(i => i.TotalTaxableIncome),
                    Details = g.Select(MapToDto).ToList()
                })
                .ToList();

            return grouped;
        }

        public async Task<IncomeResponseDto> UpdateIncomeAsync(Guid userId, Guid id, UpdateIncomeRequest request)
        {
            var repository = _unitOfWork.Repository<Income>();
            var incomes = await repository.FindAsync(i => i.Id == id && i.UserId == userId);
            var income = incomes.FirstOrDefault();

            if (income == null)
            {
                throw new NotFoundException("Không tìm thấy thông tin thu nhập.");
            }

            income.OrganizationName = request.OrganizationName;
            income.TaxIdNumber = request.TaxIdNumber;
            income.Month = request.Month;
            income.Year = request.Year;
            income.TotalTaxableIncome = request.TotalTaxableIncome;
            income.InsuranceDeducted = request.InsuranceDeducted;
            income.TaxAlreadyDeducted = request.TaxAlreadyDeducted;
            income.PayslipFileUrl = request.PayslipFileUrl;
            income.UpdatedAt = DateTime.UtcNow;

            repository.Update(income);
            await _unitOfWork.SaveChangesAsync();

            return MapToDto(income);
        }

        public async Task DeleteIncomeAsync(Guid userId, Guid id)
        {
            var repository = _unitOfWork.Repository<Income>();
            var incomes = await repository.FindAsync(i => i.Id == id && i.UserId == userId);
            var income = incomes.FirstOrDefault();

            if (income == null)
            {
                throw new NotFoundException("Không tìm thấy thông tin thu nhập.");
            }

            repository.Remove(income);
            await _unitOfWork.SaveChangesAsync();
        }

        private IncomeResponseDto MapToDto(Income income)
        {
            return new IncomeResponseDto
            {
                Id = income.Id,
                UserId = income.UserId,
                OrganizationName = income.OrganizationName,
                TaxIdNumber = income.TaxIdNumber,
                Month = income.Month,
                Year = income.Year,
                TotalTaxableIncome = income.TotalTaxableIncome,
                InsuranceDeducted = income.InsuranceDeducted,
                TaxAlreadyDeducted = income.TaxAlreadyDeducted,
                PayslipFileUrl = income.PayslipFileUrl,
                CreatedAt = income.CreatedAt
            };
        }
    }
}
