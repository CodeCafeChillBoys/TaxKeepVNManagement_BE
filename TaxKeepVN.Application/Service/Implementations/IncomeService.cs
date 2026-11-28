using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using TaxKeepVN.Application.DTOs.OcrAI;
using TaxKeepVN.Application.DTOs.Requests.Income;
using TaxKeepVN.Application.DTOs.Responses.Income;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Helpers;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    public class IncomeService : IIncomeService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _fileStorageService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ISubscriptionQuotaService? _quotaService;

        public IncomeService(
            IUnitOfWork unitOfWork,
            IFileStorageService fileStorageService,
            IHttpClientFactory httpClientFactory,
            ISubscriptionQuotaService? quotaService = null)
        {
            _unitOfWork = unitOfWork;
            _fileStorageService = fileStorageService;
            _httpClientFactory = httpClientFactory;
            _quotaService = quotaService;
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

            if (request.PayslipFile != null && request.PayslipFile.Length > 0)
            {
                var user = await _unitOfWork.Repository<User>().GetByIdAsync(userId);
                if (user == null)
                {
                    throw new NotFoundException("Không tìm thấy tài khoản người nộp thuế.");
                }

                if (string.IsNullOrWhiteSpace(user.FullName))
                {
                    throw new BadRequestException("ACCOUNT_NAME_REQUIRED", "Tài khoản chưa có họ tên để đối chiếu với phiếu lương.");
                }

                var employeeName = await ExtractEmployeeNameAsync(userId, request.PayslipFile, request.Month, request.Year);
                if (string.IsNullOrWhiteSpace(employeeName))
                {
                    throw new BadRequestException("PAYSLIP_NAME_NOT_FOUND", "Không đọc được họ tên người lao động trên phiếu lương.");
                }

                if (!StringComparisonHelper.IsNameMatching(user.FullName, employeeName))
                {
                    throw new BadRequestException("PAYSLIP_NAME_MISMATCH",
                        $"Họ tên trên phiếu lương ('{employeeName}') không khớp với họ tên tài khoản ('{user.FullName}').");
                }
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

        private async Task<string?> ExtractEmployeeNameAsync(Guid userId, IFormFile payslipFile, int month, int year)
        {
            if (_quotaService != null)
            {
                await _quotaService.CheckAndConsumeOcrScanAsync(userId);
            }

            var client = _httpClientFactory.CreateClient("TaxAIService");
            using var content = new MultipartFormDataContent();
            using var stream = payslipFile.OpenReadStream();
            using var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.TryParse(payslipFile.ContentType, out var mediaType)
                ? mediaType
                : new MediaTypeHeaderValue("application/octet-stream");
            content.Add(fileContent, "file", payslipFile.FileName);
            content.Add(new StringContent(month.ToString(CultureInfo.InvariantCulture)), "target_month");
            content.Add(new StringContent(year.ToString(CultureInfo.InvariantCulture)), "target_year");

            HttpResponseMessage response;
            try
            {
                response = await client.PostAsync("/api/incomes/ocr/extract", content);
            }
            catch (HttpRequestException)
            {
                throw new BadGatewayException("AI_SERVICE_UNAVAILABLE", "Không thể kết nối dịch vụ OCR phiếu lương.");
            }
            catch (TaskCanceledException)
            {
                throw new BadGatewayException("AI_SERVICE_UNAVAILABLE", "Dịch vụ OCR phiếu lương phản hồi quá thời gian.");
            }

            using (response)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    throw new BadGatewayException("AI_SERVICE_UNAVAILABLE", "Dịch vụ OCR phiếu lương không thể xử lý tệp.");
                }

                IncomeOcrResponseDto? ocrResponse;
                try
                {
                    ocrResponse = JsonSerializer.Deserialize<IncomeOcrResponseDto>(
                        responseJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch (JsonException)
                {
                    throw new BadGatewayException("AI_SERVICE_INVALID_RESPONSE", "Dịch vụ OCR phiếu lương trả về dữ liệu không hợp lệ.");
                }

                if (ocrResponse == null || !ocrResponse.Success || ocrResponse.Data == null)
                {
                    throw new BadRequestException("PAYSLIP_OCR_FAILED", "Không thể trích xuất thông tin từ phiếu lương đã tải lên.");
                }

                return ocrResponse.Data.EmployeeName?.Trim();
            }
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
