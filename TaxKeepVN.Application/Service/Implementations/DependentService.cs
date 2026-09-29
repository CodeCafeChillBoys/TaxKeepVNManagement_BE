using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Requests.Dependent;
using TaxKeepVN.Application.DTOs.Responses.Dependent;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    public class DependentService : IDependentService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _configuration;

        public DependentService(
            IUnitOfWork unitOfWork,
            IMemoryCache cache,
            IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _cache = cache;
            _configuration = configuration;
        }

        public async Task<DependentResponse> CreateDependentAsync(Guid taxpayerId, CreateDependentRequest request)
        {
            // ── 1. Validate Relationship enum ───────────────────────────────────
            if (!Enum.TryParse<DependentRelationship>(request.Relationship, true, out var relationship))
            {
                throw new BadRequestException(
                    "INVALID_RELATIONSHIP",
                    $"Nhóm quan hệ '{request.Relationship}' không hợp lệ. " +
                    "Các giá trị hợp lệ: CHILD, SPOUSE, PARENT, OTHER_DEPENDENT."
                );
            }

            // ── 2. Validate và parse CurrentGroup enum ──────────────────────────
            if (!Enum.TryParse<DependentGroup>(request.CurrentGroup, true, out var currentGroup))
            {
                throw new BadRequestException(
                    "INVALID_CURRENT_GROUP",
                    $"Điều kiện đăng ký '{request.CurrentGroup}' không hợp lệ. " +
                    "Vui lòng chọn đúng điều kiện đăng ký người phụ thuộc."
                );
            }

            // ── 3. Validate CurrentGroup phải khớp với Relationship ─────────────
            ValidateGroupMatchesRelationship(relationship, currentGroup);

            // ── 4. Validate định danh: phải có CitizenId HOẶC BirthCertNumber ──
            var hasCitizenId = !string.IsNullOrWhiteSpace(request.CitizenId);
            var hasBirthCert = !string.IsNullOrWhiteSpace(request.BirthCertNumber);

            if (!hasCitizenId && !hasBirthCert)
            {
                throw new BadRequestException(
                    "IDENTIFIER_REQUIRED",
                    "Phải cung cấp ít nhất Số CCCD hoặc Số Giấy khai sinh của người phụ thuộc."
                );
            }

            // ── 5. Validate khoảng thời gian: From <= To ───────────────────────
            if (string.Compare(request.EffectiveFromMonth, request.EffectiveToMonth, StringComparison.Ordinal) > 0)
            {
                throw new BadRequestException(
                    "INVALID_PERIOD",
                    "Tháng bắt đầu tính giảm trừ phải nhỏ hơn hoặc bằng tháng kết thúc."
                );
            }

            // ── 6. Kiểm tra trùng lặp thời gian ────────────────────────────────
            // Hai khoảng [A_from, A_to] và [B_from, B_to] overlap khi:
            //   A_from <= B_to AND A_to >= B_from
            var dependentRepo = _unitOfWork.Repository<Dependent>();

            if (hasCitizenId)
            {
                // Bước 1: Query DB chỉ theo CitizenId (EF Core dịch được)
                var existingByCitizenId = await dependentRepo.FindAsync(d =>
                    d.CitizenId == request.CitizenId);

                // Bước 2: Kiểm tra overlap trong bộ nhớ (in-memory)
                var conflict = existingByCitizenId.FirstOrDefault(d =>
                    string.Compare(d.EffectiveFromMonth, request.EffectiveToMonth, StringComparison.Ordinal) <= 0 &&
                    string.Compare(d.EffectiveToMonth, request.EffectiveFromMonth, StringComparison.Ordinal) >= 0
                );

                if (conflict != null)
                {
                    throw new BadRequestException(
                        "DEPENDENT_PERIOD_OVERLAP",
                        $"Người phụ thuộc có số CCCD '{request.CitizenId}' đã được đăng ký " +
                        $"trong khoảng thời gian từ {conflict.EffectiveFromMonth} đến {conflict.EffectiveToMonth}. " +
                        "Một người phụ thuộc không thể được đăng ký trùng kỳ tính giảm trừ bởi nhiều người nộp thuế."
                    );
                }
            }

            if (hasBirthCert)
            {
                // Bước 1: Query DB chỉ theo BirthCertNumber (EF Core dịch được)
                var existingByBirthCert = await dependentRepo.FindAsync(d =>
                    d.BirthCertNumber == request.BirthCertNumber);

                // Bước 2: Kiểm tra overlap trong bộ nhớ (in-memory)
                var conflict = existingByBirthCert.FirstOrDefault(d =>
                    string.Compare(d.EffectiveFromMonth, request.EffectiveToMonth, StringComparison.Ordinal) <= 0 &&
                    string.Compare(d.EffectiveToMonth, request.EffectiveFromMonth, StringComparison.Ordinal) >= 0
                );

                if (conflict != null)
                {
                    throw new BadRequestException(
                        "DEPENDENT_PERIOD_OVERLAP",
                        $"Người phụ thuộc có số Giấy khai sinh '{request.BirthCertNumber}' đã được đăng ký " +
                        $"trong khoảng thời gian từ {conflict.EffectiveFromMonth} đến {conflict.EffectiveToMonth}. " +
                        "Một người phụ thuộc không thể được đăng ký trùng kỳ tính giảm trừ bởi nhiều người nộp thuế."
                    );
                }
            }

            // ── 7. Tạo mới Dependent ────────────────────────────────────────────
            var newDependent = new Dependent
            {
                Id = Guid.NewGuid(),
                TaxpayerId = taxpayerId,
                FullName = request.FullName,
                Relationship = relationship,
                CurrentGroup = currentGroup,
                BirthDate = request.BirthDate.HasValue
                    ? DateTime.SpecifyKind(request.BirthDate.Value, DateTimeKind.Utc)
                    : DateTime.UtcNow,
                CitizenId = string.IsNullOrWhiteSpace(request.CitizenId) ? null : request.CitizenId.Trim(),
                BirthCertNumber = string.IsNullOrWhiteSpace(request.BirthCertNumber) ? null : request.BirthCertNumber.Trim(),
                TaxIdNumber = string.IsNullOrWhiteSpace(request.TaxIdNumber) ? null : request.TaxIdNumber.Trim(),
                EffectiveFromMonth = request.EffectiveFromMonth,
                EffectiveToMonth = request.EffectiveToMonth,
                Note = request.Note,
                Status = DependentStatus.PENDING_DOCUMENTS,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            await dependentRepo.AddAsync(newDependent);
            await _unitOfWork.SaveChangesAsync();

            // ── 8. Trả về response kèm danh sách giấy tờ cần upload ─────────────
            return new DependentResponse
            {
                DependentId = newDependent.Id,
                TaxpayerId = newDependent.TaxpayerId,
                FullName = newDependent.FullName,
                Relationship = newDependent.Relationship.ToString(),
                CurrentGroup = newDependent.CurrentGroup.ToString(),
                BirthDate = newDependent.BirthDate,
                CitizenId = newDependent.CitizenId,
                BirthCertNumber = newDependent.BirthCertNumber,
                TaxIdNumber = newDependent.TaxIdNumber,
                EffectiveFromMonth = newDependent.EffectiveFromMonth,
                EffectiveToMonth = newDependent.EffectiveToMonth,
                Note = newDependent.Note,
                Status = newDependent.Status.ToString(),
                CreatedAt = newDependent.CreatedAt,
                UpdatedAt = newDependent.UpdatedAt,
                RequiredDocuments = await GetRequiredDocumentsAsync(currentGroup)
            };
        }

        // ── Helper: validate CurrentGroup phải khớp Relationship ─────────────────
        // Đọc từ DependentSettings:GroupOrders trong appsettings.json.
        // Không hardcode cặp Relationship-Group: thêm nhóm mới chỉ cần chỉnh config.
        private void ValidateGroupMatchesRelationship(
            DependentRelationship relationship,
            DependentGroup currentGroup)
        {
            var relKey   = relationship.ToString();   // Ví dụ: "CHILD"
            var groupKey = currentGroup.ToString();   // Ví dụ: "CHILD_UNDER_18"

            // Lấy danh sách các group hợp lệ cho relationship này từ config
            var section = _configuration.GetSection($"DependentSettings:GroupOrders:{relKey}");

            // section.Exists() = false nghĩa là relationship không có trong config (không xảy ra nếu seed đủ)
            // Kiểm tra groupKey có phải là một key con không
            var validGroups = section.GetChildren().Select(g => g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (validGroups.Count == 0 || !validGroups.Contains(groupKey))
            {
                // Lấy danh sách hợp lệ để hiển thị trong thông báo lỗi
                var validList = validGroups.Count > 0
                    ? string.Join(", ", validGroups)
                    : "(chưa cấu hình)";

                throw new BadRequestException(
                    "GROUP_RELATIONSHIP_MISMATCH",
                    $"Điều kiện đăng ký '{currentGroup}' không thuộc nhóm quan hệ '{relationship}'. " +
                    $"Các điều kiện hợp lệ cho nhóm này: {validList}."
                );
            }
        }

        // ── Helper: thứ tự ưu tiên của nhóm — đọc từ appsettings.json ──────────
        // Số cao hơn = điều kiện "lớn hơn" (không thể đảo ngược theo thời gian).
        // Cấu hình tại: DependentSettings:GroupOrders:{Relationship}:{Group}
        // Trả về -1 nếu không tìm thấy (nhóm không thuộc relationship này).
        private int GetGroupOrder(DependentRelationship relationship, DependentGroup group)
        {
            var configPath = $"DependentSettings:GroupOrders:{relationship}:{group}";
            return _configuration.GetValue<int?>(configPath) ?? -1;
        }

        // ── Helper: danh sách giấy tờ bắt buộc — đọc từ DB + IMemoryCache ────────
        // Nguồn dữ liệu: bảng dependent_document_rules (is_mandatory=true, is_active=true).
        // Cache key: DependentRules_{groupName} — hết hạn sau 1 tiếng.
        // FE không cần thay đổi: response shape (string[]) giữ nguyên.
        private async Task<string[]> GetRequiredDocumentsAsync(DependentGroup group)
        {
            var groupString = group.ToString();
            var cacheKey = $"DependentRules_Required_{groupString}";

            if (!_cache.TryGetValue(cacheKey, out string[]? requiredDocs) || requiredDocs == null)
            {
                var ruleRepo = _unitOfWork.Repository<DependentDocumentRule>();
                var rules = await ruleRepo.FindAsync(r =>
                    r.TargetGroup == groupString &&
                    r.IsMandatory &&
                    r.IsActive
                );

                requiredDocs = rules.Select(r => r.DocType).ToArray();

                // Fallback an toàn: nếu DB chưa có rule cho nhóm này
                if (requiredDocs.Length == 0)
                    requiredDocs = new[] { "OTHER" };

                _cache.Set(cacheKey, requiredDocs,
                    new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromHours(1)));
            }

            return requiredDocs;
        }

        // ── Helper: map Dependent entity → DependentListItemResponse ─────────────
        private static DependentListItemResponse MapToListItem(Dependent d) => new()
        {
            DependentId = d.Id,
            TaxpayerId = d.TaxpayerId,
            FullName = d.FullName,
            Relationship = d.Relationship.ToString(),
            CurrentGroup = d.CurrentGroup.ToString(),
            BirthDate = d.BirthDate,
            CitizenId = d.CitizenId,
            BirthCertNumber = d.BirthCertNumber,
            EffectiveFromMonth = d.EffectiveFromMonth,
            EffectiveToMonth = d.EffectiveToMonth,
            Status = d.Status.ToString(),
            IsProfileComplete = d.IsProfileComplete,
            Note = d.Note,
            CreatedAt = d.CreatedAt
        };

        // ─────────────────────────────────────────────────────────────────────────
        // GET /api/v1/dependents — Danh sách NPT của taxpayer đang đăng nhập
        // ─────────────────────────────────────────────────────────────────────────
        public async Task<PagedResult<DependentListItemResponse>> GetDependentsAsync(
            Guid taxpayerId, DependentQueryParameters query)
        {
            var dependentRepo = _unitOfWork.Repository<Dependent>();

            // 1. Lấy tất cả NPT của taxpayer (chưa bị xóa mềm)
            var all = (await dependentRepo.FindAsync(d =>
                d.TaxpayerId == taxpayerId && !d.IsDeleted)).ToList();

            // 2. Filter theo status
            if (!string.IsNullOrWhiteSpace(query.Status) &&
                Enum.TryParse<DependentStatus>(query.Status.Trim(), true, out var statusFilter))
            {
                all = all.Where(d => d.Status == statusFilter).ToList();
            }

            // 3. Filter theo relationship
            if (!string.IsNullOrWhiteSpace(query.Relationship) &&
                Enum.TryParse<DependentRelationship>(query.Relationship.Trim(), true, out var relFilter))
            {
                all = all.Where(d => d.Relationship == relFilter).ToList();
            }

            // 4. Search theo FullName hoặc CitizenId (case-insensitive)
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var keyword = query.Search.Trim().ToLowerInvariant();
                all = all.Where(d =>
                    d.FullName.ToLowerInvariant().Contains(keyword) ||
                    (d.CitizenId != null && d.CitizenId.Contains(keyword)) ||
                    (d.BirthCertNumber != null && d.BirthCertNumber.ToLowerInvariant().Contains(keyword))
                ).ToList();
            }

            // 5. Sort
            all = (query.Sort?.ToLowerInvariant().TrimStart('-') switch
            {
                "fullname"          => query.Sort.StartsWith("-")
                                       ? all.OrderByDescending(d => d.FullName)
                                       : all.OrderBy(d => d.FullName),
                "effectivefrommonth" => query.Sort.StartsWith("-")
                                       ? all.OrderByDescending(d => d.EffectiveFromMonth)
                                       : all.OrderBy(d => d.EffectiveFromMonth),
                _                   => all.OrderByDescending(d => d.CreatedAt)  // mặc định: mới nhất lên đầu
            }).ToList();

            // 6. Phân trang
            var totalItems = all.Count;
            var pageSize = query.Size;
            var page = query.Page < 1 ? 1 : query.Page;
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var items = all
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(MapToListItem)
                .ToList();

            return new PagedResult<DependentListItemResponse>
            {
                Items = items,
                Pagination = new PaginationMeta
                {
                    Page = page,
                    PageSize = pageSize,
                    TotalItems = totalItems,
                    TotalPages = totalPages < 1 ? 1 : totalPages
                }
            };
        }

        // ─────────────────────────────────────────────────────────────────────────
        // GET /api/v1/dependents/{id} — Chi tiết NPT kèm documents
        // ─────────────────────────────────────────────────────────────────────────
        public async Task<DependentDetailResponse> GetDependentByIdAsync(Guid taxpayerId, Guid dependentId)
        {
            var dependentRepo = _unitOfWork.Repository<Dependent>();

            // 1. Tìm NPT theo id
            var dependent = await dependentRepo.GetByIdAsync(dependentId);

            if (dependent == null || dependent.IsDeleted)
                throw new NotFoundException($"Không tìm thấy người phụ thuộc với Id '{dependentId}'.");

            // 2. Verify chủ sở hữu
            if (dependent.TaxpayerId != taxpayerId)
                throw new ForbiddenException(
                    "Bạn không có quyền xem thông tin người phụ thuộc này.");

            // 3. Lấy Documents của dependent
            var docRepo = _unitOfWork.Repository<DependentDocument>();
            var documents = (await docRepo.FindAsync(doc => doc.DependentId == dependentId)).ToList();

            // 4. Map → response
            var docDtos = documents.Select(doc => new DependentDocumentDto
            {
                DocId = doc.Id,
                DocType = doc.DocType.ToString(),
                FileUrl = doc.FileUrl,
                FileMimeType = doc.FileMimeType,
                IsReadable = doc.IsReadable,
                UploadedAt = doc.UploadedAt
            }).ToList();

            return new DependentDetailResponse
            {
                DependentId = dependent.Id,
                TaxpayerId = dependent.TaxpayerId,
                FullName = dependent.FullName,
                Relationship = dependent.Relationship.ToString(),
                CurrentGroup = dependent.CurrentGroup.ToString(),
                BirthDate = dependent.BirthDate,
                CitizenId = dependent.CitizenId,
                BirthCertNumber = dependent.BirthCertNumber,
                TaxIdNumber = dependent.TaxIdNumber,
                EffectiveFromMonth = dependent.EffectiveFromMonth,
                EffectiveToMonth = dependent.EffectiveToMonth,
                Status = dependent.Status.ToString(),
                IsProfileComplete = dependent.IsProfileComplete,
                Note = dependent.Note,
                CreatedAt = dependent.CreatedAt,
                UpdatedAt = dependent.UpdatedAt,
                RequiredDocuments = await GetRequiredDocumentsAsync(dependent.CurrentGroup),
                Documents = docDtos
            };
        }

        // ─────────────────────────────────────────────────────────────────────────
        // PATCH /api/v1/dependents/{id}/group — Chuyển nhóm NPT
        // ─────────────────────────────────────────────────────────────────────────
        public async Task<UpdateDependentGroupResponse> UpdateGroupAsync(
            Guid taxpayerId, Guid dependentId, UpdateDependentGroupRequest request)
        {
            var dependentRepo = _unitOfWork.Repository<Dependent>();

            // ── 1. Tìm NPT theo id ───────────────────────────────────────────────
            var dependent = await dependentRepo.GetByIdAsync(dependentId);

            if (dependent == null || dependent.IsDeleted)
                throw new NotFoundException($"Không tìm thấy người phụ thuộc với Id '{dependentId}'.");

            // ── 2. Kiểm tra chủ sở hữu ──────────────────────────────────────────
            if (dependent.TaxpayerId != taxpayerId)
                throw new ForbiddenException(
                    "Bạn không có quyền cập nhật thông tin người phụ thuộc này.");

            // ── 3. Parse và validate NewGroup enum ──────────────────────────────
            if (!Enum.TryParse<DependentGroup>(request.NewGroup, true, out var newGroup))
            {
                throw new BadRequestException(
                    "INVALID_GROUP",
                    $"Nhóm điều kiện '{request.NewGroup}' không hợp lệ. " +
                    "Các giá trị hợp lệ: CHILD_UNDER_18, CHILD_OVER_18_DISABLED, CHILD_OVER_18_STUDYING, " +
                    "SPOUSE_DISABLED, SPOUSE_RETIRED, PARENT_DISABLED, PARENT_RETIRED, OTHER_HELPLESS."
                );
            }

            // ── 4. Không cho phép chuyển cùng nhóm ──────────────────────────────
            if (newGroup == dependent.CurrentGroup)
            {
                throw new BadRequestException(
                    "SAME_GROUP",
                    $"Người phụ thuộc đã ở nhóm '{newGroup}'. Vui lòng chọn nhóm khác để chuyển."
                );
            }

            // ── 4.5. Validate NewGroup phải thuộc cùng Relationship ────────────
            // Phải kiểm tra trước: nếu chọn sai Relationship thì báo lỗi GROUP_RELATIONSHIP_MISMATCH
            // rõ ràng hơn là bị lọt xuống check downgrade (ra lỗi sai ngữ nghĩa).
            ValidateGroupMatchesRelationship(dependent.Relationship, newGroup);

            // ── 5. Không cho phép chuyển xuống nhóm thấp hơn (downgrade) ─────────
            // Ví dụ: CHILD_OVER_18_STUDYING → CHILD_UNDER_18 là không hợp lệ vì
            // độ tuổi chỉ tăng theo thời gian, không thể trẻ lại.
            var currentOrder = GetGroupOrder(dependent.Relationship, dependent.CurrentGroup);
            var newOrder     = GetGroupOrder(dependent.Relationship, newGroup);

            if (newOrder < currentOrder)
            {
                throw new BadRequestException(
                    "GROUP_DOWNGRADE_NOT_ALLOWED",
                    $"Không thể chuyển người phụ thuộc từ nhóm '{dependent.CurrentGroup}' " +
                    $"xuống nhóm thấp hơn '{newGroup}'. " +
                    "Nhóm mới phải tương đương hoặc cao hơn nhóm hiện tại."
                );
            }

            // ── 6. Lưu nhóm cũ để đưa vào response ─────────────────────────────
            var previousGroup = dependent.CurrentGroup;

            // ── 7. Cập nhật entity ───────────────────────────────────────────────
            dependent.CurrentGroup = newGroup;
            dependent.IsProfileComplete = false;
            dependent.Status = DependentStatus.PENDING_DOCUMENTS;
            dependent.Note = request.Note ?? dependent.Note; // null giữ nguyên ghi chú cũ
            dependent.UpdatedAt = DateTimeOffset.UtcNow;

            dependentRepo.Update(dependent);

            // ── 8. Tạo SystemNotification thông báo cho user ─────────────────────
            var requiredDocs = await GetRequiredDocumentsAsync(newGroup);
            var docList = string.Join(", ", requiredDocs);

            var notification = new SystemNotification
            {
                NotificationId = Guid.NewGuid(),
                UserId = taxpayerId,
                Title = $"Hồ sơ người phụ thuộc '{dependent.FullName}' cần cập nhật giấy tờ",
                Message = $"Người phụ thuộc '{dependent.FullName}' vừa được chuyển từ nhóm " +
                          $"'{previousGroup}' sang nhóm '{newGroup}'. " +
                          $"Vui lòng bổ sung các giấy tờ cần thiết: {docList}.",
                NotificationType = "DEPENDENT_GROUP_TRANSITION",
                IsRead = false,
                TargetActionUrl = $"/dependents/{dependentId}/documents",
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Repository<SystemNotification>().AddAsync(notification);
            await _unitOfWork.SaveChangesAsync();

            // ── 9. Lấy danh sách documents đã upload ───────────────────────────
            var docRepo = _unitOfWork.Repository<DependentDocument>();
            var documents = (await docRepo.FindAsync(doc => doc.DependentId == dependentId)).ToList();

            var docDtos = documents.Select(doc => new DependentDocumentDto
            {
                DocId = doc.Id,
                DocType = doc.DocType.ToString(),
                FileUrl = doc.FileUrl,
                FileMimeType = doc.FileMimeType,
                IsReadable = doc.IsReadable,
                UploadedAt = doc.UploadedAt
            }).ToList();

            // ── 10. Trả về response ─────────────────────────────────────────────
            return new UpdateDependentGroupResponse
            {
                DependentId = dependent.Id,
                TaxpayerId = dependent.TaxpayerId,
                FullName = dependent.FullName,
                BirthDate = dependent.BirthDate,
                CitizenId = dependent.CitizenId,
                BirthCertNumber = dependent.BirthCertNumber,
                TaxIdNumber = dependent.TaxIdNumber,
                Relationship = dependent.Relationship.ToString(),
                EffectiveFromMonth = dependent.EffectiveFromMonth,
                EffectiveToMonth = dependent.EffectiveToMonth,
                Note = dependent.Note,
                CreatedAt = dependent.CreatedAt,
                UpdatedAt = dependent.UpdatedAt,
                PreviousGroup = previousGroup.ToString(),
                CurrentGroup = dependent.CurrentGroup.ToString(),
                Status = dependent.Status.ToString(),
                IsProfileComplete = false,
                RequiredDocuments = requiredDocs,
                Documents = docDtos
            };
        }

        // ─────────────────────────────────────────────────────────────────────────
        // DELETE /api/v1/dependents/{id} — Xóa mềm người phụ thuộc
        // ─────────────────────────────────────────────────────────────────────────
        public async Task<DeleteDependentResponse> DeleteDependentAsync(
            Guid taxpayerId, Guid dependentId, DeleteDependentRequest request)
        {
            var dependentRepo = _unitOfWork.Repository<Dependent>();

            // ── 1. Tìm NPT ──────────────────────────────────────────────────────
            var dependent = await dependentRepo.GetByIdAsync(dependentId);

            if (dependent == null || dependent.IsDeleted)
                throw new NotFoundException($"Không tìm thấy người phụ thuộc với Id '{dependentId}'.");

            // ── 2. Kiểm tra chủ sở hữu ──────────────────────────────────────────
            if (dependent.TaxpayerId != taxpayerId)
                throw new ForbiddenException(
                    "Bạn không có quyền vô hiệu hóa người phụ thuộc này.");

            var deletedAt = DateTimeOffset.UtcNow;

            // ── 3. Soft delete ───────────────────────────────────────────────────
            dependent.IsDeleted  = true;
            dependent.Status     = DependentStatus.INACTIVE;
            dependent.UpdatedAt  = deletedAt;

            // Ghi lý do vào Note nếu có (giữ nguyên note cũ nếu không cung cấp)
            if (!string.IsNullOrWhiteSpace(request?.Reason))
                dependent.Note = $"[Vô hiệu hóa] {request.Reason}";

            dependentRepo.Update(dependent);

            // ── 4. Tạo SystemNotification ────────────────────────────────────────
            var reasonText = string.IsNullOrWhiteSpace(request?.Reason)
                ? "Không có lý do cụ thể."
                : request.Reason;

            var notification = new SystemNotification
            {
                NotificationId    = Guid.NewGuid(),
                UserId            = taxpayerId,
                Title             = $"Người phụ thuộc '{dependent.FullName}' đã bị vô hiệu hóa",
                Message           = $"Hồ sơ người phụ thuộc '{dependent.FullName}' " +
                                    $"đã được vô hiệu hóa và không còn được tính giảm trừ gia cảnh. " +
                                    $"Lý do: {reasonText}",
                NotificationType  = "DEPENDENT_DEACTIVATED",
                IsRead            = false,
                TargetActionUrl   = $"/dependents/{dependentId}",
                CreatedAt         = deletedAt.UtcDateTime
            };

            await _unitOfWork.Repository<SystemNotification>().AddAsync(notification);
            await _unitOfWork.SaveChangesAsync();

            // ── 5. Trả về response ───────────────────────────────────────────────
            return new DeleteDependentResponse
            {
                DependentId  = dependent.Id,
                FullName     = dependent.FullName,
                Relationship = dependent.Relationship.ToString(),
                CurrentGroup = dependent.CurrentGroup.ToString(),
                Status       = dependent.Status.ToString(),
                IsDeleted    = true,
                Reason       = request?.Reason,
                DeletedAt    = deletedAt
            };
        }
    }
}
