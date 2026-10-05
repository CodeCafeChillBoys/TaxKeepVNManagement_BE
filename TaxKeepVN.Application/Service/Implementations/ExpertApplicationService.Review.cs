using Microsoft.Extensions.Logging;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.ExpertApplications;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;

namespace TaxKeepVN.Application.Service.Implementations
{
    /// <summary>
    /// Partial class xử lý luồng Thẩm định & Phê duyệt hồ sơ của Admin / Reviewer (Bước 4)
    /// </summary>
    public partial class ExpertApplicationService : IExpertApplicationService
    {
        // ── 1. LẤY DANH SÁCH HỒ SƠ CHUYÊN GIA (Phân trang, lọc status, search) ─────
        public async Task<PagedResult<ExpertApplicationListItemResponse>> GetApplicationsAsync(AdminExpertApplicationQueryParameters query)
        {
            var appRepo = _unitOfWork.Repository<ExpertApplication>();
            var allApps = await appRepo.GetAllAsync();
            var queryable = allApps.AsQueryable();

            if (query.Status.HasValue)
            {
                queryable = queryable.Where(a => a.Status == query.Status.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var keyword = query.Search.Trim().ToLower();
                queryable = queryable.Where(a =>
                    a.FullName.ToLower().Contains(keyword) ||
                    a.ApplicationNumber.ToLower().Contains(keyword) ||
                    a.JobTitle.ToLower().Contains(keyword) ||
                    (a.CompanyName != null && a.CompanyName.ToLower().Contains(keyword)));
            }

            // Sắp xếp: Hồ sơ PendingReview ưu tiên lên đầu, rồi theo SubmittedAt / CreatedAt giảm dần
            queryable = queryable
                .OrderByDescending(a => a.Status == ExpertApplicationStatus.PendingReview)
                .ThenByDescending(a => a.SubmittedAt ?? a.CreatedAt);

            var totalItems = queryable.Count();
            // phân trang skip và lây thêm 10
            var pagedApps = queryable
                .Skip((query.Page - 1) * query.Size)
                .Take(query.Size)
                .ToList();
            // lấy tất cả hồ sơ ID lên
            var appIds = pagedApps.Select(a => a.Id).ToList();
            // kiểm tra xem hồ soe có trong ExpertApplicationSpecialization
            var appSpecs = (await _unitOfWork.Repository<ExpertApplicationSpecialization>().FindAsync(s => appIds.Contains(s.ApplicationId))).ToList();
            // lấy lên tất cả SpecializationId lên
            var specIds = appSpecs.Select(s => s.SpecializationId).Distinct().ToList();
            // kiểm tra list vừa lấy lên có trong Specialization nếu có lấy lên hết
            var specs = (await _unitOfWork.Repository<Specialization>().FindAsync(s => specIds.Contains(s.Id))).ToList();
            // trả về dạng dic
            var specDict = specs.ToDictionary(s => s.Id, s => s.Name);

            // check bằng có trong hồ sơ ko 1 hồ sơ sẽ có nhiều bằng
            var allCerts = (await _unitOfWork.Repository<ExpertApplicationCertificate>().FindAsync(c => appIds.Contains(c.ApplicationId))).ToList();
            
            // 
            var items = pagedApps.Select(a =>
            {
                var aSpecs = appSpecs
                    .Where(s => s.ApplicationId == a.Id)
                    .Select(s => specDict.TryGetValue(s.SpecializationId, out var name) ? name : "")
                    .Where(n => !string.IsNullOrEmpty(n))
                    .ToList();
                var aCerts = allCerts.Where(c => c.ApplicationId == a.Id).ToList();

                return new ExpertApplicationListItemResponse
                {
                    Id = a.Id,
                    UserId = a.UserId,
                    ApplicationNumber = a.ApplicationNumber,
                    FullName = a.FullName,
                    AvatarUrl = a.AvatarUrl,
                    JobTitle = a.JobTitle,
                    CompanyName = a.CompanyName,
                    YearsOfExperience = a.YearsOfExperience,
                    Status = a.Status,
                    SubmittedAt = a.SubmittedAt,
                    ReviewedAt = a.ReviewedAt,
                    CreatedAt = a.CreatedAt,
                    SpecializationNames = aSpecs,
                    CertificateCount = aCerts.Count,
                    VerifiedCertificateCount = aCerts.Count(c => c.VerificationStatus == CertificateVerificationStatus.Verified)
                };
            }).ToList();

            var totalPages = (int)Math.Ceiling(totalItems / (double)query.Size);

            return new PagedResult<ExpertApplicationListItemResponse>
            {
                Items = items,
                Pagination = new PaginationMeta
                {
                    Page = query.Page,
                    PageSize = query.Size,
                    TotalItems = totalItems,
                    TotalPages = totalPages
                }
            };
        }

        // ── 2. XEM CHI TIẾT HỒ SƠ ──────────────────────────────────────────────
        public async Task<ExpertApplicationDetailResponse> GetApplicationByIdAsync(Guid applicationId)
        {
            var app = await _unitOfWork.Repository<ExpertApplication>().GetByIdAsync(applicationId);
            if (app == null)
            {
                throw new NotFoundException($"Không tìm thấy hồ sơ đăng ký chuyên gia với Id = {applicationId}.");
            }

            return await BuildApplicationDetailResponseAsync(app);
        }

        // ── 3. THẨM ĐỊNH CHỨNG CHỈ ĐƠN LẺ ─────────────────────────────────────
        public async Task<CertificateResponseDto> VerifyCertificateAsync(Guid adminId, Guid applicationId, Guid certificateId, VerifyCertificateRequest request)
        {
            var app = await _unitOfWork.Repository<ExpertApplication>().GetByIdAsync(applicationId);
            if (app == null)
            {
                throw new NotFoundException($"Không tìm thấy hồ sơ đăng ký chuyên gia với Id = {applicationId}.");
            }

            var certRepo = _unitOfWork.Repository<ExpertApplicationCertificate>();
            var certs = await certRepo.FindAsync(c => c.Id == certificateId && c.ApplicationId == applicationId);
            var cert = certs.FirstOrDefault();
            if (cert == null)
            {
                throw new NotFoundException($"Không tìm thấy chứng chỉ với Id = {certificateId} trong hồ sơ.");
            }

            cert.VerificationStatus = request.VerificationStatus;
            cert.VerificationSource = request.VerificationSource?.Trim();
            cert.VerificationNote = request.VerificationNote?.Trim();
            cert.VerifiedBy = adminId;
            cert.VerifiedAt = DateTimeOffset.UtcNow;

            certRepo.Update(cert);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Admin {AdminId} đã thẩm định chứng chỉ {CertId} của hồ sơ {AppId}: Status = {Status}",
                adminId, certificateId, applicationId, request.VerificationStatus);

            return new CertificateResponseDto
            {
                Id = cert.Id,
                CertificateType = cert.CertificateType,
                CertificateName = cert.CertificateName,
                CertificateNumber = cert.CertificateNumber,
                IssuingAuthority = cert.IssuingAuthority,
                IssueDate = cert.IssueDate,
                HasExpiry = cert.HasExpiry,
                ExpiryDate = cert.ExpiryDate,
                FileUrl = cert.FileUrl,
                FileName = cert.FileName,
                VerificationStatus = cert.VerificationStatus,
                VerificationSource = cert.VerificationSource,
                VerificationNote = cert.VerificationNote,
                VerifiedBy = cert.VerifiedBy,
                VerifiedAt = cert.VerifiedAt
            };
        }

        // ── 4. PHÊ DUYỆT HỒ SƠ (Approved -> Role expert -> ExpertProfile) ────────
        public async Task<ExpertApplicationDetailResponse> ApproveApplicationAsync(Guid adminId, Guid applicationId, ApproveExpertApplicationRequest? request)
        {
            var appRepo = _unitOfWork.Repository<ExpertApplication>();
            // kiểm tra xem bồ hồ sơ Id có tồn tại ko
            var app = await appRepo.GetByIdAsync(applicationId);
            if (app == null)
            {
                throw new NotFoundException($"Không tìm thấy hồ sơ đăng ký chuyên gia với Id = {applicationId}.");
            }

            // Kiểm tra xem hồ sơ vừa đc tìm kiếm đang trạng thái nào nếu trạng thái "PendingReview" => ok
            if (app.Status != ExpertApplicationStatus.PendingReview)
            {
                throw new BadRequestException("INVALID_STATUS",
                    $"Chỉ có thể phê duyệt hồ sơ ở trạng thái Đang chờ duyệt (PendingReview). Trạng thái hiện tại: {app.Status}");
            }
            // kiểm tra trong chứng chỉ đó có hồ sơ ko => lấy lên tất cả chứng chỉ có trong hồ sơ
            var certs = (await _unitOfWork.Repository<ExpertApplicationCertificate>().FindAsync(c => c.ApplicationId == app.Id)).ToList();
            // BR-07: Chứng chỉ bắt buộc phải còn hiệu lực tại thời điểm hồ sơ được phê duyệt
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var expiredCert = certs.FirstOrDefault(c => c.HasExpiry && c.ExpiryDate.HasValue && c.ExpiryDate.Value < today);
            if (expiredCert != null)
            {
                throw new BadRequestException("CERTIFICATE_EXPIRED",
                    $"Chứng chỉ '{expiredCert.CertificateName}' (Số hiệu: {expiredCert.CertificateNumber}) đã hết hạn vào ngày {expiredCert.ExpiryDate:dd/MM/yyyy}. Không thể phê duyệt hồ sơ (BR-07).");
            }

            // Tự động chuyển các chứng chỉ PendingVerification sang Verified
            var certRepo = _unitOfWork.Repository<ExpertApplicationCertificate>();
            foreach (var cert in certs)
            {
                if (cert.VerificationStatus == CertificateVerificationStatus.PendingVerification)
                {
                    cert.VerificationStatus = CertificateVerificationStatus.Verified;
                    cert.VerifiedBy = adminId;
                    cert.VerifiedAt = DateTimeOffset.UtcNow;
                    cert.VerificationNote ??= "Tự động xác nhận khi phê duyệt hồ sơ.";
                    certRepo.Update(cert);
                }
            }

            var prevStatus = app.Status;
            app.Status = ExpertApplicationStatus.Approved;
            app.ReviewedBy = adminId;
            app.ReviewedAt = DateTimeOffset.UtcNow;
            app.UpdatedAt = DateTimeOffset.UtcNow;
            appRepo.Update(app);

            // Cập nhật Role người dùng lên Chuyên gia (Role 3: expert)
            var userRepo = _unitOfWork.Repository<User>();
            var user = await userRepo.GetByIdAsync(app.UserId);
            if (user != null)
            {
                user.RoleId = 3;
                user.UserRole = "expert";
                user.UpdatedAt = DateTimeOffset.UtcNow;
                userRepo.Update(user);
            }

            // Tạo mới hoặc kích hoạt ExpertProfile
            var profileRepo = _unitOfWork.Repository<ExpertProfile>();
            var profiles = await profileRepo.FindAsync(p => p.UserId == app.UserId);
            var existingProfile = profiles.FirstOrDefault();
            if (existingProfile == null)
            {
                var newProfile = new ExpertProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = app.UserId,
                    LatestApplicationId = app.Id,
                    JobTitle = app.JobTitle,
                    CompanyName = app.CompanyName,
                    Bio = app.Bio,
                    YearsOfExperience = app.YearsOfExperience,
                    Rating = 0.00m,
                    TotalReviews = 0,
                    IsActive = true,
                    ApprovedAt = DateTimeOffset.UtcNow,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                await profileRepo.AddAsync(newProfile);
            }
            else
            {
                existingProfile.LatestApplicationId = app.Id;
                existingProfile.JobTitle = app.JobTitle;
                existingProfile.CompanyName = app.CompanyName;
                existingProfile.Bio = app.Bio;
                existingProfile.YearsOfExperience = app.YearsOfExperience;
                existingProfile.IsActive = true;
                existingProfile.ApprovedAt = DateTimeOffset.UtcNow;
                existingProfile.UpdatedAt = DateTimeOffset.UtcNow;
                profileRepo.Update(existingProfile);
            }

            // BR-11: Ghi log Audit
            var audit = new ExpertApplicationAudit
            {
                Id = Guid.NewGuid(),
                ApplicationId = app.Id,
                ActorId = adminId,
                Action = ApplicationAuditAction.Approved,
                FromStatus = prevStatus,
                ToStatus = ExpertApplicationStatus.Approved,
                Notes = request?.Note ?? "Hồ sơ đã được phê duyệt thành công.",
                CreatedAt = DateTimeOffset.UtcNow
            };
            await _unitOfWork.Repository<ExpertApplicationAudit>().AddAsync(audit);

            // Gửi thông báo đến ứng viên
            var notif = new SystemNotification
            {
                NotificationId = Guid.NewGuid(),
                UserId = app.UserId,
                Title = "Chúc mừng! Hồ sơ đăng ký chuyên gia đã được phê duyệt",
                Message = $"Hồ sơ #{app.ApplicationNumber} của bạn đã được phê duyệt. Bạn hiện là Chuyên gia tư vấn chính thức trên hệ thống.",
                NotificationType = "EXPERT_APPLICATION_APPROVED",
                IsRead = false,
                TargetActionUrl = "/expert/profile",
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.Repository<SystemNotification>().AddAsync(notif);

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Admin {AdminId} đã phê duyệt hồ sơ chuyên gia {AppId} (#{AppNumber}) cho User {UserId}",
                adminId, app.Id, app.ApplicationNumber, app.UserId);

            return await BuildApplicationDetailResponseAsync(app);
        }

        // ── 5. TỪ CHỐI HỒ SƠ (Rejected - BR-09) ──────────────────────────────────
        public async Task<ExpertApplicationDetailResponse> RejectApplicationAsync(Guid adminId, Guid applicationId, RejectExpertApplicationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                throw new BadRequestException("REASON_REQUIRED", "Lý do từ chối hồ sơ không được để trống (BR-09).");
            }

            var appRepo = _unitOfWork.Repository<ExpertApplication>();
            var app = await appRepo.GetByIdAsync(applicationId);
            if (app == null)
            {
                throw new NotFoundException($"Không tìm thấy hồ sơ đăng ký chuyên gia với Id = {applicationId}.");
            }

            if (app.Status != ExpertApplicationStatus.PendingReview && app.Status != ExpertApplicationStatus.NeedSupplement)
            {
                throw new BadRequestException("INVALID_STATUS",
                    $"Chỉ có thể từ chối hồ sơ đang chờ xét duyệt hoặc đang chờ bổ sung. Trạng thái hiện tại: {app.Status}");
            }

            var prevStatus = app.Status;
            app.Status = ExpertApplicationStatus.Rejected;
            app.ReviewedBy = adminId;
            app.ReviewedAt = DateTimeOffset.UtcNow;
            app.RejectionReason = request.Reason.Trim();
            app.UpdatedAt = DateTimeOffset.UtcNow;
            appRepo.Update(app);

            // BR-11: Ghi log Audit
            var audit = new ExpertApplicationAudit
            {
                Id = Guid.NewGuid(),
                ApplicationId = app.Id,
                ActorId = adminId,
                Action = ApplicationAuditAction.Rejected,
                FromStatus = prevStatus,
                ToStatus = ExpertApplicationStatus.Rejected,
                Notes = request.Reason.Trim(),
                CreatedAt = DateTimeOffset.UtcNow
            };
            await _unitOfWork.Repository<ExpertApplicationAudit>().AddAsync(audit);

            // Gửi thông báo đến ứng viên
            var notif = new SystemNotification
            {
                NotificationId = Guid.NewGuid(),
                UserId = app.UserId,
                Title = "Hồ sơ đăng ký chuyên gia không được phê duyệt",
                Message = $"Hồ sơ #{app.ApplicationNumber} của bạn đã bị từ chối. Lý do: {request.Reason.Trim()}",
                NotificationType = "EXPERT_APPLICATION_REJECTED",
                IsRead = false,
                TargetActionUrl = "/expert/registration",
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.Repository<SystemNotification>().AddAsync(notif);

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Admin {AdminId} đã từ chối hồ sơ {AppId} (#{AppNumber}). Lý do: {Reason}",
                adminId, app.Id, app.ApplicationNumber, request.Reason);

            return await BuildApplicationDetailResponseAsync(app);
        }

        // ── 6. YÊU CẦU BỔ SUNG HỒ SƠ (NeedSupplement - BR-10) ───────────────────
        public async Task<ExpertApplicationDetailResponse> RequestSupplementAsync(Guid adminId, Guid applicationId, RequestSupplementRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                throw new BadRequestException("REASON_REQUIRED", "Nội dung yêu cầu bổ sung không được để trống (BR-10).");
            }

            var appRepo = _unitOfWork.Repository<ExpertApplication>();
            var app = await appRepo.GetByIdAsync(applicationId);
            if (app == null)
            {
                throw new NotFoundException($"Không tìm thấy hồ sơ đăng ký chuyên gia với Id = {applicationId}.");
            }

            if (app.Status != ExpertApplicationStatus.PendingReview)
            {
                throw new BadRequestException("INVALID_STATUS",
                    $"Chỉ có thể yêu cầu bổ sung đối với hồ sơ đang chờ xét duyệt (PendingReview). Trạng thái hiện tại: {app.Status}");
            }

            var prevStatus = app.Status;
            app.Status = ExpertApplicationStatus.NeedSupplement;
            app.ReviewedBy = adminId;
            app.ReviewedAt = DateTimeOffset.UtcNow;
            app.SupplementRequestReason = request.Reason.Trim();
            app.UpdatedAt = DateTimeOffset.UtcNow;
            appRepo.Update(app);

            // BR-11: Ghi log Audit
            var audit = new ExpertApplicationAudit
            {
                Id = Guid.NewGuid(),
                ApplicationId = app.Id,
                ActorId = adminId,
                Action = ApplicationAuditAction.SupplementRequested,
                FromStatus = prevStatus,
                ToStatus = ExpertApplicationStatus.NeedSupplement,
                Notes = request.Reason.Trim(),
                CreatedAt = DateTimeOffset.UtcNow
            };
            await _unitOfWork.Repository<ExpertApplicationAudit>().AddAsync(audit);

            // Gửi thông báo đến ứng viên
            var notif = new SystemNotification
            {
                NotificationId = Guid.NewGuid(),
                UserId = app.UserId,
                Title = "Yêu cầu bổ sung hồ sơ đăng ký chuyên gia",
                Message = $"Hồ sơ #{app.ApplicationNumber} của bạn cần bổ sung thêm thông tin: {request.Reason.Trim()}",
                NotificationType = "EXPERT_APPLICATION_SUPPLEMENT_REQUESTED",
                IsRead = false,
                TargetActionUrl = "/expert/registration",
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.Repository<SystemNotification>().AddAsync(notif);

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Admin {AdminId} yêu cầu bổ sung hồ sơ {AppId} (#{AppNumber}). Nội dung: {Reason}",
                adminId, app.Id, app.ApplicationNumber, request.Reason);

            return await BuildApplicationDetailResponseAsync(app);
        }
    }
}
