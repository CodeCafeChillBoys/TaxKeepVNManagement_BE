using Microsoft.Extensions.Logging;
using TaxKeepVN.Application.DTOs.ExpertApplications;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Helpers;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    /// <summary>
    /// Partial class xử lý luồng Đăng ký chuyên gia của Ứng viên (Bước 1 -> Bước 3)
    /// </summary>
    public partial class ExpertApplicationService : IExpertApplicationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _fileStorageService;
        private readonly ILogger<ExpertApplicationService> _logger;

        public ExpertApplicationService(
            IUnitOfWork unitOfWork,
            IFileStorageService fileStorageService,
            ILogger<ExpertApplicationService> logger)
        {
            _unitOfWork = unitOfWork;
            _fileStorageService = fileStorageService;
            _logger = logger;
        }

        // ── 1. TẠO HOẶC CẬP NHẬT BẢN NHÁP HỒ SƠ (Draft) ─────────────────────
        public async Task<ExpertApplicationDetailResponse> SaveDraftAsync(Guid userId, SaveExpertApplicationDraftRequest request)
        {
            // Kiểm tra trạng thái tài khoản user (BR-01)
            var user = await GetActiveUserOrThrowAsync(userId);

            // Kiểm tra xem đã có hồ sơ đang chờ xét duyệt hay chưa (BR-02)
            var pendingApps = await _unitOfWork.Repository<ExpertApplication>().FindAsync(
                a => a.UserId == userId && a.Status == ExpertApplicationStatus.PendingReview
            );
            if (pendingApps.Any())
            {
                throw new BadRequestException(
                    "ACTIVE_APPLICATION_EXISTS",
                    "Bạn đã có hồ sơ đang trong quá trình xét duyệt (PendingReview). Không thể chỉnh sửa hoặc tạo bản nháp mới."
                );
            }

            // Kiểm tra tính nhất quán họ tên: Ứng viên không được đăng ký dưới tên người khác (phải trùng khớp với User profile)
            var targetFullName = request.FullName?.Trim();
            if (string.IsNullOrWhiteSpace(targetFullName))
            {
                targetFullName = user.FullName;
            }
            else if (!string.IsNullOrWhiteSpace(user.FullName) && !StringComparisonHelper.IsNameMatching(user.FullName, targetFullName))
            {
                throw new BadRequestException(
                    "FULLNAME_MISMATCH",
                    $"Họ và tên đăng ký ('{targetFullName}') không trùng khớp với họ và tên trên tài khoản hệ thống ('{user.FullName}'). Vui lòng kiểm tra lại."
                );
            }

            // Tìm hồ sơ có thể chỉnh sửa hiện tại: Draft hoặc NeedSupplement
            var editableApps = await _unitOfWork.Repository<ExpertApplication>().FindAsync(
                a => a.UserId == userId && (a.Status == ExpertApplicationStatus.Draft || a.Status == ExpertApplicationStatus.NeedSupplement)
            );
            var app = editableApps.OrderByDescending(a => a.UpdatedAt).FirstOrDefault();

            if (app == null)
            {
                // Tạo mới bản nháp
                app = new ExpertApplication
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    ApplicationNumber = GenerateApplicationNumber(),
                    FullName = targetFullName,
                    AvatarUrl = request.AvatarUrl,
                    JobTitle = request.JobTitle,
                    CompanyName = request.CompanyName,
                    Bio = request.Bio,
                    YearsOfExperience = request.YearsOfExperience,
                    CurrentPosition = request.CurrentPosition,
                    ExperienceDescription = request.ExperienceDescription,
                    Status = ExpertApplicationStatus.Draft,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };

                await _unitOfWork.Repository<ExpertApplication>().AddAsync(app);
            }
            else
            {
                // Cập nhật thông tin bản nháp có sẵn
                app.FullName = targetFullName;
                if (!string.IsNullOrWhiteSpace(request.AvatarUrl))
                {
                    app.AvatarUrl = request.AvatarUrl;
                }
                app.JobTitle = request.JobTitle;
                app.CompanyName = request.CompanyName;
                app.Bio = request.Bio;
                app.YearsOfExperience = request.YearsOfExperience;
                app.CurrentPosition = request.CurrentPosition;
                app.ExperienceDescription = request.ExperienceDescription;
                app.UpdatedAt = DateTimeOffset.UtcNow;

                _unitOfWork.Repository<ExpertApplication>().Update(app);
            }

            // Lưu danh sách lĩnh vực chuyên môn (nếu có gửi lên)
            if (request.SpecializationIds != null && request.SpecializationIds.Any())
            {
                // Lấy lên chuyên môn có status là active
                var allActiveSpecs = await _unitOfWork.Repository<Specialization>().FindAsync(s => s.IsActive);
                var activeSpecIds = allActiveSpecs.Select(s => s.Id).ToList();
                var invalidIds = request.SpecializationIds.Where(id => !activeSpecIds.Contains(id)).ToList();
                if (invalidIds.Any())
                {
                    throw new BadRequestException(
                        "INVALID_SPECIALIZATION",
                        $"Lĩnh vực chuyên môn không hợp lệ hoặc đã ngừng áp dụng: {string.Join(", ", invalidIds)}"
                    );
                }

                // Xóa danh sách cũ
                var oldSpecs = await _unitOfWork.Repository<ExpertApplicationSpecialization>().FindAsync(s => s.ApplicationId == app.Id);
                foreach (var old in oldSpecs)
                {
                    _unitOfWork.Repository<ExpertApplicationSpecialization>().Remove(old);
                }

                // Thêm danh sách mới
                foreach (var specId in request.SpecializationIds.Distinct())
                {
                    await _unitOfWork.Repository<ExpertApplicationSpecialization>().AddAsync(new ExpertApplicationSpecialization
                    {
                        ApplicationId = app.Id,
                        SpecializationId = specId
                    });
                }
            }

            // Lưu danh sách đề xuất mức phí tư vấn (nếu có gửi lên)
            if (request.FeeProposals != null && request.FeeProposals.Any())
            {
                var oldProposals = await _unitOfWork.Repository<ExpertApplicationFeeProposal>().FindAsync(p => p.ApplicationId == app.Id);
                foreach (var old in oldProposals)
                {
                    _unitOfWork.Repository<ExpertApplicationFeeProposal>().Remove(old);
                }

                foreach (var feeDto in request.FeeProposals)
                {
                    await _unitOfWork.Repository<ExpertApplicationFeeProposal>().AddAsync(new ExpertApplicationFeeProposal
                    {
                        Id = Guid.NewGuid(),
                        ApplicationId = app.Id,
                        SessionType = feeDto.SessionType,
                        DurationMinutes = feeDto.DurationMinutes,
                        ProposedFee = feeDto.ProposedFee
                    });
                }
            }

            await _unitOfWork.SaveChangesAsync();

            return await BuildApplicationDetailResponseAsync(app);
        }

        // ── 1.1 TẢI LÊN ẢNH CHÂN DUNG / AVATAR (Mobile Camera & Thư viện ảnh) ─
        public async Task<UploadAvatarResponseDto> UploadAvatarAsync(Guid userId, UploadAvatarRequest request)
        {
            var user = await GetActiveUserOrThrowAsync(userId);

            if (request.File == null || request.File.Length == 0)
            {
                throw new BadRequestException("EMPTY_FILE", "File ảnh đại diện không được để trống.");
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".heic" };
            var extension = Path.GetExtension(request.File.FileName).ToLower();
            if (!allowedExtensions.Contains(extension))
            {
                throw new BadRequestException("INVALID_FILE_TYPE", "Chỉ hỗ trợ file ảnh định dạng JPG, JPEG, PNG, WEBP hoặc HEIC.");
            }

            if (request.File.Length > 10 * 1024 * 1024)
            {
                throw new BadRequestException("FILE_TOO_LARGE", "Dung lượng file ảnh đại diện không được vượt quá 10MB.");
            }

            // Tìm hồ sơ nháp hoặc hồ sơ đang cần bổ sung
            var apps = await _unitOfWork.Repository<ExpertApplication>().FindAsync(
                a => a.UserId == userId && (a.Status == ExpertApplicationStatus.Draft || a.Status == ExpertApplicationStatus.NeedSupplement)
            );
            var app = apps.OrderByDescending(a => a.UpdatedAt).FirstOrDefault();

            var folderName = $"expert-avatars/{userId}";
            var fileUrl = await _fileStorageService.SaveFileAsync(request.File, folderName);

            if (app == null)
            {
                app = new ExpertApplication
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    ApplicationNumber = GenerateApplicationNumber(),
                    FullName = user.FullName,
                    AvatarUrl = fileUrl,
                    Status = ExpertApplicationStatus.Draft,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                await _unitOfWork.Repository<ExpertApplication>().AddAsync(app);
            }
            else
            {
                app.AvatarUrl = fileUrl;
                app.UpdatedAt = DateTimeOffset.UtcNow;
                _unitOfWork.Repository<ExpertApplication>().Update(app);
            }

            await _unitOfWork.SaveChangesAsync();

            return new UploadAvatarResponseDto
            {
                AvatarUrl = fileUrl
            };
        }

        // ── 2. TẢI LÊN CHỨNG CHỈ (Upload Supabase) ───────────────────────────
        public async Task<CertificateResponseDto> UploadCertificateAsync(Guid userId, UploadCertificateRequest request)
        {
            var user = await GetActiveUserOrThrowAsync(userId);

            // Tìm hồ sơ nháp hoặc hồ sơ đang cần bổ sung
            var apps = await _unitOfWork.Repository<ExpertApplication>().FindAsync(
                a => a.UserId == userId && (a.Status == ExpertApplicationStatus.Draft || a.Status == ExpertApplicationStatus.NeedSupplement)
            );
            var app = apps.FirstOrDefault();

            if (app == null)
            {
                // Nếu chưa có hồ sơ nháp, tự động khởi tạo hồ sơ nháp để gắn chứng chỉ
                app = new ExpertApplication
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    ApplicationNumber = GenerateApplicationNumber(),
                    FullName = user.FullName,
                    Status = ExpertApplicationStatus.Draft,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };

                await _unitOfWork.Repository<ExpertApplication>().AddAsync(app);
                await _unitOfWork.SaveChangesAsync();
            }

            // Kiểm tra quy tắc ngày cấp chứng chỉ (BR-07)
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (request.IssueDate > today)
            {
                throw new BadRequestException("INVALID_ISSUE_DATE", "Ngày cấp chứng chỉ không được lớn hơn ngày hiện tại.");
            }

            // Kiểm tra quy tắc thời hạn chứng chỉ (BR-06)
            if (request.HasExpiry)
            {
                if (!request.ExpiryDate.HasValue)
                {
                    throw new BadRequestException("EXPIRY_DATE_REQUIRED", "Chứng chỉ có thời hạn bắt buộc phải nhập ngày hết hạn.");
                }

                if (request.ExpiryDate.Value <= today)
                {
                    throw new BadRequestException("CERTIFICATE_EXPIRED", "Chứng chỉ đã hết thời hạn hiệu lực.");
                }

                if (request.ExpiryDate.Value <= request.IssueDate)
                {
                    throw new BadRequestException("INVALID_EXPIRY_DATE", "Ngày hết hạn phải sau ngày cấp chứng chỉ.");
                }
            }
            else
            {
                request.ExpiryDate = null;
            }

            // Kiểm tra file đính kèm
            if (request.File == null || request.File.Length == 0)
            {
                throw new BadRequestException("EMPTY_FILE", "File chứng chỉ không được để trống.");
            }

            var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
            var extension = Path.GetExtension(request.File.FileName).ToLower();
            if (!allowedExtensions.Contains(extension))
            {
                throw new BadRequestException("INVALID_FILE_TYPE", "Chỉ hỗ trợ file đính kèm định dạng PDF, JPG, JPEG hoặc PNG.");
            }

            if (request.File.Length > 10 * 1024 * 1024)
            {
                throw new BadRequestException("FILE_TOO_LARGE", "Dung lượng file chứng chỉ không được vượt quá 10MB.");
            }

            // Upload file lên Supabase Storage
            var folderName = $"expert-certificates/{app.Id}";
            var fileUrl = await _fileStorageService.SaveFileAsync(request.File, folderName);

            var certificate = new ExpertApplicationCertificate
            {
                Id = Guid.NewGuid(),
                ApplicationId = app.Id,
                CertificateType = request.CertificateType,
                CertificateName = request.CertificateName,
                CertificateNumber = request.CertificateNumber,
                IssuingAuthority = request.IssuingAuthority,
                IssueDate = request.IssueDate,
                HasExpiry = request.HasExpiry,
                ExpiryDate = request.ExpiryDate,
                FileUrl = fileUrl,
                FileName = request.File.FileName,
                FileMimeType = request.File.ContentType,
                VerificationStatus = CertificateVerificationStatus.PendingVerification,
                CreatedAt = DateTimeOffset.UtcNow
            };

            await _unitOfWork.Repository<ExpertApplicationCertificate>().AddAsync(certificate);
            await _unitOfWork.SaveChangesAsync();

            return new CertificateResponseDto
            {
                Id = certificate.Id,
                CertificateType = certificate.CertificateType,
                CertificateName = certificate.CertificateName,
                CertificateNumber = certificate.CertificateNumber,
                IssuingAuthority = certificate.IssuingAuthority,
                IssueDate = certificate.IssueDate,
                HasExpiry = certificate.HasExpiry,
                ExpiryDate = certificate.ExpiryDate,
                FileUrl = certificate.FileUrl,
                FileName = certificate.FileName,
                VerificationStatus = certificate.VerificationStatus,
                VerificationNote = certificate.VerificationNote
            };
        }

        // ── 3. XÓA CHỨNG CHỈ KHỎI BẢN NHÁP ──────────────────────────────────
        public async Task DeleteCertificateAsync(Guid userId, Guid certificateId)
        {
            var certs = await _unitOfWork.Repository<ExpertApplicationCertificate>().FindAsync(c => c.Id == certificateId);
            var cert = certs.FirstOrDefault();
            if (cert == null)
            {
                throw new NotFoundException("Không tìm thấy thông tin chứng chỉ.");
            }

            var app = await _unitOfWork.Repository<ExpertApplication>().GetByIdAsync(cert.ApplicationId);
            if (app == null || app.UserId != userId)
            {
                throw new UnauthorizedException("FORBIDDEN", "Bạn không có quyền thao tác trên chứng chỉ này.");
            }

            if (app.Status != ExpertApplicationStatus.Draft && app.Status != ExpertApplicationStatus.NeedSupplement)
            {
                throw new BadRequestException("CANNOT_MODIFY", "Hồ sơ đang trong quá trình xét duyệt hoặc đã hoàn tất, không thể xóa chứng chỉ.");
            }

            // Xóa file trên Storage
            try
            {
                await _fileStorageService.DeleteFileAsync(cert.FileUrl);
            }
            catch
            {
                // Bỏ qua lỗi xóa file storage để không chặn quá trình xóa dữ liệu DB
            }

            _unitOfWork.Repository<ExpertApplicationCertificate>().Remove(cert);
            await _unitOfWork.SaveChangesAsync();
        }

        // ── 4. NỘP HỒ SƠ CHÍNH THỨC (Draft -> PendingReview) ─────────────────
        public async Task<ExpertApplicationDetailResponse> SubmitApplicationAsync(Guid userId)
        {
            var user = await GetActiveUserOrThrowAsync(userId);

            // Tìm hồ sơ nháp hoặc hồ sơ cần bổ sung
            var apps = await _unitOfWork.Repository<ExpertApplication>().FindAsync(
                a => a.UserId == userId && (a.Status == ExpertApplicationStatus.Draft || a.Status == ExpertApplicationStatus.NeedSupplement)
            );
            var app = apps.FirstOrDefault();

            if (app == null)
            {
                throw new NotFoundException("Bạn không có hồ sơ bản nháp nào đang chờ nộp.");
            }

            // Kiểm tra không có hồ sơ nào khác đang ở trạng thái PendingReview (BR-02)
            var pendingApps = await _unitOfWork.Repository<ExpertApplication>().FindAsync(
                a => a.UserId == userId && a.Id != app.Id && a.Status == ExpertApplicationStatus.PendingReview
            );
            if (pendingApps.Any())
            {
                throw new BadRequestException("ACTIVE_APPLICATION_EXISTS", "Bạn đã có hồ sơ đang chờ xét duyệt.");
            }

            // Kiểm tra thông tin cơ bản bắt buộc
            if (string.IsNullOrWhiteSpace(app.FullName) ||
                string.IsNullOrWhiteSpace(app.JobTitle) ||
                string.IsNullOrWhiteSpace(app.ExperienceDescription))
            {
                throw new BadRequestException("MISSING_REQUIRED_FIELDS", "Vui lòng điền đầy đủ Họ tên, Chức danh và Mô tả kinh nghiệm trước khi nộp hồ sơ.");
            }

            // Đối soát họ tên trong hồ sơ với tài khoản người dùng
            if (!string.IsNullOrWhiteSpace(user.FullName) && !StringComparisonHelper.IsNameMatching(user.FullName, app.FullName))
            {
                throw new BadRequestException(
                    "FULLNAME_MISMATCH",
                    $"Họ và tên trong hồ sơ ('{app.FullName}') không trùng khớp với họ và tên trên tài khoản hệ thống ('{user.FullName}'). Vui lòng cập nhật lại trước khi nộp."
                );
            }

            // Kiểm tra lĩnh vực chuyên môn (Tối thiểu 1)
            var appSpecs = await _unitOfWork.Repository<ExpertApplicationSpecialization>().FindAsync(s => s.ApplicationId == app.Id);
            if (!appSpecs.Any())
            {
                throw new BadRequestException("SPECIALIZATION_REQUIRED", "Vui lòng chọn ít nhất 1 lĩnh vực chuyên môn tư vấn.");
            }

            // Kiểm tra chứng chỉ (BR-05: Ít nhất 1 chứng chỉ hợp lệ)
            var certs = (await _unitOfWork.Repository<ExpertApplicationCertificate>().FindAsync(c => c.ApplicationId == app.Id)).ToList();
            if (!certs.Any())
            {
                throw new BadRequestException("CERTIFICATE_REQUIRED", "Hồ sơ bắt buộc phải có ít nhất 1 chứng chỉ/bằng cấp đính kèm.");
            }

            // Kiểm tra thời hạn các chứng chỉ (BR-06)
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var expiredCerts = certs.Where(c => c.HasExpiry && c.ExpiryDate.HasValue && c.ExpiryDate.Value <= today).ToList();
            if (expiredCerts.Any())
            {
                throw new BadRequestException("CERTIFICATE_EXPIRED", $"Có {expiredCerts.Count} chứng chỉ đã hết hạn sử dụng. Vui lòng cập nhật trước khi nộp hồ sơ.");
            }

            // Kiểm tra mức phí đề xuất (BR-08: Nằm trong khung quy định)
            var proposals = (await _unitOfWork.Repository<ExpertApplicationFeeProposal>().FindAsync(p => p.ApplicationId == app.Id)).ToList();
            if (!proposals.Any())
            {
                throw new BadRequestException("FEE_PROPOSAL_REQUIRED", "Vui lòng đề xuất mức phí tư vấn trước khi nộp hồ sơ.");
            }

            var activeConfigs = (await _unitOfWork.Repository<ConsultationFeeConfiguration>().FindAsync(c => c.IsActive)).ToList();
            foreach (var prop in proposals)
            {
                var matchedConfig = activeConfigs.FirstOrDefault(
                    c => c.SessionType == prop.SessionType && c.DurationMinutes == prop.DurationMinutes
                );

                if (matchedConfig != null)
                {
                    if (prop.ProposedFee < matchedConfig.MinFee || prop.ProposedFee > matchedConfig.MaxFee)
                    {
                        throw new BadRequestException(
                            "FEE_OUT_OF_RANGE",
                            $"Mức phí đề xuất cho hình thức {prop.SessionType} ({prop.DurationMinutes} phút) phải nằm trong khoảng {matchedConfig.MinFee:N0} VNĐ - {matchedConfig.MaxFee:N0} VNĐ."
                        );
                    }
                }
            }

            // Cập nhật trạng thái hồ sơ
            var fromStatus = app.Status;
            app.Status = ExpertApplicationStatus.PendingReview;
            app.SubmittedAt = DateTimeOffset.UtcNow;
            app.UpdatedAt = DateTimeOffset.UtcNow;

            _unitOfWork.Repository<ExpertApplication>().Update(app);

            // Ghi nhật ký xử lý Audit Trail (BR-11)
            var auditAction = fromStatus == ExpertApplicationStatus.NeedSupplement
                ? ApplicationAuditAction.Resubmitted
                : ApplicationAuditAction.Submitted;

            var auditNote = fromStatus == ExpertApplicationStatus.NeedSupplement
                ? "Ứng viên đã nộp lại hồ sơ sau khi bổ sung theo yêu cầu."
                : "Ứng viên nộp hồ sơ đăng ký chuyên gia mới.";

            var audit = new ExpertApplicationAudit
            {
                Id = Guid.NewGuid(),
                ApplicationId = app.Id,
                ActorId = userId,
                Action = auditAction,
                FromStatus = fromStatus,
                ToStatus = ExpertApplicationStatus.PendingReview,
                Notes = auditNote,
                CreatedAt = DateTimeOffset.UtcNow
            };

            await _unitOfWork.Repository<ExpertApplicationAudit>().AddAsync(audit);
            await _unitOfWork.SaveChangesAsync();

            return await BuildApplicationDetailResponseAsync(app);
        }

        // ── 5. XEM HỒ SƠ CỦA TÔI ─────────────────────────────────────────────
        public async Task<ExpertApplicationDetailResponse?> GetMyApplicationAsync(Guid userId)
        {
            var apps = await _unitOfWork.Repository<ExpertApplication>().FindAsync(a => a.UserId == userId);
            var latestApp = apps.OrderByDescending(a => a.CreatedAt).FirstOrDefault();

            if (latestApp == null)
            {
                return null;
            }

            return await BuildApplicationDetailResponseAsync(latestApp);
        }

        // ── SHARED HELPER FUNCTIONS ─────────────────────────────────────────
        private async Task<User> GetActiveUserOrThrowAsync(Guid userId)
        {
            var users = await _unitOfWork.Repository<User>().FindAsync(u => u.UserId == userId);
            var user = users.FirstOrDefault();
            if (user == null)
            {
                throw new NotFoundException("Không tìm thấy tài khoản người dùng.");
            }

            if (!string.Equals(user.Status, "active", StringComparison.OrdinalIgnoreCase))
            {
                throw new BadRequestException("USER_NOT_ACTIVE", "Tài khoản của bạn không ở trạng thái hoạt động.");
            }

            return user;
        }

        private static string GenerateApplicationNumber()
        {
            // Định dạng: EXP-YYYYMM-XXXX
            var datePart = DateTime.UtcNow.ToString("yyyyMM");
            var randomPart = Random.Shared.Next(1000, 9999);
            return $"EXP-{datePart}-{randomPart}";
        }

        private async Task<ExpertApplicationDetailResponse> BuildApplicationDetailResponseAsync(ExpertApplication app)
        {
            // Lấy danh sách lĩnh vực chuyên môn
            var appSpecs = await _unitOfWork.Repository<ExpertApplicationSpecialization>().FindAsync(s => s.ApplicationId == app.Id);
            var specIds = appSpecs.Select(s => s.SpecializationId).ToList();
            var allSpecs = await _unitOfWork.Repository<Specialization>().FindAsync(s => specIds.Contains(s.Id));

            // Lấy danh sách chứng chỉ
            var certs = await _unitOfWork.Repository<ExpertApplicationCertificate>().FindAsync(c => c.ApplicationId == app.Id);

            // Lấy danh sách đề xuất mức phí
            var proposals = await _unitOfWork.Repository<ExpertApplicationFeeProposal>().FindAsync(p => p.ApplicationId == app.Id);

            // Lấy lịch sử audit
            var audits = (await _unitOfWork.Repository<ExpertApplicationAudit>().FindAsync(a => a.ApplicationId == app.Id)).ToList();
            var actorIds = audits.Select(a => a.ActorId).Distinct().ToList();
            var actors = (await _unitOfWork.Repository<User>().FindAsync(u => actorIds.Contains(u.UserId))).ToList();
            var actorDict = actors.ToDictionary(u => u.UserId, u => u.FullName);

            return new ExpertApplicationDetailResponse
            {
                Id = app.Id,
                UserId = app.UserId,
                ApplicationNumber = app.ApplicationNumber,
                FullName = app.FullName,
                AvatarUrl = app.AvatarUrl,
                JobTitle = app.JobTitle,
                CompanyName = app.CompanyName,
                Bio = app.Bio,
                YearsOfExperience = app.YearsOfExperience,
                CurrentPosition = app.CurrentPosition,
                ExperienceDescription = app.ExperienceDescription,
                Status = app.Status,
                SubmittedAt = app.SubmittedAt,
                ReviewedBy = app.ReviewedBy,
                ReviewedAt = app.ReviewedAt,
                RejectionReason = app.RejectionReason,
                SupplementRequestReason = app.SupplementRequestReason,
                CreatedAt = app.CreatedAt,
                UpdatedAt = app.UpdatedAt,

                Specializations = allSpecs.Select(s => new SpecializationDtoItem
                {
                    Id = s.Id,
                    Code = s.Code,
                    Name = s.Name
                }).ToList(),

                Certificates = certs.Select(c => new CertificateResponseDto
                {
                    Id = c.Id,
                    CertificateType = c.CertificateType,
                    CertificateName = c.CertificateName,
                    CertificateNumber = c.CertificateNumber,
                    IssuingAuthority = c.IssuingAuthority,
                    IssueDate = c.IssueDate,
                    HasExpiry = c.HasExpiry,
                    ExpiryDate = c.ExpiryDate,
                    FileUrl = c.FileUrl,
                    FileName = c.FileName,
                    VerificationStatus = c.VerificationStatus,
                    VerificationSource = c.VerificationSource,
                    VerificationNote = c.VerificationNote,
                    VerifiedBy = c.VerifiedBy,
                    VerifiedAt = c.VerifiedAt
                }).ToList(),

                FeeProposals = proposals.Select(p => new FeeProposalResponseDto
                {
                    Id = p.Id,
                    SessionType = p.SessionType,
                    DurationMinutes = p.DurationMinutes,
                    ProposedFee = p.ProposedFee
                }).ToList(),

                Audits = audits.OrderByDescending(a => a.CreatedAt).Select(a => new ExpertApplicationAuditResponseDto
                {
                    Id = a.Id,
                    ActorId = a.ActorId,
                    ActorName = actorDict.TryGetValue(a.ActorId, out var name) ? name : null,
                    Action = a.Action,
                    FromStatus = a.FromStatus,
                    ToStatus = a.ToStatus,
                    Notes = a.Notes,
                    CreatedAt = a.CreatedAt
                }).ToList()
            };
        }
    }
}
