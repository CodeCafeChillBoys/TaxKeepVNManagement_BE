using FluentValidation;
using TaxKeepVN.Application.DTOs.Rules;

namespace TaxKeepVN.Application.Validators
{
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class CreateDependentRuleValidator : AbstractValidator<CreateDependentRuleDto>
    {
        public CreateDependentRuleValidator()
        {
            RuleFor(x => x.TargetGroup)
                .NotEmpty().WithMessage("Nhóm đối tượng áp dụng (target_group) không được để trống.")
                .MaximumLength(50).WithMessage("Nhóm đối tượng không được vượt quá 50 ký tự.");

            RuleFor(x => x.DocType)
                .NotEmpty().WithMessage("Loại giấy tờ (doc_type) không được để trống.")
                .MaximumLength(50).WithMessage("Loại giấy tờ không được vượt quá 50 ký tự.")
                .Matches("^[A-Z0-9_]+$").WithMessage("Loại giấy tờ chỉ được chứa ký tự in hoa A-Z, chữ số 0-9 và dấu gạch dưới _.");

            RuleFor(x => x.DocName)
                .MaximumLength(150).WithMessage("Tên loại giấy tờ không được vượt quá 150 ký tự.");

            RuleFor(x => x.Description)
                .MaximumLength(500).WithMessage("Mô tả hướng dẫn không được vượt quá 500 ký tự.");
        }
    }
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]

    public class UpdateDependentRuleValidator : AbstractValidator<UpdateDependentRuleDto>
    {
        public UpdateDependentRuleValidator()
        {
            RuleFor(x => x.DocName)
                .MaximumLength(150).WithMessage("Tên loại giấy tờ không được vượt quá 150 ký tự.");

            RuleFor(x => x.Description)
                .MaximumLength(500).WithMessage("Mô tả hướng dẫn không được vượt quá 500 ký tự.");
        }
    }
}
