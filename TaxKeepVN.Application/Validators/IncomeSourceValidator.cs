using FluentValidation;
using TaxKeepVN.Application.DTOs.IncomeSources;

namespace TaxKeepVN.Application.Validators
{
    public class IncomeSourceCreateValidator : AbstractValidator<IncomeSourceCreateDto>
    {
        public IncomeSourceCreateValidator()
        {
            RuleFor(x => x.CompanyName)
                .NotEmpty().WithMessage("Tên tổ chức không được để trống.")
                .MaximumLength(255).WithMessage("Tên tổ chức không được vượt quá 255 ký tự.");

            RuleFor(x => x.CompanyTaxCode)
                .NotEmpty().WithMessage("Mã số thuế không được để trống.")
                .Matches(@"^\d{10}$|^\d{10}-\d{3}$")
                .WithMessage("Định dạng MST không hợp lệ. Phải là 10 số hoặc dạng XXXXXXXXXX-XXX.");
        }
    }

    public class IncomeSourceUpdateValidator : AbstractValidator<IncomeSourceUpdateDto>
    {
        public IncomeSourceUpdateValidator()
        {
            RuleFor(x => x.CompanyName)
                .NotEmpty().WithMessage("Tên tổ chức không được để trống.")
                .MaximumLength(255).WithMessage("Tên tổ chức không được vượt quá 255 ký tự.");

            RuleFor(x => x.CompanyTaxCode)
                .NotEmpty().WithMessage("Mã số thuế không được để trống.")
                .Matches(@"^\d{10}$|^\d{10}-\d{3}$")
                .WithMessage("Định dạng MST không hợp lệ. Phải là 10 số hoặc dạng XXXXXXXXXX-XXX.");
        }
    }
}
