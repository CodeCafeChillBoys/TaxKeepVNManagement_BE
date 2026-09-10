using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaxKeepVN.Application.DTOs;
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

        public async Task<DependentAgeReminderResponseDto> GetAgeTransitionRemindersAsync(Guid userId, int taxYear)
        {
            if (taxYear < 2000 || taxYear > 2100)
            {
                throw new BadRequestException("INVALID_TAX_YEAR", "Năm tính thuế không hợp lệ. Vui lòng nhập năm trong khoảng từ 2000 đến 2100.");
            }

            var dependentRepo = _unitOfWork.Repository<Dependent>();
            
            // Lấy danh sách NPT UNDER_18, chưa bị xóa, thuộc về userId
            var dependents = await dependentRepo.FindAsync(d => 
                d.TaxpayerId == userId && 
                !d.IsDeleted && 
                d.CurrentGroup == DependentGroup.CHILD_UNDER_18);

            var response = new DependentAgeReminderResponseDto();
            var reminders = new List<AgeReminderItemDto>();

            var currentDate = DateTime.UtcNow; // Or a specific date if testing

            foreach (var dep in dependents)
            {
                var turning18Date = dep.BirthDate.AddYears(18);
                var daysRemaining = (turning18Date - currentDate).Days;

                var isTurningSoon = daysRemaining >= 0 && daysRemaining <= 60;
                var isAlready18 = daysRemaining < 0 && turning18Date.Year <= taxYear;

                if (isTurningSoon || isAlready18)
                {
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
            }

            response.TotalReminders = reminders.Count;
            response.Reminders = reminders;

            return response;
        }
    }
}
