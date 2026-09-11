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

            // ── 2. Validate định danh: phải có CitizenId HOẶC BirthCertNumber ──
            var hasCitizenId = !string.IsNullOrWhiteSpace(request.CitizenId);
            var hasBirthCert = !string.IsNullOrWhiteSpace(request.BirthCertNumber);

            if (!hasCitizenId && !hasBirthCert)
            {
                throw new BadRequestException(
                    "IDENTIFIER_REQUIRED",
                    "Phải cung cấp ít nhất Số CCCD hoặc Số Giấy khai sinh của người phụ thuộc."
                );
            }

            // ── 3. Validate khoảng thời gian: From <= To ───────────────────────
            if (string.Compare(request.EffectiveFromMonth, request.EffectiveToMonth, StringComparison.Ordinal) > 0)
            {
                throw new BadRequestException(
                    "INVALID_PERIOD",
                    "Tháng bắt đầu tính giảm trừ phải nhỏ hơn hoặc bằng tháng kết thúc."
                );
            }

            // ── 4. Kiểm tra trùng lặp thời gian ────────────────────────────────
            // Hai khoảng [A_from, A_to] và [B_from, B_to] overlap khi:
            //   A_from <= B_to AND A_to >= B_from
            // So sánh chuỗi "YYYY-MM" hoạt động đúng vì format chuẩn.
            var dependentRepo = _unitOfWork.Repository<Dependent>();

            if (hasCitizenId)
            {
                // Bước 1: Query DB chỉ theo CitizenId (EF Core dịch được)
                var existingByCitizenId = await dependentRepo.FindAsync(d =>
                    d.CitizenId == request.CitizenId);

                // Bước 2: Kiểm tra overlap trong bộ nhớ (in-memory)
                // [A_from, A_to] overlap [B_from, B_to]  ↔  A_from <= B_to AND A_to >= B_from
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

            // ── 5. Tạo mới Dependent ────────────────────────────────────────────
            var newDependent = new Dependent
            {
                Id = Guid.NewGuid(),
                TaxpayerId = taxpayerId,
                FullName = request.FullName,
                Relationship = relationship,
                DateOfBirth = request.DateOfBirth,
                CitizenId = request.CitizenId?.Trim(),
                BirthCertNumber = request.BirthCertNumber?.Trim(),
                TaxIdNumber = request.TaxIdNumber?.Trim(),
                EffectiveFromMonth = request.EffectiveFromMonth,
                EffectiveToMonth = request.EffectiveToMonth,
                Note = request.Note,
                Status = DependentStatus.PENDING_DOCUMENTS,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            await dependentRepo.AddAsync(newDependent);
            await _unitOfWork.SaveChangesAsync();

            // ── 6. Trả về response kèm danh sách giấy tờ cần upload ─────────────
            return new DependentResponse
            {
                DependentId = newDependent.Id,
                TaxpayerId = newDependent.TaxpayerId,
                FullName = newDependent.FullName,
                Relationship = newDependent.Relationship.ToString(),
                DateOfBirth = newDependent.DateOfBirth,
                CitizenId = newDependent.CitizenId,
                BirthCertNumber = newDependent.BirthCertNumber,
                TaxIdNumber = newDependent.TaxIdNumber,
                EffectiveFromMonth = newDependent.EffectiveFromMonth,
                EffectiveToMonth = newDependent.EffectiveToMonth,
                Note = newDependent.Note,
                Status = newDependent.Status.ToString(),
                CreatedAt = newDependent.CreatedAt,
                UpdatedAt = newDependent.UpdatedAt,
                RequiredDocuments = GetRequiredDocuments(relationship)
            };
        }

        // ── Helper: gợi ý giấy tờ cần upload theo từng nhóm quan hệ ─────────────
        private static string[] GetRequiredDocuments(DependentRelationship relationship)
        {
            return relationship switch
            {
                DependentRelationship.CHILD => new[]
                {
                    "BIRTH_CERTIFICATE",    // Giấy khai sinh
                    "CITIZEN_ID",           // CCCD (nếu đã có)
                    "STUDENT_CARD"          // Thẻ học sinh/sinh viên (nếu từ 18-25 tuổi)
                },
                DependentRelationship.SPOUSE => new[]
                {
                    "CITIZEN_ID",           // CCCD vợ/chồng
                    "OTHER"                 // Giấy đăng ký kết hôn
                },
                DependentRelationship.PARENT => new[]
                {
                    "CITIZEN_ID",           // CCCD cha/mẹ
                    "PENSION_STATEMENT",    // Giấy xác nhận không thu nhập / Quyết định hưu trí
                    "OTHER"                 // Giấy tờ chứng minh quan hệ huyết thống
                },
                DependentRelationship.OTHER_DEPENDENT => new[]
                {
                    "CITIZEN_ID",           // CCCD người phụ thuộc
                    "DISABILITY_CERTIFICATE", // Giấy xác nhận khuyết tật (nếu có)
                    "OTHER"                 // Giấy tờ chứng minh không nơi nương tựa
                },
                _ => new[] { "OTHER" }
            };
        }
    }
}
