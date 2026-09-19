namespace TaxKeepVN.Application.Constants
{
    public static class ErrorCodes
    {
        // Auth & Identity
        public const string InvalidToken = "INVALID_TOKEN";

        // Tax Period & Documents
        public const string InvalidTaxYear = "INVALID_TAX_YEAR";
        public const string FilesRequired = "FILES_REQUIRED";
        public const string UnsupportedFormat = "UNSUPPORTED_FORMAT";
        public const string FileSizeExceeded = "FILE_SIZE_EXCEEDED";
        public const string InvalidDocType = "INVALID_DOC_TYPE";

        // Tax Document Types
        public const string InvalidCode = "INVALID_CODE";
        public const string DuplicateCode = "DUPLICATE_CODE";
    }

    public static class ErrorMessages
    {
        // Auth & Identity
        public const string InvalidToken = "Không thể xác định danh tính người dùng từ token. Vui lòng đăng nhập lại.";

        // Tax Period & Documents
        public static string InvalidTaxYear(int currentYear) => $"Invalid tax year. Must be between 2015 and {currentYear}.";
        public const string TaxPeriodSubmitted = "The tax filing for this year has already been submitted and is locked.";
        public static string TaxPeriodSubmittedForYear(int taxYear) => $"Tax filing for year {taxYear} has been submitted and is locked.";
        public const string TaxPeriodNotFound = "Tax period not found. Please select a valid tax year first.";
        public const string FilesRequired = "At least one document file is required.";
        public const string UnsupportedFormat = "Unsupported file format. Only JPG, PNG, and PDF files are permitted.";
        public const string FileSizeExceeded = "Only JPG, PNG, and PDF files under 10MB are permitted.";
        public const string DocumentNotFound = "Không tìm thấy chứng từ cần duyệt.";
        public static string InvalidDocType(string code) => $"Mã loại chứng từ '{code}' không tồn tại trong hệ thống.";

        // Tax Document Types
        public const string DocTypeCodeRequired = "Mã loại chứng từ không được để trống.";
        public static string DocTypeNotFound(string code) => $"Không tìm thấy loại chứng từ với mã '{code}'.";
        public static string DuplicateDocTypeCode(string code) => $"Mã loại chứng từ '{code}' đã tồn tại trong hệ thống.";
    }

    public static class SuccessMessages
    {
        // Tax Period & Documents
        public const string TaxPeriodInitialized = "Tax year period initialized successfully.";
        public const string DocumentsUploaded = "Documents uploaded successfully.";
        public const string DocumentReviewConfirmed = "Xác nhận và lưu trữ dữ liệu chứng từ thành công.";

        // Tax Document Types
        public const string DocTypeListRetrieved = "Lấy danh sách loại chứng từ thuế thành công.";
        public const string DocTypeDetailRetrieved = "Lấy chi tiết loại chứng từ thuế thành công.";
        public const string DocTypeCreated = "Thêm mới loại chứng từ thuế thành công.";
        public const string DocTypeUpdated = "Cập nhật loại chứng từ thuế thành công.";
    }
}
