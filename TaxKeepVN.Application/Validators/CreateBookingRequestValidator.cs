using FluentValidation;
using TaxKeepVN.Application.DTOs.Bookings;

namespace TaxKeepVN.Application.Validators
{
    public class CreateBookingRequestValidator : AbstractValidator<CreateBookingRequest>
    {
        public CreateBookingRequestValidator()
        {
            RuleFor(x => x.ExpertSlotId)
                .NotEmpty().WithMessage("Vui lòng chọn khung giờ hẹn của chuyên gia.");

            RuleFor(x => x.SpecializationId)
                .GreaterThan(0).WithMessage("Vui lòng chọn chủ đề cần tư vấn hợp lệ từ danh mục.");

            RuleFor(x => x.TopicTitle)
                .NotEmpty().WithMessage("Vui lòng nhập chủ đề / tiêu đề tóm tắt cho buổi tư vấn.")
                .MaximumLength(255).WithMessage("Tiêu đề buổi tư vấn không được vượt quá 255 ký tự.");

            RuleFor(x => x.ProblemDescription)
                .NotEmpty().WithMessage("Vui lòng nhập mô tả chi tiết vấn đề / câu hỏi trọng tâm.")
                .MaximumLength(2000).WithMessage("Mô tả câu hỏi không được vượt quá 2000 ký tự.");
        }
    }
}
