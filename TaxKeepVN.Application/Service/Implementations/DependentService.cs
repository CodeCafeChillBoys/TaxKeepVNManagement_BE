using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using TaxKeepVN.Application.Constants;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Requests.Dependent;
using TaxKeepVN.Application.DTOs.Responses.Dependent;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Helpers;
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
        private readonly IRedisCacheService? _redisCache;

        public DependentService(
            IUnitOfWork unitOfWork,
            IMemoryCache cache,
            IConfiguration configuration,
            IRedisCacheService? redisCache = null)
        {
            _unitOfWork = unitOfWork;
            _cache = cache;
            _configuration = configuration;
            _redisCache = redisCache;
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

            // ── 3.5. Validate độ tuổi phù hợp với CurrentGroup nếu có BirthDate ─
            if (request.BirthDate.HasValue)
            {
                ValidateGroupMatchesAge(request.BirthDate.Value, currentGroup);
            }

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

            // ── 5. Validate khoảng thời gian: From <= To và From >= BirthDate ──
            var birthDate = request.BirthDate.HasValue
                ? DateTime.SpecifyKind(request.BirthDate.Value, DateTimeKind.Utc)
                : default;
            ValidateEffectivePeriod(birthDate, request.EffectiveFromMonth, request.EffectiveToMonth);

            // ── 5.5. Chặn theo hồ sơ quyết toán đã chốt (TaxSettlementDossier LOCKED) ─
            var affectedYears = PeriodHelper.GetYearsInRange(request.EffectiveFromMonth, request.EffectiveToMonth);
            var settlementRepo = _unitOfWork.Repository<TaxSettlementDossier>();
            if (settlementRepo != null && affectedYears.Count > 0)
            {
                var lockedDossiers = (await settlementRepo.FindAsync(d =>
                    d.TaxpayerId == taxpayerId &&
                    d.Status == "LOCKED")).ToList();

                var lockedYears = lockedDossiers
                    .Where(d => affectedYears.Contains(d.TaxYear))
                    .Select(d => d.TaxYear)
                    .OrderBy(y => y)
                    .ToList();

                if (lockedYears.Count > 0)
                {
                    throw new ConflictException(
                        ErrorCodes.Dependent.SettlementLockedPeriod,
                        $"Năm {string.Join(", ", lockedYears)} đã chốt hồ sơ quyết toán, không thể thay đổi thời gian hiệu lực trong năm này."
                    );
                }
            }

            // ── 6. Kiểm tra trùng lặp thời gian ────────────────────────────────
            // Hai khoảng [A_from, A_to] và [B_from, B_to] overlap khi:
            //   A_from <= B_to AND A_to >= B_from
            var dependentRepo = _unitOfWork.Repository<Dependent>();

            if (hasCitizenId)
            {
                // Bước 1: Query DB chỉ theo CitizenId (EF Core dịch được)
                var existingByCitizenId = await dependentRepo.FindAsync(d =>
                    d.CitizenId == request.CitizenId && !d.IsDeleted);

                // Bước 2: Kiểm tra overlap trong bộ nhớ (in-memory)
                var conflict = existingByCitizenId.FirstOrDefault(d =>
                    string.Compare(d.EffectiveFromMonth, request.EffectiveToMonth, StringComparison.Ordinal) <= 0 &&
                    string.Compare(d.EffectiveToMonth, request.EffectiveFromMonth, StringComparison.Ordinal) >= 0
                );

                if (conflict != null)
                {
                    throw new BadRequestException(
                        ErrorCodes.Dependent.DependentPeriodOverlap,
                        $"Người phụ thuộc có số CCCD '{MaskIdentifier(request.CitizenId!)}' đã được đăng ký " +
                        $"trong khoảng thời gian từ {conflict.EffectiveFromMonth} đến {conflict.EffectiveToMonth}. " +
                        "Một người phụ thuộc không thể được đăng ký trùng kỳ tính giảm trừ bởi nhiều người nộp thuế."
                    );
                }
            }

            if (hasBirthCert)
            {
                // Bước 1: Query DB chỉ theo BirthCertNumber (EF Core dịch được)
                var existingByBirthCert = await dependentRepo.FindAsync(d =>
                    d.BirthCertNumber == request.BirthCertNumber && !d.IsDeleted);

                // Bước 2: Kiểm tra overlap trong bộ nhớ (in-memory)
                var conflict = existingByBirthCert.FirstOrDefault(d =>
                    string.Compare(d.EffectiveFromMonth, request.EffectiveToMonth, StringComparison.Ordinal) <= 0 &&
                    string.Compare(d.EffectiveToMonth, request.EffectiveFromMonth, StringComparison.Ordinal) >= 0
                );

                if (conflict != null)
                {
                    throw new BadRequestException(
                        ErrorCodes.Dependent.DependentPeriodOverlap,
                        $"Người phụ thuộc có số Giấy khai sinh '{MaskIdentifier(request.BirthCertNumber!)}' đã được đăng ký " +
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
                RequiredDocuments = await GetRequiredDocumentsAsync(currentGroup),
                MissingDocuments = (await GetRequiredDocumentsAsync(currentGroup)).ToArray()
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

        // ── Helper: lấy tên hiển thị tiếng Việt thân thiện cho DependentGroup ────
        public static string GetGroupDisplayName(DependentGroup group) => group switch
        {
            DependentGroup.CHILD_UNDER_18 => "Con dưới 18 tuổi",
            DependentGroup.CHILD_OVER_18_STUDYING => "Con từ 18 tuổi trở lên đang đi học",
            DependentGroup.CHILD_OVER_18_DISABLED => "Con từ 18 tuổi trở lên bị khuyết tật",
            DependentGroup.SPOUSE_DISABLED => "Vợ/Chồng khuyết tật",
            DependentGroup.SPOUSE_RETIRED => "Vợ/Chồng ngoài độ tuổi lao động",
            DependentGroup.PARENT_DISABLED => "Cha/Mẹ khuyết tật",
            DependentGroup.PARENT_RETIRED => "Cha/Mẹ ngoài độ tuổi lao động",
            DependentGroup.OTHER_HELPLESS => "Cá nhân không nơi nương tựa khác",
            _ => group.ToString()
        };

        // ── Helper: validate độ tuổi phải phù hợp với DependentGroup ─────────────
        private static void ValidateGroupMatchesAge(DateTime birthDate, DependentGroup group)
        {
            if (birthDate == default || birthDate == DateTime.MinValue)
                return;

            var today = DateTime.UtcNow;
            var age = today.Year - birthDate.Year;
            if (birthDate.Date > today.AddYears(-age))
            {
                age--;
            }

            if (group == DependentGroup.CHILD_UNDER_18 && age >= 18)
            {
                throw new BadRequestException(
                    ErrorCodes.Dependent.GroupAgeMismatch,
                    $"Người phụ thuộc hiện tại {age} tuổi (đã từ đủ 18 tuổi trở lên). " +
                    "Không thể chuyển hoặc đăng ký vào nhóm 'Con dưới 18 tuổi'."
                );
            }

            if ((group == DependentGroup.CHILD_OVER_18_STUDYING || group == DependentGroup.CHILD_OVER_18_DISABLED) && age < 18)
            {
                throw new BadRequestException(
                    ErrorCodes.Dependent.GroupAgeMismatch,
                    $"Người phụ thuộc hiện tại {age} tuổi (chưa đủ 18 tuổi). " +
                    $"Không thể chuyển sang nhóm '{GetGroupDisplayName(group)}'. " +
                    "Nhóm này chỉ áp dụng cho con từ đủ 18 tuổi trở lên."
                );
            }
        }

        // ── Helper: validate khoảng thời gian giảm trừ: From <= To và From >= BirthDate ──
        private static void ValidateEffectivePeriod(DateTime birthDate, string effectiveFromMonth, string effectiveToMonth)
        {
            if (string.Compare(effectiveFromMonth, effectiveToMonth, StringComparison.Ordinal) > 0)
            {
                throw new BadRequestException(
                    ErrorCodes.Dependent.InvalidPeriod,
                    "Tháng bắt đầu tính giảm trừ phải nhỏ hơn hoặc bằng tháng kết thúc."
                );
            }

            if (birthDate != default && birthDate != DateTime.MinValue)
            {
                var birthMonth = birthDate.ToString("yyyy-MM", CultureInfo.InvariantCulture);
                var birthDisplay = birthDate.ToString("MM/yyyy", CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(effectiveFromMonth) &&
                    string.Compare(effectiveFromMonth, birthMonth, StringComparison.Ordinal) < 0)
                {
                    throw new BadRequestException(
                        ErrorCodes.Dependent.PeriodBeforeBirth,
                        $"Tháng bắt đầu giảm trừ không được trước tháng sinh của người phụ thuộc ({birthDisplay})."
                    );
                }
            }
        }

        // ── Helper: che số giấy tờ (chỉ hiện 4 số cuối) ───────────────────────
        private static string MaskIdentifier(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return string.Empty;
            var trimmed = id.Trim();
            if (trimmed.Length <= 4) return trimmed;
            return new string('*', trimmed.Length - 4) + trimmed[^4..];
        }

        // ── Helper: danh sách giấy tờ bắt buộc — đọc từ DB + IMemoryCache + Redis ────────
        private async Task<string[]> GetRequiredDocumentsAsync(DependentGroup group)
        {
            var groupString = group.ToString();
            var cacheKey = $"DependentRules_Required_{groupString}";
            var redisKey = $"taxkeep:dependent_rules:required:{groupString}";

            if (_redisCache != null)
            {
                var cachedRedis = await _redisCache.GetAsync<string[]>(redisKey);
                if (cachedRedis != null && cachedRedis.Length > 0)
                    return cachedRedis;
            }

            if (!_cache.TryGetValue(cacheKey, out string[]? requiredDocs) || requiredDocs == null)
            {
                var ruleRepo = _unitOfWork.Repository<DependentDocumentRule>();
                var rules = ruleRepo != null
                    ? (await ruleRepo.FindAsync(r =>
                        r.TargetGroup == groupString &&
                        r.IsMandatory &&
                        r.IsActive)).ToList()
                    : new List<DependentDocumentRule>();

                requiredDocs = rules.Select(r => r.DocType).ToArray();

                // Fallback an toàn: nếu DB chưa có rule cho nhóm này
                if (requiredDocs.Length == 0)
                    requiredDocs = new[] { "OTHER" };

                _cache.Set(cacheKey, requiredDocs,
                    new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromHours(1)));

                if (_redisCache != null)
                {
                    await _redisCache.SetAsync(redisKey, requiredDocs, TimeSpan.FromHours(24));
                }
            }

            return requiredDocs;
        }

        // ── Helper: danh sách giấy tờ còn thiếu ─────────────────────────────────
        private async Task<string[]> GetMissingDocumentsAsync(
            DependentGroup group,
            bool isProfileComplete,
            List<string> uploadedDocTypes)
        {
            if (isProfileComplete)
                return Array.Empty<string>();

            var groupString = group.ToString();
            var cacheKey = $"DependentRules_Active_{groupString}";
            var redisKey = $"taxkeep:dependent_rules:active:{groupString}";

            List<DependentDocumentRule>? activeRules = null;
            if (_redisCache != null)
            {
                activeRules = await _redisCache.GetAsync<List<DependentDocumentRule>>(redisKey);
            }

            if (activeRules == null && (!_cache.TryGetValue(cacheKey, out activeRules) || activeRules == null))
            {
                var ruleRepo = _unitOfWork.Repository<DependentDocumentRule>();
                activeRules = ruleRepo != null
                    ? (await ruleRepo.FindAsync(r => r.TargetGroup == groupString && r.IsActive)).ToList()
                    : new List<DependentDocumentRule>();

                _cache.Set(cacheKey, activeRules,
                    new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromHours(1)));

                if (_redisCache != null)
                {
                    await _redisCache.SetAsync(redisKey, activeRules, TimeSpan.FromHours(24));
                }
            }

            var uploadedNames = uploadedDocTypes
                .Select(DependentRuleService.NormalizeDocType)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missing = new List<string>();

            if (activeRules.Count > 0)
            {
                if (group == DependentGroup.CHILD_UNDER_18)
                {
                    // Theo Thông tư 87: Con dưới 18 tuổi cần Giấy khai sinh HOẶC Căn cước công dân
                    bool hasBirthCert = uploadedNames.Contains("BIRTH_CERTIFICATE");
                    bool hasCitizenId = uploadedNames.Contains("CITIZEN_ID");
                    if (!hasBirthCert && !hasCitizenId)
                    {
                        missing.Add("BIRTH_CERTIFICATE");
                    }

                    var otherMandatory = activeRules.Where(r => r.IsMandatory &&
                        !r.DocType.Equals("BIRTH_CERTIFICATE", StringComparison.OrdinalIgnoreCase) &&
                        !r.DocType.Equals("CITIZEN_ID", StringComparison.OrdinalIgnoreCase));

                    foreach (var r in otherMandatory)
                    {
                        if (!uploadedNames.Contains(r.DocType))
                            missing.Add(r.DocType);
                    }
                }
                else if (group == DependentGroup.PARENT_DISABLED || group == DependentGroup.PARENT_RETIRED)
                {
                    // Theo Thông tư 111/2013 và Thông tư 79/2022: Minh chứng quan hệ cha/mẹ gồm Giấy khai sinh HOẶC Giấy xác nhận quan hệ/cư trú (CT07, hộ khẩu)
                    bool hasRelationshipProof = uploadedNames.Contains("RELATIONSHIP_CERTIFICATE") || uploadedNames.Contains("BIRTH_CERTIFICATE");

                    bool requiresRelationshipProof = activeRules.Any(r => r.IsMandatory &&
                        (r.DocType.Equals("BIRTH_CERTIFICATE", StringComparison.OrdinalIgnoreCase) ||
                         r.DocType.Equals("RELATIONSHIP_CERTIFICATE", StringComparison.OrdinalIgnoreCase)));

                    if (requiresRelationshipProof && !hasRelationshipProof)
                    {
                        var prefRule = activeRules.FirstOrDefault(r => r.IsMandatory && r.DocType.Equals("RELATIONSHIP_CERTIFICATE", StringComparison.OrdinalIgnoreCase))
                            ?? activeRules.FirstOrDefault(r => r.IsMandatory && r.DocType.Equals("BIRTH_CERTIFICATE", StringComparison.OrdinalIgnoreCase));
                        missing.Add(prefRule?.DocType ?? "RELATIONSHIP_CERTIFICATE");
                    }

                    var otherMandatory = activeRules.Where(r => r.IsMandatory &&
                        !r.DocType.Equals("BIRTH_CERTIFICATE", StringComparison.OrdinalIgnoreCase) &&
                        !r.DocType.Equals("RELATIONSHIP_CERTIFICATE", StringComparison.OrdinalIgnoreCase));

                    foreach (var r in otherMandatory)
                    {
                        if (!uploadedNames.Contains(r.DocType))
                            missing.Add(r.DocType);
                    }
                }
                else
                {
                    var mandatoryRules = activeRules.Where(r => r.IsMandatory);
                    foreach (var rule in mandatoryRules)
                    {
                        if (!uploadedNames.Contains(rule.DocType))
                            missing.Add(rule.DocType);
                    }
                }
            }
            else
            {
                // Fallback theo quy định thuế khi chưa seed rules
                switch (group)
                {
                    case DependentGroup.CHILD_UNDER_18:
                        if (!uploadedNames.Contains("BIRTH_CERTIFICATE") && !uploadedNames.Contains("CITIZEN_ID"))
                            missing.Add("BIRTH_CERTIFICATE");
                        break;
                    case DependentGroup.CHILD_OVER_18_DISABLED:
                        if (!uploadedNames.Contains("CITIZEN_ID")) missing.Add("CITIZEN_ID");
                        if (!uploadedNames.Contains("DISABILITY_CERTIFICATE")) missing.Add("DISABILITY_CERTIFICATE");
                        break;
                    case DependentGroup.CHILD_OVER_18_STUDYING:
                        if (!uploadedNames.Contains("CITIZEN_ID")) missing.Add("CITIZEN_ID");
                        if (!uploadedNames.Contains("STUDENT_CARD")) missing.Add("STUDENT_CARD");
                        break;
                    case DependentGroup.SPOUSE_DISABLED:
                    case DependentGroup.SPOUSE_RETIRED:
                        if (!uploadedNames.Contains("CITIZEN_ID")) missing.Add("CITIZEN_ID");
                        if (!uploadedNames.Contains("MARRIAGE_CERTIFICATE")) missing.Add("MARRIAGE_CERTIFICATE");
                        break;
                    case DependentGroup.PARENT_DISABLED:
                        if (!uploadedNames.Contains("CITIZEN_ID")) missing.Add("CITIZEN_ID");
                        if (!uploadedNames.Contains("RELATIONSHIP_CERTIFICATE") && !uploadedNames.Contains("BIRTH_CERTIFICATE"))
                            missing.Add("RELATIONSHIP_CERTIFICATE");
                        if (!uploadedNames.Contains("DISABILITY_CERTIFICATE")) missing.Add("DISABILITY_CERTIFICATE");
                        break;
                    case DependentGroup.PARENT_RETIRED:
                        if (!uploadedNames.Contains("CITIZEN_ID")) missing.Add("CITIZEN_ID");
                        if (!uploadedNames.Contains("RELATIONSHIP_CERTIFICATE") && !uploadedNames.Contains("BIRTH_CERTIFICATE"))
                            missing.Add("RELATIONSHIP_CERTIFICATE");
                        break;
                    case DependentGroup.OTHER_HELPLESS:
                        if (!uploadedNames.Contains("CITIZEN_ID")) missing.Add("CITIZEN_ID");
                        if (!uploadedNames.Contains("RELATIONSHIP_CERTIFICATE")) missing.Add("RELATIONSHIP_CERTIFICATE");
                        if (!uploadedNames.Contains("SUPPORT_COMMITMENT_FORM")) missing.Add("SUPPORT_COMMITMENT_FORM");
                        break;
                }
            }

            return missing.ToArray();
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

            var pagedDependents = all
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var pagedIds = pagedDependents.Select(d => d.Id).ToList();
            var docRepo = _unitOfWork.Repository<DependentDocument>();
            var allDocs = docRepo != null && pagedIds.Count > 0
                ? (await docRepo.FindAsync(doc => pagedIds.Contains(doc.DependentId))).ToList()
                : new List<DependentDocument>();
            var depMap = pagedDependents.ToDictionary(d => d.Id);
            var docsByDependentId = allDocs
                .Where(doc => {
                    var dep = depMap.GetValueOrDefault(doc.DependentId);
                    return dep == null || !dep.IdentityUpdatedAt.HasValue || doc.UploadedAt >= dep.IdentityUpdatedAt.Value;
                })
                .GroupBy(doc => doc.DependentId)
                .ToDictionary(g => g.Key, g => g.Select(d => d.DocType.ToString()).ToList());

            var items = new List<DependentListItemResponse>(pagedDependents.Count);
            foreach (var d in pagedDependents)
            {
                var uploaded = docsByDependentId.TryGetValue(d.Id, out var u) ? u : new List<string>();
                var requiredDocs = await GetRequiredDocumentsAsync(d.CurrentGroup);
                var missingDocs = await GetMissingDocumentsAsync(d.CurrentGroup, d.IsProfileComplete, uploaded);

                var item = MapToListItem(d);
                item.RequiredDocuments = requiredDocs;
                item.MissingDocuments = missingDocs;
                items.Add(item);
            }

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

            // Loại bỏ các bản ghi trùng lặp: nhóm theo DocType và chỉ giữ lại bản ghi mới nhất
            var distinctDocs = documents
                .GroupBy(doc => doc.DocType)
                .Select(g => g.OrderByDescending(d => d.UploadedAt).First())
                .ToList();

            // 4. Map → response
            var docDtos = distinctDocs.Select(doc => new DependentDocumentDto
            {
                DocId = doc.Id,
                DocType = doc.DocType.ToString(),
                FileUrl = doc.FileUrl,
                FileMimeType = doc.FileMimeType,
                IsReadable = doc.IsReadable,
                UploadedAt = doc.UploadedAt,
                IsCurrent = !dependent.IdentityUpdatedAt.HasValue || doc.UploadedAt >= dependent.IdentityUpdatedAt.Value
            }).ToList();

            var currentValidDocs = distinctDocs
                .Where(doc => !dependent.IdentityUpdatedAt.HasValue || doc.UploadedAt >= dependent.IdentityUpdatedAt.Value)
                .Select(doc => doc.DocType.ToString())
                .ToList();

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
                MissingDocuments = await GetMissingDocumentsAsync(
                    dependent.CurrentGroup,
                    dependent.IsProfileComplete,
                    currentValidDocs),
                Documents = docDtos
            };
        }

        // ─────────────────────────────────────────────────────────────────────────
        // PUT /api/v1/dependents/{id} — Cập nhật thông tin hồ sơ NPT
        // ─────────────────────────────────────────────────────────────────────────
        public async Task<DependentResponse> UpdateDependentAsync(
            Guid taxpayerId, Guid dependentId, UpdateDependentRequest request)
        {
            var dependentRepo = _unitOfWork.Repository<Dependent>();

            // ── 1. Tìm NPT theo id ───────────────────────────────────────────────
            var dependent = await dependentRepo.GetByIdAsync(dependentId);

            if (dependent == null || dependent.IsDeleted || dependent.Status == DependentStatus.INACTIVE)
                throw new NotFoundException($"Không tìm thấy người phụ thuộc với Id '{dependentId}'.");

            // ── 2. Kiểm tra quyền sở hữu ─────────────────────────────────────────
            if (dependent.TaxpayerId != taxpayerId)
                throw new ForbiddenException(
                    "Bạn không có quyền cập nhật thông tin người phụ thuộc này.");

            // ── 3. Chặn đổi quan hệ và nhóm qua API này ─────────────────────────
            if (!string.IsNullOrWhiteSpace(request.Relationship))
            {
                if (!Enum.TryParse<DependentRelationship>(request.Relationship, true, out var parsedRel) ||
                    parsedRel != dependent.Relationship)
                {
                    throw new BadRequestException(
                        ErrorCodes.Dependent.UseChangeGroupApi,
                        "Đổi nhóm/quan hệ dùng chức năng Đổi nhóm điều kiện."
                    );
                }
            }

            if (!string.IsNullOrWhiteSpace(request.CurrentGroup))
            {
                if (!Enum.TryParse<DependentGroup>(request.CurrentGroup, true, out var parsedGroup) ||
                    parsedGroup != dependent.CurrentGroup)
                {
                    throw new BadRequestException(
                        ErrorCodes.Dependent.UseChangeGroupApi,
                        "Đổi nhóm/quan hệ dùng chức năng Đổi nhóm điều kiện."
                    );
                }
            }

            // ── 4. Xác định giá trị mới (nếu không gửi thì giữ nguyên) ──────────
            var newFullName = !string.IsNullOrWhiteSpace(request.FullName)
                ? request.FullName.Trim()
                : dependent.FullName;

            var newBirthDate = request.BirthDate.HasValue
                ? DateTime.SpecifyKind(request.BirthDate.Value, DateTimeKind.Utc)
                : dependent.BirthDate;

            var newCitizenId = request.CitizenId != null
                ? (string.IsNullOrWhiteSpace(request.CitizenId) ? null : request.CitizenId.Trim())
                : dependent.CitizenId;

            var newBirthCertNumber = request.BirthCertNumber != null
                ? (string.IsNullOrWhiteSpace(request.BirthCertNumber) ? null : request.BirthCertNumber.Trim())
                : dependent.BirthCertNumber;

            var newTaxIdNumber = request.TaxIdNumber != null
                ? (string.IsNullOrWhiteSpace(request.TaxIdNumber) ? null : request.TaxIdNumber.Trim())
                : dependent.TaxIdNumber;

            var newEffectiveFromMonth = !string.IsNullOrWhiteSpace(request.EffectiveFromMonth)
                ? request.EffectiveFromMonth.Trim()
                : dependent.EffectiveFromMonth;

            var newEffectiveToMonth = !string.IsNullOrWhiteSpace(request.EffectiveToMonth)
                ? request.EffectiveToMonth.Trim()
                : dependent.EffectiveToMonth;

            var newNote = request.Note != null
                ? request.Note
                : dependent.Note;

            // ── 5. So sánh thay đổi thông tin nhân thân ─────────────────────────
            bool isNameChanged = !string.Equals(newFullName.Trim(), dependent.FullName.Trim(), StringComparison.OrdinalIgnoreCase);
            bool isBirthDateChanged = newBirthDate.Date != dependent.BirthDate.Date;
            bool isCitizenIdChanged = !string.Equals(newCitizenId?.Trim(), dependent.CitizenId?.Trim(), StringComparison.Ordinal);
            bool isBirthCertChanged = !string.Equals(newBirthCertNumber?.Trim(), dependent.BirthCertNumber?.Trim(), StringComparison.Ordinal);

            bool isIdentityChanged = isNameChanged || isBirthDateChanged || isCitizenIdChanged || isBirthCertChanged;

            // ── 6. Validate định danh: phải có CitizenId HOẶC BirthCertNumber ──
            var hasCitizenId = !string.IsNullOrWhiteSpace(newCitizenId);
            var hasBirthCert = !string.IsNullOrWhiteSpace(newBirthCertNumber);

            if (!hasCitizenId && !hasBirthCert)
            {
                throw new BadRequestException(
                    ErrorCodes.Dependent.IdentifierRequired,
                    "Phải cung cấp ít nhất Số CCCD hoặc Số Giấy khai sinh của người phụ thuộc."
                );
            }

            // ── 7. Validate độ tuổi: Chỉ kiểm tra nếu ngày sinh bị thay đổi ──────
            // Con thuộc nhóm CHILD_UNDER_18 khi đủ 18 tuổi thì người nộp thuế vẫn được
            // sửa kỳ hiệu lực hoặc ghi chú mà không bị chặn bởi tuổi hiện tại.
            if (isBirthDateChanged)
            {
                if (dependent.CurrentGroup == DependentGroup.CHILD_UNDER_18)
                {
                    var fromDate = DateTime.TryParseExact(newEffectiveFromMonth + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fDate) ? fDate : DateTime.UtcNow;
                    var ageAtStart = fromDate.Year - newBirthDate.Year;
                    if (newBirthDate.Date > fromDate.AddYears(-ageAtStart)) ageAtStart--;

                    if (ageAtStart >= 18)
                    {
                        throw new BadRequestException(
                            ErrorCodes.Dependent.GroupAgeMismatch,
                            $"Người phụ thuộc đã từ đủ 18 tuổi tại thời điểm bắt đầu giảm trừ ({newEffectiveFromMonth}). " +
                            "Không thể áp dụng nhóm 'Con dưới 18 tuổi'."
                        );
                    }
                }
                else
                {
                    ValidateGroupMatchesAge(newBirthDate, dependent.CurrentGroup);
                }
            }

            // ── 8. Validate thời gian: From <= To và From >= BirthDate ──────────
            ValidateEffectivePeriod(newBirthDate, newEffectiveFromMonth, newEffectiveToMonth);

            // ── 9. Chặn theo hồ sơ quyết toán đã chốt (TaxSettlementDossier LOCKED) ─
            var affectedYears = PeriodHelper.AffectedYears(
                dependent.EffectiveFromMonth, dependent.EffectiveToMonth,
                newEffectiveFromMonth, newEffectiveToMonth);

            var settlementRepo = _unitOfWork.Repository<TaxSettlementDossier>();
            if (settlementRepo != null && affectedYears.Count > 0)
            {
                var lockedDossiers = (await settlementRepo.FindAsync(d =>
                    d.TaxpayerId == taxpayerId &&
                    d.Status == "LOCKED")).ToList();

                var lockedYears = lockedDossiers
                    .Where(d => affectedYears.Contains(d.TaxYear))
                    .Select(d => d.TaxYear)
                    .OrderBy(y => y)
                    .ToList();

                if (lockedYears.Count > 0)
                {
                    throw new ConflictException(
                        ErrorCodes.Dependent.SettlementLockedPeriod,
                        $"Năm {string.Join(", ", lockedYears)} đã chốt hồ sơ quyết toán, không thể thay đổi thời gian hiệu lực trong năm này."
                    );
                }
            }

            // ── 10. Kiểm tra trùng lặp thời gian với các NPT khác ───────────────
            if (hasCitizenId)
            {
                var cleanCitizenId = newCitizenId!;
                var existingByCitizenId = await dependentRepo.FindAsync(d =>
                    d.CitizenId == cleanCitizenId && d.Id != dependentId && !d.IsDeleted);

                var conflict = existingByCitizenId.FirstOrDefault(d =>
                    string.Compare(d.EffectiveFromMonth, newEffectiveToMonth, StringComparison.Ordinal) <= 0 &&
                    string.Compare(d.EffectiveToMonth, newEffectiveFromMonth, StringComparison.Ordinal) >= 0
                );

                if (conflict != null)
                {
                    throw new BadRequestException(
                        ErrorCodes.Dependent.DependentPeriodOverlap,
                        $"Người phụ thuộc có số CCCD '{MaskIdentifier(cleanCitizenId)}' đã được đăng ký " +
                        $"trong khoảng thời gian từ {conflict.EffectiveFromMonth} đến {conflict.EffectiveToMonth}. " +
                        "Một người phụ thuộc không thể được đăng ký trùng kỳ tính giảm trừ bởi nhiều người nộp thuế."
                    );
                }
            }

            if (hasBirthCert)
            {
                var cleanBirthCert = newBirthCertNumber!;
                var existingByBirthCert = await dependentRepo.FindAsync(d =>
                    d.BirthCertNumber == cleanBirthCert && d.Id != dependentId && !d.IsDeleted);

                var conflict = existingByBirthCert.FirstOrDefault(d =>
                    string.Compare(d.EffectiveFromMonth, newEffectiveToMonth, StringComparison.Ordinal) <= 0 &&
                    string.Compare(d.EffectiveToMonth, newEffectiveFromMonth, StringComparison.Ordinal) >= 0
                );

                if (conflict != null)
                {
                    throw new BadRequestException(
                        ErrorCodes.Dependent.DependentPeriodOverlap,
                        $"Người phụ thuộc có số Giấy khai sinh '{MaskIdentifier(cleanBirthCert)}' đã được đăng ký " +
                        $"trong khoảng thời gian từ {conflict.EffectiveFromMonth} đến {conflict.EffectiveToMonth}. " +
                        "Một người phụ thuộc không thể được đăng ký trùng kỳ tính giảm trừ bởi nhiều người nộp thuế."
                    );
                }
            }

            // ── 11. Xử lý quy tắc sửa theo trạng thái ───────────────────────────
            bool statusChanged = false;
            bool documentsNeedReupload = false;

            var docRepo = _unitOfWork.Repository<DependentDocument>();
            var existingDocs = docRepo != null
                ? ((await docRepo.FindAsync(d => d.DependentId == dependentId))?.ToList() ?? new List<DependentDocument>())
                : new List<DependentDocument>();
            bool hasExistingDocs = existingDocs.Any();

            if (dependent.Status == DependentStatus.ACTIVE)
            {
                if (isIdentityChanged)
                {
                    if (!request.ConfirmIdentityChange)
                    {
                        throw new ConflictException(
                            ErrorCodes.Dependent.IdentityChangeRequiresReverify,
                            "Thay đổi thông tin nhân thân cần xác nhận để nộp lại giấy tờ minh chứng."
                        );
                    }

                    dependent.Status = DependentStatus.PENDING_DOCUMENTS;
                    dependent.IsProfileComplete = false;
                    dependent.IdentityUpdatedAt = DateTime.UtcNow;
                    statusChanged = true;
                    documentsNeedReupload = true;

                    var notification = new SystemNotification
                    {
                        NotificationId = Guid.NewGuid(),
                        UserId = taxpayerId,
                        Title = $"Hồ sơ người phụ thuộc '{newFullName}' cần nộp lại minh chứng",
                        Message = $"Hồ sơ NPT '{newFullName}' cần nộp lại minh chứng do thay đổi thông tin nhân thân.",
                        NotificationType = "DEPENDENT_IDENTITY_CHANGED",
                        IsRead = false,
                        TargetActionUrl = $"/dependents/{dependentId}/documents",
                        CreatedAt = DateTime.UtcNow
                    };
                    var notifRepo = _unitOfWork.Repository<SystemNotification>();
                    if (notifRepo != null)
                    {
                        await notifRepo.AddAsync(notification);
                    }
                }
            }
            else if (dependent.Status == DependentStatus.PENDING_DOCUMENTS)
            {
                if (isIdentityChanged && hasExistingDocs)
                {
                    dependent.IdentityUpdatedAt = DateTime.UtcNow;
                    dependent.IsProfileComplete = false;
                    documentsNeedReupload = true;
                }
            }

            // ── 12. Cập nhật entity ──────────────────────────────────────────────
            dependent.FullName = newFullName;
            dependent.BirthDate = newBirthDate;
            dependent.CitizenId = newCitizenId;
            dependent.BirthCertNumber = newBirthCertNumber;
            dependent.TaxIdNumber = newTaxIdNumber;
            dependent.EffectiveFromMonth = newEffectiveFromMonth;
            dependent.EffectiveToMonth = newEffectiveToMonth;
            dependent.Note = newNote;
            dependent.UpdatedAt = DateTimeOffset.UtcNow;

            dependentRepo.Update(dependent);
            await _unitOfWork.SaveChangesAsync();

            // ── 13. Trả về response kèm checklist giấy tờ ───────────────────────
            var validUploadedDocs = existingDocs
                .Where(d => !dependent.IdentityUpdatedAt.HasValue || d.UploadedAt >= dependent.IdentityUpdatedAt.Value)
                .Select(d => d.DocType)
                .ToList();

            var requiredDocs = await GetRequiredDocumentsAsync(dependent.CurrentGroup);
            var missingDocs = await GetMissingDocumentsAsync(dependent.CurrentGroup, dependent.IsProfileComplete, validUploadedDocs);

            return new DependentResponse
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
                Note = dependent.Note,
                Status = dependent.Status.ToString(),
                CreatedAt = dependent.CreatedAt,
                UpdatedAt = dependent.UpdatedAt,
                RequiredDocuments = requiredDocs,
                MissingDocuments = missingDocs,
                StatusChanged = statusChanged,
                DocumentsNeedReupload = documentsNeedReupload
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
                    $"Người phụ thuộc đã ở nhóm '{GetGroupDisplayName(newGroup)}'. Vui lòng chọn nhóm khác để chuyển."
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
                    $"Không thể chuyển người phụ thuộc từ nhóm '{GetGroupDisplayName(dependent.CurrentGroup)}' " +
                    $"xuống nhóm thấp hơn '{GetGroupDisplayName(newGroup)}'. " +
                    "Nhóm mới phải tương đương hoặc cao hơn nhóm hiện tại."
                );
            }

            // ── 5.5. Validate độ tuổi phù hợp với NewGroup ─────────────────────
            ValidateGroupMatchesAge(dependent.BirthDate, newGroup);

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
                UploadedAt = doc.UploadedAt,
                IsCurrent = !dependent.IdentityUpdatedAt.HasValue || doc.UploadedAt >= dependent.IdentityUpdatedAt.Value
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
                MissingDocuments = await GetMissingDocumentsAsync(
                    newGroup,
                    false,
                    documents.Select(doc => doc.DocType.ToString()).ToList()),
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
