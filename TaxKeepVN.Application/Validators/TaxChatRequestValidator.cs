using FluentValidation;
using TaxKeepVN.Application.DTOs.TaxAI;

namespace TaxKeepVN.Application.Validators
{
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class TaxChatRequestValidator : AbstractValidator<TaxChatRequest>
    {
        public TaxChatRequestValidator()
        {
            RuleFor(x => x.Question)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage("Vui lòng nhập câu hỏi.")
                .Must(q => !string.IsNullOrWhiteSpace(q)).WithMessage("Vui lòng nhập câu hỏi.");
        }
    }
}
