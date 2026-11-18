using System;
using FluentValidation;
using TaxKeepVN.Application.DTOs.Consultations;

namespace TaxKeepVN.Application.Validators
{
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class SendConsultationMessageRequestValidator : AbstractValidator<SendConsultationMessageRequest>
    {
        public SendConsultationMessageRequestValidator()
        {
            RuleFor(x => x.Content)
                .NotEmpty().WithMessage("Nội dung tin nhắn không được để trống.");

            When(x => !string.IsNullOrEmpty(x.AttachmentUrl), () =>
            {
                RuleFor(x => x.AttachmentUrl)
                    .NotEmpty().WithMessage("Đường dẫn tệp đính kèm không được để trống khi khai báo.")
                    .Must(uri => Uri.TryCreate(uri, UriKind.Absolute, out _))
                    .WithMessage("Đường dẫn tệp đính kèm phải là URL hợp lệ (ví dụ: https://...).");
            });
        }
    }
}
