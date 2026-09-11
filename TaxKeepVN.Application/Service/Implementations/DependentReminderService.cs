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

        public DependentReminderService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<PagedResult<AgeReminderItemDto>> GetAgeTransitionRemindersAsync(Guid userId, int taxYear, QueryParameters query)
        {
            if (taxYear < 2000 || taxYear > 2100)
                throw new BadRequestException("INVALID_TAX_YEAR", "Năm tính thuế không hợp lệ. Vui lòng nhập năm trong khoảng từ 2000 đến 2100.");

            var dependentRepo = _unitOfWork.Repository<Dependent>();
            var dependents = await dependentRepo.FindAsync(d =>
                d.TaxpayerId == userId &&
                !d.IsDeleted &&
                d.CurrentGroup == DependentGroup.CHILD_UNDER_18);

            var currentDate = DateTime.UtcNow;
            var reminders = new List<AgeReminderItemDto>();

            foreach (var dep in dependents)
            {
                var turning18Date = dep.BirthDate.AddYears(18);
                var daysRemaining = (turning18Date - currentDate).Days;

                bool isTurningSoon = daysRemaining >= 0 && daysRemaining <= 60;
                bool isAlready18 = daysRemaining < 0 && turning18Date.Year <= taxYear;

                if (!isTurningSoon && !isAlready18) continue;

                string status = isTurningSoon ? "TURNING_18_SOON" : "ALREADY_18_PENDING_ACTION";
                string message = isTurningSoon
                    ? $"Người phụ thuộc {dep.FullName} sẽ tròn 18 tuổi vào ngày {turning18Date:dd/MM/yyyy}. Vui lòng cập nhật hồ sơ sinh viên nếu tiếp tục theo học."
                    : $"Người phụ thuộc {dep.FullName} đã tròn 18 tuổi. Khoản giảm trừ sẽ bị tạm dừng nếu không bổ sung giấy tờ sinh viên.";

                reminders.Add(new AgeReminderItemDto
                {
                    DependentId = dep.Id,
                    FullName = dep.FullName,
                    BirthDate = dep.BirthDate.ToString("yyyy-MM-dd"),
                    CurrentGroup = dep.CurrentGroup.ToString(),
                    RecommendedGroup = DependentGroup.CHILD_OVER_18_STUDYING.ToString(),
                    TransitionStatus = status,
                    Turning18Date = turning18Date.ToString("yyyy-MM-dd"),
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
