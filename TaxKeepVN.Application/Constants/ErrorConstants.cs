namespace TaxKeepVN.Application.Constants
{
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public static class ErrorCodes
    {
        #region Backward Compatibility (Legacy Root Constants)
        // Giữ nguyên 12 hằng số cũ ở root để đảm bảo 100% tương thích ngược
        public const string InvalidToken = "INVALID_TOKEN";
        public const string InvalidTaxYear = "INVALID_TAX_YEAR";
        public const string FilesRequired = "FILES_REQUIRED";
        public const string UnsupportedFormat = "UNSUPPORTED_FORMAT";
        public const string FileSizeExceeded = "FILE_SIZE_EXCEEDED";
        public const string InvalidDocType = "INVALID_DOC_TYPE";
        public const string NonTaxDocumentType = "NON_TAX_DOCUMENT_TYPE";
        public const string InvalidDocumentStatus = "ERR_INVALID_STATUS";
        public const string DuplicateDocument = "ERR_DUPLICATE_DOCUMENT";
        public const string IdentityMismatch = "ERR_IDENTITY_MISMATCH";
        public const string InvalidCode = "INVALID_CODE";
        public const string DuplicateCode = "DUPLICATE_CODE";
        #endregion

        /// <summary>
        /// Mã lỗi phân hệ: AiOcr
        /// </summary>
        public static class AiOcr
        {
            public const string AiConnectionError = "AI_CONNECTION_ERROR";
            public const string AiQuestionQuotaExceeded = "AI_QUESTION_QUOTA_EXCEEDED";
            public const string AiServiceError = "AI_SERVICE_ERROR";
            public const string ErrConsumerException = "ERR_CONSUMER_EXCEPTION";
            public const string ErrInternalAi = "ERR_INTERNAL_AI";
            public const string ErrInvalidFile = "ERR_INVALID_FILE";
            public const string ErrMissingName = "ERR_MISSING_NAME";
            public const string ErrNotACertificate = "ERR_NOT_A_CERTIFICATE";
            public const string IdentityMismatch = "IDENTITY_MISMATCH";
            public const string InvalidFile = "INVALID_FILE";
            public const string OcrExtractionFailed = "OCR_EXTRACTION_FAILED";
            public const string OcrNotWired = "OCR_NOT_WIRED";
            public const string OcrQuotaExceeded = "OCR_QUOTA_EXCEEDED";
            public const string OcrServiceError = "OCR_SERVICE_ERROR";
            public const string QuotaExceeded = "QUOTA_EXCEEDED";
            public const string TaskNotFoundOrProcessing = "TASK_NOT_FOUND_OR_PROCESSING";
        }

        /// <summary>
        /// Mã lỗi phân hệ: Auth
        /// </summary>
        public static class Auth
        {
            public const string AccountSuspended = "ACCOUNT_SUSPENDED";
            public const string CitizenIdAlreadyExists = "CITIZEN_ID_ALREADY_EXISTS";
            public const string EmailAlreadyExists = "EMAIL_ALREADY_EXISTS";
            public const string InvalidCitizenId = "INVALID_CITIZEN_ID";
            public const string InvalidCredentials = "INVALID_CREDENTIALS";
            public const string InvalidToken = "INVALID_TOKEN";
            public const string MissingToken = "MISSING_TOKEN";
            public const string NewPasswordsDoNotMatch = "NEW_PASSWORDS_DO_NOT_MATCH";
            public const string PasswordCannotBeIdentical = "PASSWORD_CANNOT_BE_IDENTICAL";
            public const string SamePassword = "SAME_PASSWORD";
            public const string TokenExpiredOrInvalid = "TOKEN_EXPIRED_OR_INVALID";
            public const string UnreadableImage = "UNREADABLE_IMAGE";
            public const string WrongCurrentPassword = "WRONG_CURRENT_PASSWORD";
        }

        /// <summary>
        /// Mã lỗi phân hệ: Booking
        /// </summary>
        public static class Booking
        {
            public const string ActiveApplicationExists = "ACTIVE_APPLICATION_EXISTS";
            public const string BookingApprovalExpiredRefunded = "BOOKING_APPROVAL_EXPIRED_REFUNDED";
            public const string CannotCancel = "CANNOT_CANCEL";
            public const string CertificateExpired = "CERTIFICATE_EXPIRED";
            public const string CertificateRequired = "CERTIFICATE_REQUIRED";
            public const string ExpertAlreadyCheckedIn = "EXPERT_ALREADY_CHECKED_IN";
            public const string FeeConfigExists = "FEE_CONFIG_EXISTS";
            public const string FeeProposalRequired = "FEE_PROPOSAL_REQUIRED";
            public const string FullnameMismatch = "FULLNAME_MISMATCH";
            public const string GatewayError = "GATEWAY_ERROR";
            public const string HoldSlotExpired = "HOLD_SLOT_EXPIRED";
            public const string InsufficientWalletBalance = "INSUFFICIENT_WALLET_BALANCE";
            public const string InvalidBookingStatus = "INVALID_BOOKING_STATUS";
            public const string InvalidFeeRange = "INVALID_FEE_RANGE";
            public const string InvalidReason = "INVALID_REASON";
            public const string InvalidSpecialization = "INVALID_SPECIALIZATION";
            public const string InvalidStatus = "INVALID_STATUS";
            public const string LeadTimeViolation = "LEAD_TIME_VIOLATION";
            public const string MaxDailySlotsExceeded = "MAX_DAILY_SLOTS_EXCEEDED";
            public const string MissingBookingId = "MISSING_BOOKING_ID";
            public const string NotesRequired = "NOTES_REQUIRED";
            public const string NotFound = "NOT_FOUND";
            public const string NoShowEarly = "NO_SHOW_EARLY";
            public const string ReasonRequired = "REASON_REQUIRED";
            public const string RoomNotOpenYet = "ROOM_NOT_OPEN_YET";
            public const string SelfBookingNotAllowed = "SELF_BOOKING_NOT_ALLOWED";
            public const string SlotAlreadyBooked = "SLOT_ALREADY_BOOKED";
            public const string SlotAlreadyTaken = "SLOT_ALREADY_TAKEN";
            public const string SlotExpired = "SLOT_EXPIRED";
            public const string SlotHolding = "SLOT_HOLDING";
            public const string SlotOverlap = "SLOT_OVERLAP";
            public const string UnderDispute = "UNDER_DISPUTE";
            public const string UnpaidBooking = "UNPAID_BOOKING";
            public const string UserAlreadyCheckedIn = "USER_ALREADY_CHECKED_IN";
            public const string UserNotActive = "USER_NOT_ACTIVE";
            public const string UserSlotConflict = "USER_SLOT_CONFLICT";
            public const string WrongTaxLawAdvice = "WRONG_TAX_LAW_ADVICE";
        }

        /// <summary>
        /// Mã lỗi phân hệ: Common
        /// </summary>
        public static class Common
        {
            public const string DuplicateCode = "DUPLICATE_CODE";
            public const string InvalidCancelHours = "INVALID_CANCEL_HOURS";
            public const string SessionNotFound = "SESSION_NOT_FOUND";
        }

        /// <summary>
        /// Mã lỗi phân hệ: Dependent
        /// </summary>
        public static class Dependent
        {
            public const string Already18PendingAction = "ALREADY_18_PENDING_ACTION";
            public const string DependentLimitExceeded = "DEPENDENT_LIMIT_EXCEEDED";
            public const string DependentPeriodOverlap = "DEPENDENT_PERIOD_OVERLAP";
            public const string DuplicateRule = "DUPLICATE_RULE";
            public const string ErrNetwork = "ERR_NETWORK";
            public const string FileMissing = "FILE_MISSING";
            public const string FileSizeExceeded = "FILE_SIZE_EXCEEDED";
            public const string GroupAgeMismatch = "GROUP_AGE_MISMATCH";
            public const string GroupDowngradeNotAllowed = "GROUP_DOWNGRADE_NOT_ALLOWED";
            public const string GroupRelationshipMismatch = "GROUP_RELATIONSHIP_MISMATCH";
            public const string IdentifierRequired = "IDENTIFIER_REQUIRED";
            public const string IdentityChangeRequiresReverify = "IDENTITY_CHANGE_REQUIRES_REVERIFY";
            public const string InvalidCurrentGroup = "INVALID_CURRENT_GROUP";
            public const string InvalidDocType = "INVALID_DOC_TYPE";
            public const string InvalidFileType = "INVALID_FILE_TYPE";
            public const string InvalidGroup = "INVALID_GROUP";
            public const string InvalidPeriod = "INVALID_PERIOD";
            public const string InvalidRelationship = "INVALID_RELATIONSHIP";
            public const string InvalidTaxYear = "INVALID_TAX_YEAR";
            public const string PeriodBeforeBirth = "PERIOD_BEFORE_BIRTH";
            public const string RulesNotConfigured = "RULES_NOT_CONFIGURED";
            public const string SameGroup = "SAME_GROUP";
            public const string SettlementLockedPeriod = "SETTLEMENT_LOCKED_PERIOD";
            public const string UnreadableDocument = "UNREADABLE_DOCUMENT";
            public const string UseChangeGroupApi = "USE_CHANGE_GROUP_API";
        }

        /// <summary>
        /// Mã lỗi phân hệ: Disbursement
        /// </summary>
        public static class Disbursement
        {
            public const string AlreadyProcessed = "ALREADY_PROCESSED";
            public const string BankInfoRequired = "BANK_INFO_REQUIRED";
            public const string DescriptionTooShort = "DESCRIPTION_TOO_SHORT";
            public const string DisputeAlreadyResolved = "DISPUTE_ALREADY_RESOLVED";
            public const string DisputeNotAllowed = "DISPUTE_NOT_ALLOWED";
            public const string EkycRequired = "EKYC_REQUIRED";
            public const string EscrowNotFound = "ESCROW_NOT_FOUND";
            public const string InsufficientBalance = "INSUFFICIENT_BALANCE";
            public const string InvalidAction = "INVALID_ACTION";
            public const string InvalidAmount = "INVALID_AMOUNT";
            public const string InvalidDecisionType = "INVALID_DECISION_TYPE";
            public const string InvalidRefundRate = "INVALID_REFUND_RATE";
            public const string InvalidSignature = "INVALID_SIGNATURE";
            public const string MinWithdrawalNotMet = "MIN_WITHDRAWAL_NOT_MET";
            public const string ResolutionRequired = "RESOLUTION_REQUIRED";
            public const string ResponseTooShort = "RESPONSE_TOO_SHORT";
            public const string TaxIdRequired = "TAX_ID_REQUIRED";
            public const string TopupFailed = "TOPUP_FAILED";
        }

        /// <summary>
        /// Mã lỗi phân hệ: Expert
        /// </summary>
        public static class Expert
        {
            public const string CannotDeleteInUse = "CANNOT_DELETE_IN_USE";
            public const string CannotModify = "CANNOT_MODIFY";
            public const string CertificateConfigExists = "CERTIFICATE_CONFIG_EXISTS";
            public const string EmptyFile = "EMPTY_FILE";
            public const string ExpiryDateRequired = "EXPIRY_DATE_REQUIRED";
            public const string ExpiryRequiredForType = "EXPIRY_REQUIRED_FOR_TYPE";
            public const string FileTooLarge = "FILE_TOO_LARGE";
            public const string InvalidCertificateType = "INVALID_CERTIFICATE_TYPE";
            public const string InvalidCode = "INVALID_CODE";
            public const string InvalidExpiryDate = "INVALID_EXPIRY_DATE";
            public const string InvalidIssueDate = "INVALID_ISSUE_DATE";
            public const string MissingRequiredFields = "MISSING_REQUIRED_FIELDS";
            public const string SpecializationCodeExists = "SPECIALIZATION_CODE_EXISTS";
            public const string SpecializationRequired = "SPECIALIZATION_REQUIRED";
        }

        /// <summary>
        /// Mã lỗi phân hệ: Review
        /// </summary>
        public static class Review
        {
            public const string AlreadyReplied = "ALREADY_REPLIED";
            public const string CommentTooShort = "COMMENT_TOO_SHORT";
            public const string EmptyReply = "EMPTY_REPLY";
            public const string InvalidDetailScore = "INVALID_DETAIL_SCORE";
            public const string InvalidRating = "INVALID_RATING";
            public const string ReviewNotAllowed = "REVIEW_NOT_ALLOWED";
        }

        /// <summary>
        /// Mã lỗi phân hệ: Subscription
        /// </summary>
        public static class Subscription
        {
            public const string AlreadyRefunded = "ALREADY_REFUNDED";
            public const string CannotDeleteBasicPlan = "CANNOT_DELETE_BASIC_PLAN";
            public const string CannotExtendFreePlan = "CANNOT_EXTEND_FREE_PLAN";
            public const string CannotRefundFreePlan = "CANNOT_REFUND_FREE_PLAN";
            public const string ExportPdfNotAllowed = "EXPORT_PDF_NOT_ALLOWED";
            public const string ExportZipNotAllowed = "EXPORT_ZIP_NOT_ALLOWED";
            public const string InvalidBillingCycle = "INVALID_BILLING_CYCLE";
            public const string InvalidRefundAmount = "INVALID_REFUND_AMOUNT";
            public const string SlugAlreadyExists = "SLUG_ALREADY_EXISTS";
            public const string SubscriptionExpired = "SUBSCRIPTION_EXPIRED";
            public const string SubscriptionNotActive = "SUBSCRIPTION_NOT_ACTIVE";
        }

        /// <summary>
        /// Mã lỗi phân hệ: SystemConfig
        /// </summary>
        public static class SystemConfig
        {
            public const string AlreadyExists = "ALREADY_EXISTS";
            public const string TaxRegistrationConfirmationRequired = "TAX_REGISTRATION_CONFIRMATION_REQUIRED";
        }

        /// <summary>
        /// Mã lỗi phân hệ: TaxDocument
        /// </summary>
        public static class TaxDocument
        {
            public const string DocTypeMismatch = "DOC_TYPE_MISMATCH";
            public const string DuplicateDocument = "DUPLICATE_DOCUMENT";
            public const string DuplicateIncome = "DUPLICATE_INCOME";
            public const string DuplicateTaxCode = "DUPLICATE_TAX_CODE";
            public const string ErrAiApi = "ERR_AI_API";
            public const string ErrAiValidation = "ERR_AI_VALIDATION";
            public const string ErrCorruptedFile = "ERR_CORRUPTED_FILE";
            public const string ErrDocumentNotFound = "ERR_DOCUMENT_NOT_FOUND";
            public const string ErrDuplicateDocument = "ERR_DUPLICATE_DOCUMENT";
            public const string ErrDuplicateFileHash = "ERR_DUPLICATE_FILE_HASH";
            public const string ErrFutureDate = "ERR_FUTURE_DATE";
            public const string ErrIdentityMismatch = "ERR_IDENTITY_MISMATCH";
            public const string ErrImageQualityLow = "ERR_IMAGE_QUALITY_LOW";
            public const string ErrImageQualityTooLow = "ERR_IMAGE_QUALITY_TOO_LOW";
            public const string ErrInternalServer = "ERR_INTERNAL_SERVER";
            public const string ErrInvalidDocType = "ERR_INVALID_DOC_TYPE";
            public const string ErrInvalidMonth = "ERR_INVALID_MONTH";
            public const string ErrInvalidStatus = "ERR_INVALID_STATUS";
            public const string ErrNotIncomeDocument = "ERR_NOT_INCOME_DOCUMENT";
            public const string ErrNotTaxDocument = "ERR_NOT_TAX_DOCUMENT";
            public const string ErrNoFieldsExtracted = "ERR_NO_FIELDS_EXTRACTED";
            public const string ErrSystem = "ERR_SYSTEM";
            public const string ErrTaxPeriodLocked = "ERR_TAX_PERIOD_LOCKED";
            public const string ErrUnauthorized = "ERR_UNAUTHORIZED";
            public const string ErrUnreadable = "ERR_UNREADABLE";
            public const string ErrUnreadableImage = "ERR_UNREADABLE_IMAGE";
            public const string ErrYearMismatch = "ERR_YEAR_MISMATCH";
            public const string FilesRequired = "FILES_REQUIRED";
            public const string IncomeSourceLimitExceeded = "INCOME_SOURCE_LIMIT_EXCEEDED";
            public const string InternalServerError = "INTERNAL_SERVER_ERROR";
            public const string InvalidTaxCode = "INVALID_TAX_CODE";
            public const string InvalidTaxCodeChecksum = "INVALID_TAX_CODE_CHECKSUM";
            public const string InvalidTaxCodeFormat = "INVALID_TAX_CODE_FORMAT";
            public const string NotTaxDocument = "NOT_TAX_DOCUMENT";
            public const string TaxRuleSetExists = "TAX_RULE_SET_EXISTS";
        }

        /// <summary>
        /// Mã lỗi phân hệ: TaxSettlement
        /// </summary>
        public static class TaxSettlement
        {
            public const string FileNotFound = "FILE_NOT_FOUND";
            public const string InvalidBracket = "INVALID_BRACKET";
            public const string InvalidCutoffDate = "INVALID_CUTOFF_DATE";
            public const string InvalidDeadlineDate = "INVALID_DEADLINE_DATE";
            public const string ValidationError = "VALIDATION_ERROR";
        }

    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public static class ErrorMessages
    {
        // Auth & Identity
        public const string InvalidToken = "Không thể xác định danh tính người dùng từ token. Vui lòng đăng nhập lại.";
        public const string AccountSuspended = "Tài khoản đã bị tạm khóa hoặc ngừng kích hoạt.";
        public const string InvalidCredentials = "Số điện thoại, CCCD hoặc mật khẩu không chính xác.";
        public const string WrongCurrentPassword = "Mật khẩu hiện tại không đúng.";
        public const string SamePassword = "Mật khẩu mới không được trùng với mật khẩu cũ.";

        // Tax Period & Documents
        public static string InvalidTaxYear(int currentYear) => $"Invalid tax year. Must be between 2015 and {currentYear}.";
        public const string TaxPeriodSubmitted = "The tax filing for this year has already been submitted and is locked.";
        public static string TaxPeriodSubmittedForYear(int taxYear) => $"Tax filing for year {taxYear} has been submitted and is locked.";
        public const string TaxPeriodNotFound = "Tax period not found. Please select a valid tax year first.";
        public const string FilesRequired = "At least one document file is required.";
        public const string UnsupportedFormat = "Unsupported file format. Only JPG, PNG, and PDF files are permitted.";
        public const string FileSizeExceeded = "Only JPG, PNG, and PDF files under 10MB are permitted.";
        public const string DocumentNotFound = "Không tìm thấy chứng từ cần duyệt.";
        public const string DocumentAlreadyVerified = "Document has already been extracted or verified.";
        public static string DuplicateDocumentExists(string invNum, string taxCode) => $"Duplicate document: Invoice number {invNum} from seller {taxCode} already exists.";
        public static string IdentityMismatch(string buyer) => $"Thông tin người mua trên chứng từ ('{buyer}') không trùng khớp với Người nộp thuế hoặc bất kỳ Người phụ thuộc nào đã đăng ký.";
        public static string InvalidDocType(string code) => $"Mã loại chứng từ '{code}' không tồn tại trong hệ thống.";
        public static string NonTaxDocumentType(string code) => $"Loại chứng từ '{code}' không thuộc nhóm chứng từ được phép kê khai thuế.";

        // Tax Document Types
        public const string DocTypeCodeRequired = "Mã loại chứng từ không được để trống.";
        public static string DocTypeNotFound(string code) => $"Không tìm thấy loại chứng từ với mã '{code}'.";
        public static string DuplicateDocTypeCode(string code) => $"Mã loại chứng từ '{code}' đã tồn tại trong hệ thống.";

        // Booking & Consultations
        public const string SlotOverlap = "Khung giờ tư vấn này bị trùng lặp với lịch hẹn khác.";
        public const string SlotAlreadyBooked = "Khung giờ tư vấn này đã có khách hàng đặt hẹn.";
        public const string HoldSlotExpired = "Thời gian giữ chỗ slot tư vấn đã hết hạn (10 phút).";
        public const string LeadTimeViolation = "Lịch tư vấn phải được đặt trước tối thiểu 2 giờ.";
        public const string SelfBookingNotAllowed = "Chuyên gia không được tự đặt lịch hẹn của chính mình.";
        public const string InsufficientWalletBalance = "Số dư khả dụng trong ví không đủ để thanh toán.";
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public static class SuccessMessages
    {
        // Tax Period & Documents
        public const string TaxPeriodInitialized = "Tax year period initialized successfully.";
        public const string DocumentsUploaded = "Documents uploaded successfully.";
        public const string DocumentOcrTriggered = "Yêu cầu bóc tách lại OCR chứng từ đã được gửi thành công.";
        public const string DocumentReviewConfirmed = "Xác nhận và lưu trữ dữ liệu chứng từ thành công.";
        public const string DocumentDeleted = "Xóa chứng từ chưa xác nhận thành công.";

        // Tax Document Types
        public const string DocTypeListRetrieved = "Lấy danh sách loại chứng từ thuế thành công.";
        public const string DocTypeDetailRetrieved = "Lấy chi tiết loại chứng từ thuế thành công.";
        public const string DocTypeCreated = "Thêm mới loại chứng từ thuế thành công.";
        public const string DocTypeUpdated = "Cập nhật loại chứng từ thuế thành công.";
    }
}