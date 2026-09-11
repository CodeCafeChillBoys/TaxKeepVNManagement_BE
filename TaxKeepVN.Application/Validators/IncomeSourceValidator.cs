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

            RuleFor(x => x.ResolvedTaxCode)
                .NotEmpty().WithMessage("Mã số thuế tổ chức không được để trống.")
                .Matches(@"^\d{10}$|^\d{10}-\d{3}$")
                .WithMessage("Định dạng MST không hợp lệ. Phải là 10 số hoặc dạng XXXXXXXXXX-XXX.");

            RuleFor(x => x.TaxYear)
                .InclusiveBetween(2000, 2100).WithMessage("Năm tính thuế phải từ 2000 đến 2100.");

            RuleFor(x => x.TotalIncome)
                .GreaterThanOrEqualTo(0).WithMessage("Tổng thu nhập không được âm.");

            RuleFor(x => x.TaxWithheld)
                .GreaterThanOrEqualTo(0).WithMessage("Số thuế đã khấu trừ không được âm.");
        }
    }

    public class IncomeSourceUpdateValidator : AbstractValidator<IncomeSourceUpdateDto>
    {
        public IncomeSourceUpdateValidator()
        {
            RuleFor(x => x.CompanyName)
                .NotEmpty().WithMessage("Tên tổ chức không được để trống.")
                .MaximumLength(255).WithMessage("Tên tổ chức không được vượt quá 255 ký tự.");

            RuleFor(x => x.ResolvedTaxCode)
                .NotEmpty().WithMessage("Mã số thuế tổ chức không được để trống.")
                .Matches(@"^\d{10}$|^\d{10}-\d{3}$")
                .WithMessage("Định dạng MST không hợp lệ. Phải là 10 số hoặc dạng XXXXXXXXXX-XXX.");

            RuleFor(x => x.TaxYear)
                .InclusiveBetween(2000, 2100).WithMessage("Năm tính thuế phải từ 2000 đến 2100.");

            RuleFor(x => x.TotalIncome)
                .GreaterThanOrEqualTo(0).WithMessage("Tổng thu nhập không được âm.");

            RuleFor(x => x.TaxWithheld)
                .GreaterThanOrEqualTo(0).WithMessage("Số thuế đã khấu trừ không được âm.");
        }
    }
}
