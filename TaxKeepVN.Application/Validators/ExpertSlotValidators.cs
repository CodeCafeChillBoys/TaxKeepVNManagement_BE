using System;
using System.Linq;
using FluentValidation;
using TaxKeepVN.Application.DTOs.Experts;

namespace TaxKeepVN.Application.Validators
{
    public class CreateExpertSlotRequestValidator : AbstractValidator<CreateExpertSlotRequest>
    {
        public CreateExpertSlotRequestValidator()
        {
            RuleFor(x => x.SlotDate)
                .NotEmpty().WithMessage("Vui lòng chọn ngày cho khung giờ tư vấn.")
                .Must(date => date >= DateOnly.FromDateTime(DateTime.UtcNow))
                .WithMessage("Ngày của khung giờ làm việc không được ở trong quá khứ.");

            RuleFor(x => x.EndTime)
                .GreaterThan(x => x.StartTime)
                .WithMessage("Thời gian kết thúc phải lớn hơn thời gian bắt đầu.");

            RuleFor(x => x)
                .Must(x =>
                {
                    var duration = (x.EndTime - x.StartTime).TotalMinutes;
                    return duration >= 15 && duration <= 480;
                })
                .WithMessage("Thời lượng của khung giờ làm việc phải từ 15 phút đến tối đa 8 tiếng.");

            RuleFor(x => x)
                .Must(x =>
                {
                    var nowVietnam = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7));
                    var today = DateOnly.FromDateTime(nowVietnam.DateTime);
                    if (x.SlotDate == today)
                    {
                        var nowTime = TimeOnly.FromDateTime(nowVietnam.DateTime);
                        return x.StartTime > nowTime;
                    }
                    return true;
                })
                .WithMessage("Thời gian bắt đầu của khung giờ trong ngày hôm nay phải ở tương lai.");

            RuleFor(x => x.SessionType)
                .IsInEnum().WithMessage("Hình thức tư vấn không hợp lệ.");
        }
    }

    public class UpdateExpertSlotRequestValidator : AbstractValidator<UpdateExpertSlotRequest>
    {
        public UpdateExpertSlotRequestValidator()
        {
            RuleFor(x => x.SlotDate)
                .NotEmpty().WithMessage("Vui lòng chọn ngày cho khung giờ tư vấn.")
                .Must(date => date >= DateOnly.FromDateTime(DateTime.UtcNow))
                .WithMessage("Ngày của khung giờ làm việc không được ở trong quá khứ.");

            RuleFor(x => x.EndTime)
                .GreaterThan(x => x.StartTime)
                .WithMessage("Thời gian kết thúc phải lớn hơn thời gian bắt đầu.");

            RuleFor(x => x)
                .Must(x =>
                {
                    var duration = (x.EndTime - x.StartTime).TotalMinutes;
                    return duration >= 15 && duration <= 480;
                })
                .WithMessage("Thời lượng của khung giờ làm việc phải từ 15 phút đến tối đa 8 tiếng.");

            RuleFor(x => x.SessionType)
                .IsInEnum().WithMessage("Hình thức tư vấn không hợp lệ.");
        }
    }
}
