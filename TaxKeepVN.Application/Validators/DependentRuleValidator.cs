using FluentValidation;
using TaxKeepVN.Application.DTOs.Rules;

namespace TaxKeepVN.Application.Validators
{
    public class CreateDependentRuleValidator : AbstractValidator<CreateDependentRuleDto>
    {
        public CreateDependentRuleValidator()
        {
            RuleFor(x => x.TargetGroup)
                .NotEmpty().WithMessage("Nhóm đối tượng áp dụng (target_group) không được để trống.")
                .MaximumLength(50).WithMessage("Nhóm đối tượng không được vượt quá 50 ký tự.");

            RuleFor(x => x.DocType)
                .NotEmpty().WithMessage("Loại giấy tờ (doc_type) không được để trống.")
                .MaximumLength(50).WithMessage("Loại giấy tờ không được vượt quá 50 ký tự.");

            RuleFor(x => x.Description)
                .MaximumLength(500).WithMessage("Mô tả hướng dẫn không được vượt quá 500 ký tự.");
        }
    }

    public class UpdateDependentRuleValidator : AbstractValidator<UpdateDependentRuleDto>
    {
        public UpdateDependentRuleValidator()
        {
            RuleFor(x => x.Description)
                .MaximumLength(500).WithMessage("Mô tả hướng dẫn không được vượt quá 500 ký tự.");
        }
    }
}
