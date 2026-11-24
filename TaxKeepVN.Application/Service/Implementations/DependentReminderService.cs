using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs;
using TaxKeepVN.Application.DTOs.Common;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    public class DependentReminderService : IDependentReminderService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IDependentCatalogService? _catalogService;

        public DependentReminderService(IUnitOfWork unitOfWork, IDependentCatalogService? catalogService = null)
        {
            _unitOfWork = unitOfWork;
            _catalogService = catalogService;
        }

        public async Task<PagedResult<AgeReminderItemDto>> GetAgeTransitionRemindersAsync(Guid userId, int taxYear, QueryParameters query)
        {
            if (taxYear < 2000 || taxYear > 2100)
                throw new BadRequestException("INVALID_TAX_YEAR", "Năm tính thuế không hợp lệ. Vui lòng nhập năm trong khoảng từ 2000 đến 2100.");

            List<DependentGroupCatalog> allGroups;
            if (_catalogService != null)
            {
                allGroups = await _catalogService.GetActiveGroupsAsync();
            }
            else
            {
                var groupRepo = _unitOfWork.Repository<DependentGroupCatalog>();
                allGroups = groupRepo != null
                    ? (await groupRepo.FindAsync(g => g.IsActive)).ToList()
                    : new List<DependentGroupCatalog>();
            }

            var groupsWithMaxAge = allGroups
                .Where(g => g.MaxAge.HasValue)
                .ToDictionary(g => g.Code, StringComparer.OrdinalIgnoreCase);

            var dependentRepo = _unitOfWork.Repository<Dependent>();
            var allDeps = await dependentRepo.FindAsync(d =>
                d.TaxpayerId == userId &&
                !d.IsDeleted);

            var dependents = allDeps.Where(d => groupsWithMaxAge.ContainsKey(d.CurrentGroup)).ToList();

            var currentDate = DateTime.UtcNow;
            var reminders = new List<AgeReminderItemDto>();

            foreach (var dep in dependents)
            {
                var curGroup = groupsWithMaxAge[dep.CurrentGroup];
                var maxAge = curGroup.MaxAge!.Value;
                var turningDate = dep.BirthDate.AddYears(maxAge);
                var daysRemaining = (turningDate - currentDate).Days;

                bool isTurningSoon = daysRemaining >= 0 && daysRemaining <= 60;
                bool isAlreadyAge = daysRemaining < 0 && turningDate.Year <= taxYear;

                if (!isTurningSoon && !isAlreadyAge) continue;

                var relCode = curGroup.RelationshipCode ?? dep.Relationship;
                var candidateGroups = allGroups
                    .Where(g => string.Equals(g.RelationshipCode, relCode, StringComparison.OrdinalIgnoreCase)
                                && g.MinAge == maxAge
                                && g.Level > curGroup.Level)
                    .OrderByDescending(g => g.RequireStudying)
                    .ThenBy(g => g.Level)
                    .ToList();

                var recommendedGroup = candidateGroups.FirstOrDefault();

                string status = isTurningSoon ? $"TURNING_{maxAge}_SOON" : $"ALREADY_{maxAge}_PENDING_ACTION";
                string message;
                if (isTurningSoon)
                {
                    message = recommendedGroup != null
                        ? $"Người phụ thuộc {dep.FullName} sẽ tròn {maxAge} tuổi vào ngày {turningDate:dd/MM/yyyy}. Vui lòng chuyển sang nhóm '{recommendedGroup.Name}' và bổ sung giấy tờ nếu tiếp tục đủ điều kiện."
                        : $"Người phụ thuộc {dep.FullName} sẽ tròn {maxAge} tuổi vào ngày {turningDate:dd/MM/yyyy}. Vui lòng cập nhật thông tin nhóm và bổ sung giấy tờ phù hợp nếu tiếp tục đủ điều kiện.";
                }
                else
                {
                    message = recommendedGroup != null
                        ? $"Người phụ thuộc {dep.FullName} đã tròn {maxAge} tuổi. Vui lòng chuyển sang nhóm '{recommendedGroup.Name}' và bổ sung giấy tờ để tiếp tục được tính giảm trừ."
                        : $"Người phụ thuộc {dep.FullName} đã tròn {maxAge} tuổi. Khoản giảm trừ sẽ bị tạm dừng nếu không cập nhật nhóm và bổ sung giấy tờ phù hợp.";
                }

                reminders.Add(new AgeReminderItemDto
                {
                    DependentId = dep.Id,
                    FullName = dep.FullName,
                    BirthDate = dep.BirthDate.ToString("yyyy-MM-dd"),
                    CurrentGroup = dep.CurrentGroup,
                    RecommendedGroup = recommendedGroup?.Code,
                    TransitionStatus = status,
                    Turning18Date = turningDate.ToString("yyyy-MM-dd"),
                    DaysRemaining = daysRemaining,
                    Message = message,
                    RequiredAction = "UPDATE_GROUP_AND_UPLOAD_STUDENT_CARD"
                });
            }

            // Searching by full name
            IEnumerable<AgeReminderItemDto> result = reminders;
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var kw = query.Search.ToLower();
                result = result.Where(r => r.FullName.ToLower().Contains(kw));
            }

            // Sorting
            result = ApplySort(result, query.Sort);

            var totalItems = result.Count();
            var items = result
                .Skip((query.Page - 1) * query.Size)
                .Take(query.Size)
                .ToList();

            return new PagedResult<AgeReminderItemDto>
            {
                Items = items,
                Pagination = new PaginationMeta
                {
                    Page = query.Page,
                    PageSize = query.Size,
                    TotalItems = totalItems,
                    TotalPages = (int)Math.Ceiling(totalItems / (double)query.Size)
                }
            };
        }

        private static IEnumerable<AgeReminderItemDto> ApplySort(IEnumerable<AgeReminderItemDto> source, string? sort)
        {
            if (string.IsNullOrWhiteSpace(sort))
                return source.OrderBy(r => r.DaysRemaining); // Mặc định: sắp xếp theo ngày sắp đến gần nhất

            bool desc = sort.StartsWith("-");
            string field = sort.TrimStart('-').ToLower();

            return field switch
            {
                "fullname" => desc ? source.OrderByDescending(r => r.FullName) : source.OrderBy(r => r.FullName),
                "daysremaining" => desc ? source.OrderByDescending(r => r.DaysRemaining) : source.OrderBy(r => r.DaysRemaining),
                "birthdate" => desc ? source.OrderByDescending(r => r.BirthDate) : source.OrderBy(r => r.BirthDate),
                "transitionstatus" => desc ? source.OrderByDescending(r => r.TransitionStatus) : source.OrderBy(r => r.TransitionStatus),
                _ => source.OrderBy(r => r.DaysRemaining)
            };
        }
    }
}
