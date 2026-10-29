using System;
using FluentValidation;
using TaxKeepVN.Application.DTOs.TaxPeriods;

namespace TaxKeepVN.Application.Validators
{
    public class InitTaxPeriodRequestValidator : AbstractValidator<InitTaxPeriodRequest>
    {
        public InitTaxPeriodRequestValidator()
        {
            RuleFor(x => x.TaxYear)
                .NotNull().WithMessage("The taxYear field is required.")
                .Must(y => y.HasValue && y.Value >= 2015 && y.Value <= DateTime.UtcNow.Year)
                .WithMessage($"Invalid tax year. Must be between 2015 and {DateTime.UtcNow.Year}.");
        }
    }
}