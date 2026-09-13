using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        public DependentService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
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
                RequiredDocuments = GetRequiredDocuments(currentGroup)
            };
        }

        // ── Helper: validate CurrentGroup phải khớp Relationship ─────────────────
        private static void ValidateGroupMatchesRelationship(
            DependentRelationship relationship,
            DependentGroup currentGroup)
        {
            var isValid = relationship switch
            {
                DependentRelationship.CHILD => currentGroup is
                    DependentGroup.CHILD_UNDER_18 or
                    DependentGroup.CHILD_OVER_18_DISABLED or
                    DependentGroup.CHILD_OVER_18_STUDYING,

                DependentRelationship.SPOUSE => currentGroup is
                    DependentGroup.SPOUSE_DISABLED or
                    DependentGroup.SPOUSE_RETIRED,

                DependentRelationship.PARENT => currentGroup is
                    DependentGroup.PARENT_DISABLED or
                    DependentGroup.PARENT_RETIRED,

                DependentRelationship.OTHER_DEPENDENT => currentGroup is
                    DependentGroup.OTHER_HELPLESS,

                _ => false
            };

            if (!isValid)
            {
                throw new BadRequestException(
                    "GROUP_RELATIONSHIP_MISMATCH",
                    $"Điều kiện đăng ký '{currentGroup}' không thuộc nhóm quan hệ '{relationship}'. " +
                    "Vui lòng kiểm tra lại điều kiện phù hợp với loại người phụ thuộc."
                );
            }
        }

        // ── Helper: danh sách giấy tờ bắt buộc theo DependentGroup ───────────────
        private static string[] GetRequiredDocuments(DependentGroup group)
        {
            return group switch
            {
                // ── Nhóm 1: Con ──────────────────────────────────────────────────
                DependentGroup.CHILD_UNDER_18 => new[]
                {
                    "BIRTH_CERTIFICATE",    // Giấy khai sinh (bắt buộc)
                    "CITIZEN_ID"            // CCCD (nếu đã có, không bắt buộc với trẻ nhỏ)
                },

                DependentGroup.CHILD_OVER_18_DISABLED => new[]
                {
                    "DISABILITY_CERTIFICATE" // Giấy xác nhận mức độ khuyết tật hoặc hồ sơ bệnh án
                },

                DependentGroup.CHILD_OVER_18_STUDYING => new[]
                {
                    "STUDENT_CARD"          // Thẻ học sinh/sinh viên hoặc Giấy xác nhận đang theo học
                },

                // ── Nhóm 2: Vợ/Chồng ─────────────────────────────────────────────
                DependentGroup.SPOUSE_DISABLED => new[]
                {
                    "CITIZEN_ID",           // CCCD của vợ/chồng (bắt buộc)
                    "OTHER"                 // Giấy chứng nhận kết hôn (bắt buộc)
                },

                DependentGroup.SPOUSE_RETIRED => new[]
                {
                    "CITIZEN_ID",           // CCCD của vợ/chồng (bắt buộc)
                    "OTHER"                 // Giấy chứng nhận kết hôn (bắt buộc)
                },

                // ── Nhóm 3: Cha/Mẹ ───────────────────────────────────────────────
                DependentGroup.PARENT_DISABLED => new[]
                {
                    "CITIZEN_ID",           // CCCD của cha/mẹ (bắt buộc)
                    "OTHER"                 // Giấy tờ chứng minh quan hệ phụ tử/mẫu tử (GKS của NNT hoặc quyết định nhận con nuôi)
                },

                DependentGroup.PARENT_RETIRED => new[]
                {
                    "CITIZEN_ID",           // CCCD của cha/mẹ (bắt buộc)
                    "OTHER"                 // Giấy tờ chứng minh quan hệ phụ tử/mẫu tử
                },

                // ── Nhóm 4: Cá nhân khác không nơi nương tựa ─────────────────────
                DependentGroup.OTHER_HELPLESS => new[]
                {
                    "CITIZEN_ID",           // CCCD của người phụ thuộc (bắt buộc)
                    "OTHER"                 // Giấy tờ pháp lý chứng minh quan hệ huyết thống/họ hàng + Mẫu số 07/XN-NPT-TNCN
                },

                _ => new[] { "OTHER" }
            };
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
                RequiredDocuments = GetRequiredDocuments(dependent.CurrentGroup),
                Documents = docDtos
            };
        }
    }
}
