using FluentValidation;
using TaxKeepVN.Application.DTOs.Documents;

namespace TaxKeepVN.Application.Validators
{
    public class UploadDocumentRequestValidator : AbstractValidator<UploadDocumentRequestDto>
    {
        private static readonly string[] AllowedExtensions = { ".pdf", ".jpg", ".jpeg", ".png" };
        private static readonly string[] AllowedDocTypes =
        {
            "BIRTH_CERTIFICATE", "CITIZEN_ID", "STUDENT_CARD",
            "DISABILITY_CERTIFICATE", "MARRIAGE_CERTIFICATE",
            "RELATIONSHIP_CERTIFICATE", "SUPPORT_COMMITMENT_FORM", "OTHER"
        };

        public UploadDocumentRequestValidator()
        {
            RuleFor(x => x.DocType)
                .NotEmpty().WithMessage("Loại giấy tờ không được để trống.")
                .Must(t => AllowedDocTypes.Contains(t?.ToUpper()))
                .WithMessage($"Loại giấy tờ không hợp lệ. Các loại hợp lệ: {string.Join(", ", AllowedDocTypes)}.");

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
