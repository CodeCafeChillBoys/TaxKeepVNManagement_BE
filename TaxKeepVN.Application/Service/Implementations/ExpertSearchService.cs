using Microsoft.Extensions.Logging;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.DTOs.Experts;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Helpers;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    /// <summary>
    /// Service thực thi Tìm kiếm, lọc và xem thông tin chuyên gia theo Đặc tả 2
    /// </summary>
    public class ExpertSearchService : IExpertSearchService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ExpertSearchService> _logger;

        public ExpertSearchService(IUnitOfWork unitOfWork, ILogger<ExpertSearchService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        // ── 1. TÌM KIẾM VÀ LỌC DANH SÁCH CHUYÊN GIA ─────────────────────────────────────
        public async Task<ExpertSearchResultDto> SearchExpertsAsync(ExpertSearchQueryParameters query)
        {
            var profileRepo = _unitOfWork.Repository<ExpertProfile>();
            var allProfiles = (await profileRepo.GetAllAsync()).ToList();

            // BR-01: Chỉ những chuyên gia có trạng thái "Đã duyệt" và "Đang mở lịch nhận tư vấn" (IsActive = true)
            var activeProfiles = allProfiles.Where(p => p.IsActive).ToList();
            if (!activeProfiles.Any())
            {
                return new ExpertSearchResultDto
                {
                    Items = new List<ExpertListItemResponse>(),
                    Pagination = new PaginationMeta { Page = query.Page, PageSize = query.Size, TotalItems = 0, TotalPages = 0 },
                    Suggestion = ExpertSearchHelper.BuildEmptySuggestions(query)
                };
            }
            // lấy hồ sơ trong profile ra tránh bị lặp
            var appIds = activeProfiles.Select(p => p.LatestApplicationId).Distinct().ToList();
            // lấy chuyên gia trong profile ra tránh bị lặp
            var userIds = activeProfiles.Select(p => p.UserId).Distinct().ToList();
            // lấy profileid ra hết tránh bị lặp
            var profileIds = activeProfiles.Select(p => p.Id).Distinct().ToList();
            
            // Tải dữ liệu liên quan để xử lý in-memory tối ưu
            // Kiểm tra hồ sơ đc có khớp với tất cả đã lấy ra trong profile hay ko
            var apps = (await _unitOfWork.Repository<ExpertApplication>().FindAsync(a => appIds.Contains(a.Id))).ToDictionary(a => a.Id);
            // kiểm tra xem chuyên gia trong bảng user user có status active với bên trong profile ko
            var users = (await _unitOfWork.Repository<User>().FindAsync(u => userIds.Contains(u.UserId) && u.Status == "active")).ToDictionary(u => u.UserId);

            // Chỉ giữ lại profile có user hợp lệ và đơn đăng ký Approved
            var validProfiles = activeProfiles.Where(p =>
                users.ContainsKey(p.UserId) &&
                apps.TryGetValue(p.LatestApplicationId, out var app) &&
                app.Status == ExpertApplicationStatus.Approved).ToList();

            if (!validProfiles.Any())
            {
                return new ExpertSearchResultDto
                {
                    Items = new List<ExpertListItemResponse>(),
                    Pagination = new PaginationMeta { Page = query.Page, PageSize = query.Size, TotalItems = 0, TotalPages = 0 },
                    Suggestion = ExpertSearchHelper.BuildEmptySuggestions(query)
                };
            }
            // lọc qua tất cả profileID và lấy lên tất cả các profileID hợp lệ
            validProfiles = validProfiles.Where(p => profileIds.Contains(p.Id)).ToList();
            // lấy lên những hồ sơ hợp lệ
            var validAppIds = validProfiles.Select(p => p.LatestApplicationId).Distinct().ToList();
            // lấy lên những profile hợp lệ
            var validProfileIds = validProfiles.Select(p => p.Id).Distinct().ToList();

            // Tải Chuyên môn
            var appSpecs = (await _unitOfWork.Repository<ExpertApplicationSpecialization>().FindAsync(s => validAppIds.Contains(s.ApplicationId))).ToList();
            var specIds = appSpecs.Select(s => s.SpecializationId).Distinct().ToList();
            var allSpecs = (await _unitOfWork.Repository<Specialization>().FindAsync(s => specIds.Contains(s.Id) && s.IsActive)).ToDictionary(s => s.Id, s => s.Name);

            // Tải Mức phí đề xuất
            var feeProposals = (await _unitOfWork.Repository<ExpertApplicationFeeProposal>().FindAsync(f => validAppIds.Contains(f.ApplicationId))).ToList();

            // Tải Chứng chỉ Verified
            var certs = (await _unitOfWork.Repository<ExpertApplicationCertificate>().FindAsync(c => validAppIds.Contains(c.ApplicationId) && c.VerificationStatus == CertificateVerificationStatus.Verified)).ToList();

            // Tải Slots khả dụng từ hôm nay trở đi
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var futureSlots = (await _unitOfWork.Repository<ExpertSlot>().FindAsync(s => validProfileIds.Contains(s.ExpertProfileId) && s.SlotDate >= today && !s.IsBooked && s.IsActive)).ToList();

            // ── BỘ LỌC 1: LỌC THEO TỪ KHÓA (Keyword) ─────────────────────────
            // lấy lên tất cả profile và  trong quá trình lấy có thẻ lấy search sort ....
            var filteredProfiles = validProfiles.AsEnumerable();
            // Check keyWord có null ko 
            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                // bỏ khoảng trắng và chuyễn chuỗi sang dạng chữ thường 
                var kw = query.Keyword.Trim().ToLowerInvariant();

                filteredProfiles = filteredProfiles.Where(p =>
                {
                    // lấy hồ sơ lên và lấy tên trong hồ sơ
                    apps.TryGetValue(p.LatestApplicationId, out var app);
                    // trong hồ sơ ko có tên lấy trong bảng user
                    var fullName = app?.FullName ?? users[p.UserId].FullName;
                    var jobTitle = p.JobTitle ?? string.Empty;
                    var company = p.CompanyName ?? string.Empty;
                    var bio = p.Bio ?? string.Empty;
                    // tiến thành kiểm tra chuỗi đó có ký từ đc truyền vào thay ko
                    return fullName.ToLowerInvariant().Contains(kw) ||
                           jobTitle.ToLowerInvariant().Contains(kw) ||
                           company.ToLowerInvariant().Contains(kw) ||
                           bio.ToLowerInvariant().Contains(kw);
                });
            }

            // ── BỘ LỌC 2: LỌC THEO LĨNH VỰC / CHỦ ĐỀ CHUYÊN MÔN ──────────────
            // kiểm tra chuyên môn có khác null và có tồn tại trong Db
            if (query.SpecializationIds != null && query.SpecializationIds.Any())
            {
                // lấy chuyên môn đc truyền ko bị trùng dữ liệu
                var targetSpecIds = query.SpecializationIds.ToHashSet();
                filteredProfiles = filteredProfiles.Where(p =>
                {
                    // lọc tất cả hồ cơ có chuyên môn và lấy lên
                    var expertSpecIds = appSpecs.Where(s => s.ApplicationId == p.LatestApplicationId).Select(s => s.SpecializationId);
                    // kiểm tra với ID đc truyền vào
                    return expertSpecIds.Any(id => targetSpecIds.Contains(id));
                });
            }

            // ── BỘ LỌC 3: LỌC THEO SỐ SAO RATING ─────────────────────────────
            // Kiểm tra rating có số ko và lớn hơn 0
            if (query.MinRating.HasValue && query.MinRating.Value > 0)
            {
                // lọc các ra rating theo yêu cầu
                filteredProfiles = filteredProfiles.Where(p => p.Rating >= query.MinRating.Value);
            }

            // ── BỘ LỌC 4: LỌC THEO KHOẢNG MỨC PHÍ TƯ VẤN ─────────────────────
            // Kiểm tra giá sàn và giá trần có dữ liệu ko
            if (query.MinFee.HasValue || query.MaxFee.HasValue)
            {
                filteredProfiles = filteredProfiles.Where(p =>
                {
                    // lọc hồ sơ có giá sàn đc truyền vào lấy lên hết 
                    var pFees = feeProposals.Where(f => f.ApplicationId == p.LatestApplicationId).Select(f => f.ProposedFee).ToList();
                    if (!pFees.Any()) return false; // nếu ko có trả false

                    var minPrice = pFees.Min();
                    var maxPrice = pFees.Max();

                    if (query.MinFee.HasValue && maxPrice < query.MinFee.Value) return false; // nếu max < min 
                    if (query.MaxFee.HasValue && minPrice > query.MaxFee.Value) return false; // nếu min > max

                    return true;
                });
            }

            // ── BỘ LỌC 5: LỌC THEO KHUNG THỜI GIAN RẢNH (TimeFilter) ────────
            // kiểm tra thời gian rảnh đc chuyền vào ko phải là all thì thực hiện lệnh bên dưới
            if (query.TimeFilter != ExpertTimeFilter.All)
            {
                
                var tomorrow = today.AddDays(1);
                // lấy xem ngày thứ bảy còn bao nhiu ngày
                var daysUntilSaturday = ((int)DayOfWeek.Saturday - (int)today.DayOfWeek + 7) % 7;
                var weekendSaturday = today.AddDays(daysUntilSaturday);
                // chủ nhật cộng 1
                var weekendSunday = weekendSaturday.AddDays(1);
                // sau 7 ngày thì công 7
                var sevenDaysLater = today.AddDays(7);

                filteredProfiles = filteredProfiles.Where(p =>
                {
                    // check slodt có profileID ko nếu có chuyển thành swtich 
                    var pSlots = futureSlots.Where(s => s.ExpertProfileId == p.Id);
                    return query.TimeFilter switch
                    {
                        ExpertTimeFilter.Today => pSlots.Any(s => s.SlotDate == today),
                        ExpertTimeFilter.Tomorrow => pSlots.Any(s => s.SlotDate == tomorrow),
                        ExpertTimeFilter.ThisWeekend => pSlots.Any(s => s.SlotDate == weekendSaturday || s.SlotDate == weekendSunday),
                        ExpertTimeFilter.Next7Days => pSlots.Any(s => s.SlotDate >= today && s.SlotDate <= sevenDaysLater),
                        _ => true
                    };
                });
            }

            // ── CHUYỂN ĐỔI SANG DTO & TÍNH TOÁN XẾP HẠNG (Ranking) ────────────
            //
            var itemsList = filteredProfiles.Select(p =>
            {
                apps.TryGetValue(p.LatestApplicationId, out var app);
                // lấy lên userID
                var user = users[p.UserId];
                // lấy lên chuyên môn
                var pSpecs = appSpecs.Where(s => s.ApplicationId == p.LatestApplicationId)
                                     .Select(s => allSpecs.TryGetValue(s.SpecializationId, out var name) ? name : string.Empty)
                                     .Where(n => !string.IsNullOrEmpty(n))
                                     .ToList();
                // lấy lên giá cả
                var pFees = feeProposals.Where(f => f.ApplicationId == p.LatestApplicationId).Select(f => f.ProposedFee).ToList();
                // lấy tồn tại lấy lên giá sàn
                var startingFee = pFees.Any() ? pFees.Min() : 0m;
                // đếm số bằng
                var verifiedCertCount = certs.Count(c => c.ApplicationId == p.LatestApplicationId);
                //lọc  qua profile và lấy lên sắp xếp theo SlotDate
                var pSlots = futureSlots.Where(s => s.ExpertProfileId == p.Id).OrderBy(s => s.SlotDate).ThenBy(s => s.StartTime).ToList();
                // kiểm tra xem chuyen gia có slot tròng vòng 3 ngày tới
                var hasSlotSoon = pSlots.Any(s => s.SlotDate <= today.AddDays(3));
                // Lấy SlotDate của phần tử đầu tiên trong pSlots.
                var earliestDate = pSlots.FirstOrDefault()?.SlotDate;

                // BR-02: Thuật toán Ranking mặc định
                var rankingScore = ExpertSearchHelper.CalculateRankingScore(p, hasSlotSoon, pSlots.Any());

                return new ExpertListItemResponse
                {
                    Id = p.Id,
                    UserId = p.UserId,
                    FullName = app?.FullName ?? user.FullName,
                    AvatarUrl = app?.AvatarUrl,
                    JobTitle = p.JobTitle,
                    CompanyName = p.CompanyName,
                    Bio = p.Bio,
                    YearsOfExperience = p.YearsOfExperience,
                    Rating = p.Rating,
                    TotalReviews = p.TotalReviews,
                    CompletedConsultationsCount = p.CompletedConsultationsCount,
                    StartingFee = startingFee,
                    SpecializationNames = pSpecs,
                    VerifiedCertificateCount = verifiedCertCount,
                    HasAvailableSlotSoon = hasSlotSoon,
                    EarliestAvailableDate = earliestDate,
                    RankingScore = rankingScore
                };
            }).ToList();

            // ── SẮP XẾP (Sorting) ────────────────────────────────────────────
            itemsList = query.SortBy switch
            {
                ExpertSortBy.RatingDesc => itemsList.OrderByDescending(i => i.Rating).ThenByDescending(i => i.TotalReviews).ToList(),
                ExpertSortBy.FeeAsc => itemsList.OrderBy(i => i.StartingFee).ThenByDescending(i => i.Rating).ToList(),
                ExpertSortBy.FeeDesc => itemsList.OrderByDescending(i => i.StartingFee).ThenByDescending(i => i.Rating).ToList(),
                ExpertSortBy.ExperienceDesc => itemsList.OrderByDescending(i => i.YearsOfExperience).ThenByDescending(i => i.Rating).ToList(),
                ExpertSortBy.CompletedSessionsDesc => itemsList.OrderByDescending(i => i.CompletedConsultationsCount).ThenByDescending(i => i.Rating).ToList(),
                _ => itemsList.OrderByDescending(i => i.RankingScore).ThenByDescending(i => i.Rating).ToList() // Recommended
            };

            var totalItems = itemsList.Count;
            var totalPages = (int)Math.Ceiling(totalItems / (double)query.Size);
            var pagedItems = itemsList.Skip((query.Page - 1) * query.Size).Take(query.Size).ToList();

            // Xử lý luồng ngoại lệ: không có kết quả phù hợp (Mục 6)
            ExpertSearchSuggestionDto? suggestion = null;
            if (totalItems == 0)
            {
                suggestion = ExpertSearchHelper.BuildEmptySuggestions(query);
            }

            return new ExpertSearchResultDto
            {
                Items = pagedItems,
                Pagination = new PaginationMeta
                {
                    Page = query.Page,
                    PageSize = query.Size,
                    TotalItems = totalItems,
                    TotalPages = totalPages
                },
                Suggestion = suggestion
            };
        }

        // ── 2. LẤY DANH SÁCH CHUYÊN GIA NỔI BẬT (Trang chủ / Đề xuất) ───────────────────
        public async Task<List<ExpertListItemResponse>> GetFeaturedExpertsAsync(int count = 6)
        {
            var defaultQuery = new ExpertSearchQueryParameters
            {
                Page = 1,
                Size = Math.Max(1, Math.Min(count, 20)),
                SortBy = ExpertSortBy.Recommended
            };

            var result = await SearchExpertsAsync(defaultQuery);
            return result.Items;
        }

        // ── 3. XEM CHI TIẾT HỒ SƠ CHUYÊN GIA (Public Detail - Masking BR-03) ────────────
        public async Task<ExpertDetailResponse> GetExpertDetailByIdAsync(Guid expertProfileId)
        {
            var profile = await _unitOfWork.Repository<ExpertProfile>().GetByIdAsync(expertProfileId);
            if (profile == null)
            {
                throw new NotFoundException($"Không tìm thấy hồ sơ chuyên gia với Id = {expertProfileId}.");
            }

            var app = await _unitOfWork.Repository<ExpertApplication>().GetByIdAsync(profile.LatestApplicationId);
            var user = await _unitOfWork.Repository<User>().GetByIdAsync(profile.UserId);

            // Tải chuyên môn
            var appSpecs = (await _unitOfWork.Repository<ExpertApplicationSpecialization>().FindAsync(s => s.ApplicationId == profile.LatestApplicationId)).ToList();
            var specIds = appSpecs.Select(s => s.SpecializationId).ToList();
            var specs = (await _unitOfWork.Repository<Specialization>().FindAsync(s => specIds.Contains(s.Id))).ToList();

            // Tải chứng chỉ đã xác minh & ÁP DỤNG DATA MASKING (BR-03: Che mờ thông tin cá nhân)
            var certs = (await _unitOfWork.Repository<ExpertApplicationCertificate>().FindAsync(c =>
                c.ApplicationId == profile.LatestApplicationId &&
                c.VerificationStatus == CertificateVerificationStatus.Verified)).ToList();

            var maskedCerts = certs.Select(c => new ExpertPublicCertificateDto
            {
                Id = c.Id,
                CertificateType = c.CertificateType,
                CertificateName = c.CertificateName,
                MaskedCertificateNumber = ExpertSearchHelper.MaskCertificateNumber(c.CertificateNumber),
                IssuingAuthority = c.IssuingAuthority,
                IssueDate = c.IssueDate,
                HasExpiry = c.HasExpiry,
                ExpiryDate = c.ExpiryDate,
                FileUrl = c.FileUrl
            }).ToList();

            // Tải biểu phí tư vấn
            var fees = (await _unitOfWork.Repository<ExpertApplicationFeeProposal>().FindAsync(f => f.ApplicationId == profile.LatestApplicationId)).ToList();
            var publicFees = fees.Select(f => new ExpertPublicFeeDto
            {
                Id = f.Id,
                SessionType = f.SessionType,
                DurationMinutes = f.DurationMinutes,
                Fee = f.ProposedFee
            }).OrderBy(f => f.Fee).ToList();

            // Tải 5 review công khai gần nhất (Mask tên reviewer nếu IsAnonymous)
            var reviews = (await _unitOfWork.Repository<ExpertReview>().FindAsync(r =>
                r.ExpertProfileId == profile.Id && r.IsPublished)).OrderByDescending(r => r.CreatedAt).Take(5).ToList();

            var reviewerUserIds = reviews.Select(r => r.UserId).Distinct().ToList();
            var reviewerUsers = (await _unitOfWork.Repository<User>().FindAsync(u => reviewerUserIds.Contains(u.UserId))).ToDictionary(u => u.UserId);

            var publicReviews = reviews.Select(r =>
            {
                var reviewerName = reviewerUsers.TryGetValue(r.UserId, out var rUser) ? rUser.FullName : "Khách hàng";
                return new ExpertPublicReviewDto
                {
                    Id = r.Id,
                    Rating = r.Rating,
                    Comment = r.Comment,
                    ReviewerDisplayName = ExpertSearchHelper.MaskReviewerName(reviewerName, r.IsAnonymous),
                    CreatedAt = r.CreatedAt
                };
            }).ToList();

            // Tải các khung giờ rảnh sắp tới (14 ngày)
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var slots = (await _unitOfWork.Repository<ExpertSlot>().FindAsync(s =>
                s.ExpertProfileId == profile.Id &&
                s.SlotDate >= today &&
                s.SlotDate <= today.AddDays(14) &&
                !s.IsBooked &&
                s.IsActive)).OrderBy(s => s.SlotDate).ThenBy(s => s.StartTime).ToList();

            var publicSlots = slots.Select(s => new ExpertAvailableSlotDto
            {
                Id = s.Id,
                SlotDate = s.SlotDate,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                SessionType = s.SessionType,
                IsBooked = s.IsBooked,
                IsActive = s.IsActive
            }).ToList();

            return new ExpertDetailResponse
            {
                Id = profile.Id,
                UserId = profile.UserId,
                FullName = app?.FullName ?? user?.FullName ?? string.Empty,
                AvatarUrl = app?.AvatarUrl,
                JobTitle = profile.JobTitle,
                CompanyName = profile.CompanyName,
                Bio = profile.Bio,
                YearsOfExperience = profile.YearsOfExperience,
                Rating = profile.Rating,
                TotalReviews = profile.TotalReviews,
                CompletedConsultationsCount = profile.CompletedConsultationsCount,
                IsActive = profile.IsActive,
                SpecializationNames = specs.Select(s => s.Name).ToList(),
                VerifiedCertificates = maskedCerts,
                FeePackages = publicFees,
                RecentReviews = publicReviews,
                UpcomingAvailableSlots = publicSlots
            };
        }

        // ── 4. LẤY DANH SÁCH REVIEW (Đánh giá & Feedback phân trang) ────────────────────
        public async Task<PagedResult<ExpertPublicReviewDto>> GetExpertReviewsAsync(Guid expertProfileId, int page = 1, int size = 10, int? rating = null)
        {
            var profile = await _unitOfWork.Repository<ExpertProfile>().GetByIdAsync(expertProfileId);
            if (profile == null)
            {
                throw new NotFoundException($"Không tìm thấy chuyên gia với Id = {expertProfileId}.");
            }

            var allReviews = (await _unitOfWork.Repository<ExpertReview>().FindAsync(r =>
                r.ExpertProfileId == expertProfileId && r.IsPublished)).AsQueryable();

            if (rating.HasValue && rating.Value >= 1 && rating.Value <= 5)
            {
                allReviews = allReviews.Where(r => r.Rating == rating.Value);
            }

            allReviews = allReviews.OrderByDescending(r => r.CreatedAt);

            var totalItems = allReviews.Count();
            var paged = allReviews.Skip((page - 1) * size).Take(size).ToList();

            var userIds = paged.Select(r => r.UserId).Distinct().ToList();
            var users = (await _unitOfWork.Repository<User>().FindAsync(u => userIds.Contains(u.UserId))).ToDictionary(u => u.UserId);

            var items = paged.Select(r =>
            {
                var name = users.TryGetValue(r.UserId, out var u) ? u.FullName : "Khách hàng";
                return new ExpertPublicReviewDto
                {
                    Id = r.Id,
                    Rating = r.Rating,
                    Comment = r.Comment,
                    ReviewerDisplayName = ExpertSearchHelper.MaskReviewerName(name, r.IsAnonymous),
                    CreatedAt = r.CreatedAt
                };
            }).ToList();

            var totalPages = (int)Math.Ceiling(totalItems / (double)size);

            return new PagedResult<ExpertPublicReviewDto>
            {
                Items = items,
                Pagination = new PaginationMeta
                {
                    Page = page,
                    PageSize = size,
                    TotalItems = totalItems,
                    TotalPages = totalPages
                }
            };
        }

        // ── 5. LẤY DANH SÁCH LỊCH RẢNH SẮP TỚI ──────────────────────────────────────────
        public async Task<List<ExpertAvailableSlotDto>> GetExpertUpcomingSlotsAsync(Guid expertProfileId, DateOnly? fromDate = null, int days = 14)
        {
            var profile = await _unitOfWork.Repository<ExpertProfile>().GetByIdAsync(expertProfileId);
            if (profile == null)
            {
                throw new NotFoundException($"Không tìm thấy chuyên gia với Id = {expertProfileId}.");
            }

            var start = fromDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var end = start.AddDays(Math.Max(1, Math.Min(days, 30)));

            var nowUtc = DateTimeOffset.UtcNow;
            var slots = (await _unitOfWork.Repository<ExpertSlot>().FindAsync(s =>
                s.ExpertProfileId == expertProfileId &&
                s.SlotDate >= start &&
                s.SlotDate <= end &&
                !s.IsBooked &&
                s.IsActive &&
                (s.HoldExpiresAt == null || s.HoldExpiresAt <= nowUtc))).OrderBy(s => s.SlotDate).ThenBy(s => s.StartTime).ToList();

            return slots.Select(s => new ExpertAvailableSlotDto
            {
                Id = s.Id,
                SlotDate = s.SlotDate,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                SessionType = s.SessionType,
                IsBooked = s.IsBooked,
                IsActive = s.IsActive
            }).ToList();
        }
    }
}
