using FluentValidation;
using TaxKeepVN.Application.DTOs.Documents;

namespace TaxKeepVN.Application.Validators
{
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class UploadDocumentRequestValidator : AbstractValidator<UploadDocumentRequestDto>
    {
        private static readonly string[] AllowedExtensions = { ".pdf", ".jpg", ".jpeg", ".png" };

        public UploadDocumentRequestValidator()
        {
            RuleFor(x => x.DocType)
                .NotEmpty().WithMessage("Loại giấy tờ không được để trống.");

            RuleFor(x => x.File)
                .NotNull().WithMessage("Vui lòng chọn tệp chứng từ cần tải lên.")
                .Must(f => f != null && f.Length > 0).WithMessage("Tệp tải lên không được rỗng.")
                .Must(f => f == null || f.Length <= 10 * 1024 * 1024).WithMessage("Dung lượng tệp vượt quá giới hạn cho phép (tối đa 10MB).")
                .Must(f =>
                {
                    if (f == null) return true;
                    var ext = System.IO.Path.GetExtension(f.FileName)?.ToLowerInvariant();
                    return AllowedExtensions.Contains(ext);
                }).WithMessage("Định dạng tệp không hợp lệ. Hệ thống chỉ hỗ trợ PDF, JPG, JPEG, PNG.");
        }
    }
}
