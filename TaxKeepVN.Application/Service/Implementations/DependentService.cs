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
        private readonly IDependentRuleService _dependentRuleService;
        private readonly IDependentCatalogService _catalogService;

        public DependentService(
            IUnitOfWork unitOfWork,
            IMemoryCache cache,
            IConfiguration configuration,
            IRedisCacheService? redisCache = null,
            IDependentRuleService? dependentRuleService = null,
            IDependentCatalogService? catalogService = null)
        {
            _unitOfWork = unitOfWork;
            _cache = cache;
            _configuration = configuration;
            _redisCache = redisCache;
            _dependentRuleService = dependentRuleService ?? new DependentRuleService(_unitOfWork, _cache, _configuration, _redisCache);
            _catalogService = catalogService ?? new DependentCatalogService(_unitOfWork, _cache, _redisCache);
        }

        private async Task<List<DependentRelationshipCatalog>> GetActiveRelationshipsAsync()
        {
            return await _catalogService.GetActiveRelationshipsAsync();
        }

        private async Task<List<DependentGroupCatalog>> GetActiveGroupsAsync()
        {
            return await _catalogService.GetActiveGroupsAsync();
        }

        public async Task<DependentResponse> CreateDependentAsync(Guid taxpayerId, CreateDependentRequest request)
        {
            // ── 1. Validate Relationship catalog ─────────────────────────────────
            var activeRels = await GetActiveRelationshipsAsync();
            var matchedRel = activeRels.FirstOrDefault(r => string.Equals(r.Code, request.Relationship?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (matchedRel == null)
            {
                var validRels = activeRels.Count > 0 ? string.Join(", ", activeRels.Select(r => r.Code)) : "CHILD, SPOUSE, PARENT, OTHER_DEPENDENT";
                throw new BadRequestException(
                    "INVALID_RELATIONSHIP",
                    $"Nhóm quan hệ '{request.Relationship}' không hợp lệ. " +
                    $"Các giá trị hợp lệ: {validRels}."
                );
            }
            var relationship = matchedRel.Code;

            // ── 2. Validate và parse CurrentGroup catalog ────────────────────────
            var activeGroups = await GetActiveGroupsAsync();
            var matchedGroup = activeGroups.FirstOrDefault(g => string.Equals(g.Code, request.CurrentGroup?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (matchedGroup == null)
            {
                throw new BadRequestException(
                    "INVALID_CURRENT_GROUP",
                    $"Điều kiện đăng ký '{request.CurrentGroup}' không hợp lệ. " +
                    "Vui lòng chọn đúng điều kiện đăng ký người phụ thuộc."
                );
            }
            var currentGroup = matchedGroup.Code;

            // ── 3. Validate CurrentGroup phải khớp với Relationship ─────────────
            ValidateGroupMatchesRelationship(relationship, matchedGroup, activeGroups);
            var gender = NormalizeGender(request.Gender);
            if (string.Equals(relationship, "PARENT", StringComparison.OrdinalIgnoreCase))
            {
                await ValidateParentIncomeCommitmentAsync(request, gender);
            }

            // ── 3.5. Validate độ tuổi phù hợp với CurrentGroup nếu có BirthDate ─
            if (request.BirthDate.HasValue)
            {
                ValidateGroupMatchesAge(request.BirthDate.Value, matchedGroup);
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
                Gender = gender,
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
                Gender = newDependent.Gender,
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
        private static void ValidateGroupMatchesRelationship(
            string relationship,
            DependentGroupCatalog currentGroup,
            List<DependentGroupCatalog> allGroups)
        {
            if (!string.Equals(currentGroup.RelationshipCode, relationship, StringComparison.OrdinalIgnoreCase))
            {
                var validForRel = allGroups
                    .Where(g => string.Equals(g.RelationshipCode, relationship, StringComparison.OrdinalIgnoreCase))
                    .Select(g => g.Code)
                    .ToList();
                var validList = validForRel.Count > 0
                    ? string.Join(", ", validForRel)
                    : "(chưa cấu hình)";

                throw new BadRequestException(
                    "GROUP_RELATIONSHIP_MISMATCH",
                    $"Điều kiện đăng ký '{currentGroup.Code}' không thuộc nhóm quan hệ '{relationship}'. " +
                    $"Các điều kiện hợp lệ cho nhóm này: {validList}."
                );
            }
        }

        // ── Helper: lấy tên hiển thị tiếng Việt thân thiện cho DependentGroup ────
        public static string GetGroupDisplayName(string groupCode, DependentGroupCatalog? groupCatalog = null)
        {
            if (groupCatalog != null && !string.IsNullOrWhiteSpace(groupCatalog.Name))
                return groupCatalog.Name;

            return groupCode;
        }

        // ── Helper: validate độ tuổi phải phù hợp với DependentGroupCatalog ─────
        private static void ValidateGroupMatchesAge(DateTime birthDate, DependentGroupCatalog group)
        {
            if (birthDate == default || birthDate == DateTime.MinValue)
                return;

            var today = DateTime.UtcNow;
            var age = today.Year - birthDate.Year;
            if (birthDate.Date > today.AddYears(-age))
            {
                age--;
            }

            if (group.MaxAge.HasValue && age >= group.MaxAge.Value)
            {
                throw new BadRequestException(
                    ErrorCodes.Dependent.GroupAgeMismatch,
                    $"Người phụ thuộc hiện tại {age} tuổi (đã từ đủ {group.MaxAge.Value} tuổi trở lên). " +
                    $"Không thể chuyển hoặc đăng ký vào nhóm '{group.Name}'."
                );
            }

            if (group.MinAge.HasValue && age < group.MinAge.Value)
            {
                throw new BadRequestException(
                    ErrorCodes.Dependent.GroupAgeMismatch,
                    $"Người phụ thuộc hiện tại {age} tuổi (chưa đủ {group.MinAge.Value} tuổi). " +
                    $"Không thể chuyển sang nhóm '{group.Name}'. " +
                    $"Nhóm này chỉ áp dụng cho con từ đủ {group.MinAge.Value} tuổi trở lên."
                );
            }
        }

        private static string NormalizeGender(string? gender)
        {
            var normalizedGender = gender?.Trim().ToUpperInvariant();
            if (normalizedGender is not ("MALE" or "FEMALE" or "OTHER"))
            {
                throw new BadRequestException(
                    "INVALID_GENDER",
                    "Giới tính người phụ thuộc chỉ được là MALE, FEMALE hoặc OTHER."
                );
            }

            return normalizedGender;
        }

        private async Task ValidateParentIncomeCommitmentAsync(CreateDependentRequest request, string gender)
        {
            if (!request.BirthDate.HasValue)
            {
                throw new BadRequestException("BIRTH_DATE_REQUIRED", "Ngày sinh cha/mẹ không được để trống.");
            }

            var birthDate = request.BirthDate.Value.Date;
            var today = DateTime.UtcNow.Date;
            if (birthDate > today)
            {
                throw new BadRequestException("INVALID_BIRTH_DATE", "Ngày sinh không được nằm trong tương lai.");
            }

            var maxAgeConfigKey = gender switch
            {
                "MALE" => "DEPENDENT_PARENT_MALE_MAX_AGE",
                "FEMALE" => "DEPENDENT_PARENT_FEMALE_MAX_AGE",
                _ => throw new BadRequestException(
                    "AGE_NOT_MATCH",
                    "Không thể áp dụng quy tắc tuổi cha/mẹ với giới tính OTHER; vui lòng kiểm tra lại giới tính.")
            };

            var maxAge = await GetRequiredIntegerConfigAsync(maxAgeConfigKey);
            if (maxAge <= 0)
            {
                throw new ServiceUnavailableException(
                    "DEPENDENT_CONFIG_INVALID",
                    $"Cấu hình '{maxAgeConfigKey}' phải là số tuổi lớn hơn 0.");
            }

            var age = CalculateAge(birthDate, today);
            if (age >= maxAge)
            {
                return;
            }

            var incomeLimit = await GetRequiredDecimalConfigAsync("DEPENDENT_PARENT_MONTHLY_INCOME_LIMIT_VND");
            if (incomeLimit <= 0)
            {
                throw new ServiceUnavailableException(
                    "DEPENDENT_CONFIG_INVALID",
                    "Cấu hình 'DEPENDENT_PARENT_MONTHLY_INCOME_LIMIT_VND' phải lớn hơn 0.");
            }

            if (!request.ConfirmIncomeBelowThreshold)
            {
                throw new BadRequestException(
                    "INCOME_COMMITMENT_REQUIRED",
                    $"Cha/mẹ dưới {maxAge} tuổi cần xác nhận cam kết thu nhập hàng tháng không vượt quá {incomeLimit:N0} VND.");
            }
        }

        private async Task<int> GetRequiredIntegerConfigAsync(string configKey)
        {
            var value = await GetRequiredSystemConfigValueAsync(configKey);
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            throw new ServiceUnavailableException(
                "DEPENDENT_CONFIG_INVALID",
                $"Cấu hình '{configKey}' phải có giá trị là số nguyên.");
        }

        private async Task<decimal> GetRequiredDecimalConfigAsync(string configKey)
        {
            var value = await GetRequiredSystemConfigValueAsync(configKey);
            if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            throw new ServiceUnavailableException(
                "DEPENDENT_CONFIG_INVALID",
                $"Cấu hình '{configKey}' phải có giá trị số hợp lệ.");
        }

        private async Task<string> GetRequiredSystemConfigValueAsync(string configKey)
        {
            var configRepo = _unitOfWork.Repository<SystemConfig>();
            var config = configRepo == null
                ? null
                : (await configRepo.FindAsync(c => c.IsActive && c.ConfigKey == configKey))
                    .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(config?.ConfigValue))
            {
                throw new ServiceUnavailableException(
                    "DEPENDENT_CONFIG_NOT_CONFIGURED",
                    $"Thiếu cấu hình active '{configKey}' trong system_configs; vui lòng liên hệ quản trị viên.");
            }

            return config.ConfigValue.Trim();
        }

        private static int CalculateAge(DateTime birthDate, DateTime asOfDate)
        {
            var age = asOfDate.Year - birthDate.Year;
            if (birthDate > asOfDate.AddYears(-age))
            {
                age--;
            }

            return age;
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
        private async Task<string[]> GetRequiredDocumentsAsync(string groupString)
        {
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
            string groupString,
            bool isProfileComplete,
            List<string> uploadedDocTypes)
        {
            if (isProfileComplete)
                return Array.Empty<string>();

            var (isComplete, missing) = await _dependentRuleService.ComputeMissingDocumentsAsync(groupString, uploadedDocTypes);
            return missing.ToArray();
        }

        // ── Helper: map Dependent entity → DependentListItemResponse ─────────────
        private static DependentListItemResponse MapToListItem(Dependent d) => new()
        {
            DependentId = d.Id,
            TaxpayerId = d.TaxpayerId,
            FullName = d.FullName,
            Gender = d.Gender,
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
            if (!string.IsNullOrWhiteSpace(query.Relationship))
            {
                var relFilter = query.Relationship.Trim();
                all = all.Where(d => string.Equals(d.Relationship, relFilter, StringComparison.OrdinalIgnoreCase)).ToList();
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
                Gender = dependent.Gender,
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
                if (!string.Equals(request.Relationship.Trim(), dependent.Relationship, StringComparison.OrdinalIgnoreCase))
                {
                    throw new BadRequestException(
                        ErrorCodes.Dependent.UseChangeGroupApi,
                        "Đổi nhóm/quan hệ dùng chức năng Đổi nhóm điều kiện."
                    );
                }
            }

            if (!string.IsNullOrWhiteSpace(request.CurrentGroup))
            {
                if (!string.Equals(request.CurrentGroup.Trim(), dependent.CurrentGroup, StringComparison.OrdinalIgnoreCase))
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

            var newGender = request.Gender == null
                ? dependent.Gender
                : NormalizeGender(request.Gender);

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
            bool isGenderChanged = !string.Equals(newGender, dependent.Gender, StringComparison.OrdinalIgnoreCase);
            bool isBirthDateChanged = newBirthDate.Date != dependent.BirthDate.Date;
            bool isCitizenIdChanged = !string.Equals(newCitizenId?.Trim(), dependent.CitizenId?.Trim(), StringComparison.Ordinal);
            bool isBirthCertChanged = !string.Equals(newBirthCertNumber?.Trim(), dependent.BirthCertNumber?.Trim(), StringComparison.Ordinal);

            bool isIdentityChanged = isNameChanged || isGenderChanged || isBirthDateChanged || isCitizenIdChanged || isBirthCertChanged;

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
                var depGroup = await _catalogService.GetGroupAsync(dependent.CurrentGroup, includeInactive: true);
                if (depGroup != null && depGroup.MaxAge.HasValue)
                {
                    var fromDate = DateTime.TryParseExact(newEffectiveFromMonth + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fDate) ? fDate : DateTime.UtcNow;
                    var ageAtStart = fromDate.Year - newBirthDate.Year;
                    if (newBirthDate.Date > fromDate.AddYears(-ageAtStart)) ageAtStart--;

                    if (ageAtStart >= depGroup.MaxAge.Value)
                    {
                        throw new BadRequestException(
                            ErrorCodes.Dependent.GroupAgeMismatch,
                            $"Người phụ thuộc đã từ đủ {depGroup.MaxAge.Value} tuổi tại thời điểm bắt đầu giảm trừ ({newEffectiveFromMonth}). " +
                            $"Không thể áp dụng nhóm '{depGroup.Name}'."
                        );
                    }
                }
                else if (depGroup != null)
                {
                    ValidateGroupMatchesAge(newBirthDate, depGroup);
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
            dependent.Gender = newGender;
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
                Gender = dependent.Gender,
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

            // ── 3. Parse và validate NewGroup catalog ───────────────────────────
            var activeGroups = await GetActiveGroupsAsync();
            var matchedNewGroup = activeGroups.FirstOrDefault(g => string.Equals(g.Code, request.NewGroup?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (matchedNewGroup == null)
            {
                var validGroups = activeGroups.Count > 0 ? string.Join(", ", activeGroups.Select(g => g.Code)) : "";
                throw new BadRequestException(
                    "INVALID_GROUP",
                    $"Nhóm điều kiện '{request.NewGroup}' không hợp lệ. " +
                    $"Các giá trị hợp lệ: {validGroups}."
                );
            }

            // ── 4. Không cho phép chuyển cùng nhóm ──────────────────────────────
            if (string.Equals(matchedNewGroup.Code, dependent.CurrentGroup, StringComparison.OrdinalIgnoreCase))
            {
                throw new BadRequestException(
                    "SAME_GROUP",
                    $"Người phụ thuộc đã ở nhóm '{GetGroupDisplayName(dependent.CurrentGroup, matchedNewGroup)}'. Vui lòng chọn nhóm khác để chuyển."
                );
            }

            // ── 4.5. Validate NewGroup phải thuộc cùng Relationship ────────────
            // Phải kiểm tra trước: nếu chọn sai Relationship thì báo lỗi GROUP_RELATIONSHIP_MISMATCH
            // rõ ràng hơn là bị lọt xuống check downgrade (ra lỗi sai ngữ nghĩa).
            ValidateGroupMatchesRelationship(dependent.Relationship, matchedNewGroup, activeGroups);

            // ── 5. Không cho phép chuyển xuống nhóm thấp hơn (downgrade) ─────────
            // Ví dụ: CHILD_OVER_18_STUDYING → CHILD_UNDER_18 là không hợp lệ vì
            // độ tuổi chỉ tăng theo thời gian, không thể trẻ lại.
            var currentGroupCatalog = await _catalogService.GetGroupAsync(dependent.CurrentGroup, includeInactive: true);
            if (currentGroupCatalog == null)
            {
                throw new BadRequestException(
                    "INVALID_GROUP",
                    $"Nhóm hiện tại '{dependent.CurrentGroup}' không tồn tại trong hệ thống."
                );
            }

            if (matchedNewGroup.Level < currentGroupCatalog.Level)
            {
                throw new BadRequestException(
                    "GROUP_DOWNGRADE_NOT_ALLOWED",
                    $"Không thể chuyển người phụ thuộc từ nhóm '{currentGroupCatalog.Name}' " +
                    $"xuống nhóm thấp hơn '{matchedNewGroup.Name}'. " +
                    "Nhóm mới phải tương đương hoặc cao hơn nhóm hiện tại."
                );
            }

            // ── 5.5. Validate độ tuổi phù hợp với NewGroup ─────────────────────
            ValidateGroupMatchesAge(dependent.BirthDate, matchedNewGroup);

            // ── 6. Lưu nhóm cũ để đưa vào response ─────────────────────────────
            var previousGroup = dependent.CurrentGroup;

            // ── 7. Cập nhật entity ───────────────────────────────────────────────
            dependent.CurrentGroup = matchedNewGroup.Code;
            dependent.IsProfileComplete = false;
            dependent.Status = DependentStatus.PENDING_DOCUMENTS;
            dependent.Note = request.Note ?? dependent.Note; // null giữ nguyên ghi chú cũ
            dependent.UpdatedAt = DateTimeOffset.UtcNow;

            dependentRepo.Update(dependent);

            // ── 8. Tạo SystemNotification thông báo cho user ─────────────────────
            var requiredDocs = await GetRequiredDocumentsAsync(matchedNewGroup.Code);
            var docList = string.Join(", ", requiredDocs);

            var notification = new SystemNotification
            {
                NotificationId = Guid.NewGuid(),
                UserId = taxpayerId,
                Title = $"Hồ sơ người phụ thuộc '{dependent.FullName}' cần cập nhật giấy tờ",
                Message = $"Người phụ thuộc '{dependent.FullName}' vừa được chuyển từ nhóm " +
                          $"'{previousGroup}' sang nhóm '{matchedNewGroup.Code}'. " +
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
                Gender = dependent.Gender,
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
                    matchedNewGroup.Code,
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
