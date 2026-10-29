using TaxKeepVN.Application.DTOs.TaxPeriods;
using TaxKeepVN.Domain.Entities;

namespace TaxKeepVN.Application.Mappers
{
    public static class TaxPeriodMapper
    {
        public static TaxPeriodResponseDto ToResponseDto(this TaxPeriod period)
        {
            if (period == null) return null!;

            return new TaxPeriodResponseDto
            {
                PeriodId = period.Id,
                TaxYear = period.TaxYear,
                Status = period.Status,
                CreatedAt = period.CreatedAt, 
                UpdatedAt = period.UpdatedAt
            };
        }

        public static List<TaxPeriodResponseDto> ToResponseDtoList(this IEnumerable<TaxPeriod> periods)
        {
            return periods?.Select(p => p.ToResponseDto()).ToList() ?? new List<TaxPeriodResponseDto>();
        }
    }
}
