using FluentValidation;
using TaxKeepVN.Application.DTOs.Bookings;

namespace TaxKeepVN.Application.Validators
{
    public class RejectBookingRequestValidator : AbstractValidator<RejectBookingRequest>
    {
        public RejectBookingRequestValidator()
        {
            RuleFor(x => x.Reason)
                .NotEmpty().WithMessage("Vui lòng cung cấp lý do từ chối tiếp nhận ca tư vấn.")
                .MinimumLength(5).WithMessage("Lý do từ chối phải có ít nhất 5 ký tự.")
                .MaximumLength(500).WithMessage("Lý do từ chối không được vượt quá 500 ký tự.");
        }
    }
}
