using System.IO;
using System.Linq;
using FluentValidation;
using TaxKeepVN.Application.DTOs.Bookings;

namespace TaxKeepVN.Application.Validators
{
    public class CreateBookingRequestValidator : AbstractValidator<CreateBookingRequest>
    {
        private static readonly string[] AllowedExtensions = 
        { 
            ".pdf", ".xlsx", ".xls", ".docx", ".doc", ".png", ".jpg", ".jpeg" 
        };

        private const long MaxSingleFileSize = 10 * 1024 * 1024; // 10MB
        private const long MaxTotalFileSize = 25 * 1024 * 1024;  // 25MB (Đặc tả 6)
        private const int MaxFilesCount = 5;

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

            When(x => x.Attachments != null && x.Attachments.Any(), () =>
            {
                RuleFor(x => x.Attachments!)
                    .Must(files => files.Count <= MaxFilesCount)
                    .WithMessage($"Số lượng tệp đính kèm không được vượt quá {MaxFilesCount} tệp.")
                    .Must(files => files.Sum(f => f.Length) <= MaxTotalFileSize)
                    .WithMessage("Tổng dung lượng các tệp đính kèm vượt quá 25MB theo quy định.")
                    .ForEach(fileRule =>
                    {
                        fileRule
                            .Must(f => f.Length > 0).WithMessage("Tệp tải lên không được rỗng (0 bytes).")
                            .Must(f => f.Length <= MaxSingleFileSize)
                            .WithMessage("Dung lượng mỗi tệp đơn lẻ không được vượt quá 10MB.")
                            .Must(f =>
                            {
                                var ext = Path.GetExtension(f.FileName)?.ToLowerInvariant();
                                return !string.IsNullOrEmpty(ext) && AllowedExtensions.Contains(ext);
                            }).WithMessage($"Định dạng tệp không được hỗ trợ. Chỉ chấp nhận các định dạng: {string.Join(", ", AllowedExtensions)}.");
                    });
            });
        }
    }
}
