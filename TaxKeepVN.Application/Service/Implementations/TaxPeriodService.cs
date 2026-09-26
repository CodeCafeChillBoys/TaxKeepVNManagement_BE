using Microsoft.Extensions.Logging;
using TaxKeepVN.Application.Constants;
using TaxKeepVN.Application.DTOs.TaxPeriods;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Mappers;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    public partial class TaxPeriodService : ITaxPeriodService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _fileStorageService;
        private readonly IDocumentOcrProducerService _ocrProducerService;
        private readonly ILogger<TaxPeriodService> _logger;

        public TaxPeriodService(
            IUnitOfWork unitOfWork,
            IFileStorageService fileStorageService,
            IDocumentOcrProducerService ocrProducerService,
            ILogger<TaxPeriodService> logger)
        {
            _unitOfWork = unitOfWork;
            _fileStorageService = fileStorageService;
            _ocrProducerService = ocrProducerService;
            _logger = logger;
        }

        public async Task<TaxPeriodResponseDto> InitOrGetPeriodAsync(Guid userId, int taxYear)
        {
            int currentYear = DateTime.UtcNow.Year;
            if (taxYear < 2015 || taxYear > currentYear)
            {
                throw new BadRequestException(ErrorCodes.InvalidTaxYear, ErrorMessages.InvalidTaxYear(currentYear));
            }
            var repo = _unitOfWork.Repository<TaxPeriod>();
            // Trước khi tạo mới, kiểm tra xem kỳ kê khai đã tồn tại chưa
            var periods = await repo.FindAsync(p => p.UserId == userId && p.TaxYear == (short)taxYear);
            // Nếu đã tồn tại -> trả về thông tin kỳ kê khai
            var period = periods.FirstOrDefault();
            // Nếu kỳ kê khai đã tồn tại, kiểm tra trạng thái
            if (period != null)
            {
                // Kiểm tra trạng thái nếu kỳ kê khai đã nộp / hoàn tất
                if (string.Equals(period.Status, TaxPeriodStatus.SUBMITTED, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ForbiddenException(ErrorMessages.TaxPeriodSubmittedForYear(taxYear));
                }

                return period.ToResponseDto();
            }

            // Nếu chưa tồn tại -> Tạo mới với trạng thái DRAFT
            var newPeriod = new TaxPeriod
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TaxYear = (short)taxYear,
                Status = TaxPeriodStatus.DRAFT,
                CreatedAt = DateTimeOffset.UtcNow
            };
            await repo.AddAsync(newPeriod);
            await _unitOfWork.SaveChangesAsync();

            return newPeriod.ToResponseDto();
        }

        public async Task<List<TaxPeriodResponseDto>> GetTaxPeriodsAsync(Guid userId)
        {
            var repo = _unitOfWork.Repository<TaxPeriod>();
            var periods = await repo.FindAsync(period => period.UserId == userId);

            return periods
                .OrderByDescending(period => period.TaxYear)
                .Select(period => period.ToResponseDto())
                .ToList();
        }

        public async Task<TaxPeriodResponseDto> SubmitTaxPeriodAsync(Guid userId, Guid periodId)
        {
            var repo = _unitOfWork.Repository<TaxPeriod>();
            var period = await repo.GetByIdAsync(periodId);
            if (period == null || period.UserId != userId)
            {
                throw new NotFoundException(ErrorMessages.TaxPeriodNotFound);
            }
            if (string.Equals(period.Status, TaxPeriodStatus.SUBMITTED, StringComparison.OrdinalIgnoreCase))
            {
                throw new BadRequestException(ErrorCodes.InvalidDocumentStatus, "Kỳ tính thuế này đã được nộp trước đó.");
            }

            period.Status = TaxPeriodStatus.SUBMITTED;
            period.UpdatedAt = DateTimeOffset.UtcNow;
            repo.Update(period);
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("TaxPeriod {PeriodId} submitted successfully by user {UserId}", periodId, userId);
            return period.ToResponseDto();
        }

    }
}