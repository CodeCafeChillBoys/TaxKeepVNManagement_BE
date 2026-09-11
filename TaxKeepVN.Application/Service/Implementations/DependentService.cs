using System;
using System.Linq;
using System.Threading.Tasks;
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
    }
}
