using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.DTOs.SystemConfigs;
using TaxKeepVN.Application.DTOs.TaxSettlement;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVNManagementSystem.Controllers
{
    [ApiController]
    [Route("api/v1/admin/tax-settlement-schedule")]
    [Authorize(Roles = "admin")]
    [Produces("application/json")]
    [Tags("Admin - Tax Settlement Schedule (Lịch trình quyết toán thuế)")]
    public class TaxSettlementScheduleController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISystemConfigService _configService;

        public TaxSettlementScheduleController(
            IUnitOfWork unitOfWork,
            ISystemConfigService configService)
        {
            _unitOfWork = unitOfWork;
            _configService = configService;
        }

        /// <summary>
        /// [Admin] Lấy cấu hình lịch trình quyết toán thuế TNCN hiện hành (Hạn chót và các mốc ngày nhắc nhở).
        /// </summary>
        // GET /api/v1/admin/tax-settlement-schedule
        [HttpGet(Name = "GetTaxSettlementSchedule")]
        [EndpointSummary("[Admin] Xem hạn chót và các mốc ngày gửi thông báo nhắc nhở")]
        [ProducesResponseType(typeof(ApiResponse<TaxSettlementScheduleResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetSchedule([FromQuery] int? taxYear = null)
        {
            var repo = _unitOfWork.Repository<TaxSettlementSchedule>();
            var schedules = await repo.FindAsync(s => s.IsActive && (taxYear == null || s.TaxYear == null || s.TaxYear == taxYear));
            var schedule = schedules
                .OrderByDescending(s => s.TaxYear ?? 0)
                .ThenBy(s => s.Id)
                .FirstOrDefault();

            if (schedule == null)
            {
                // Fallback mặc định theo Luật Quản lý thuế số 38/2019/QH14 (Hạn chót 30/04)
                schedule = new TaxSettlementSchedule
                {
                    Id = 1,
                    TaxYear = taxYear,
                    DeadlineMonth = 4,
                    DeadlineDay = 30,
                    ReminderDays = "60,30,15,7,3,1",
                    Description = "Hạn chót quyết toán thuế TNCN theo Luật Quản lý thuế số 38/2019/QH14 (Ngày 30/4)",
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
            }

            var response = MapToResponse(schedule);
            return Ok(ApiResponse<TaxSettlementScheduleResponseDto>.Ok(response, "Lấy thông tin lịch trình quyết toán thuế thành công."));
        }

        /// <summary>
        /// [Admin] Cập nhật hoặc thay đổi ngày hạn chót và thêm/bớt các mốc ngày gửi thông báo nhắc nhở quyết toán thuế.
        /// </summary>
        // PUT /api/v1/admin/tax-settlement-schedule
        [HttpPut(Name = "UpdateTaxSettlementSchedule")]
        [EndpointSummary("[Admin] Thay đổi ngày hạn chót hoặc thêm/bớt các mốc nhắc nhở")]
        [ProducesResponseType(typeof(ApiResponse<TaxSettlementScheduleResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateSchedule([FromBody] UpdateTaxSettlementScheduleRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));
            }

            // Kiểm tra tính hợp lệ của ngày trong tháng (vd: tháng 2 không có ngày 30, 31)
            int maxDaysInMonth = DateTime.DaysInMonth(2024, request.DeadlineMonth); // Năm nhuận 2024 làm chuẩn tối đa 29 ngày tháng 2
            if (request.DeadlineDay > maxDaysInMonth)
            {
                return BadRequest(ApiResponse<object>.Fail("INVALID_DEADLINE_DATE",
                    $"Tháng {request.DeadlineMonth} chỉ có tối đa {maxDaysInMonth} ngày. Ngày {request.DeadlineDay} không hợp lệ."));
            }

            // Chuẩn hóa danh sách mốc ngày nhắc nhở
            List<int> normalizedDays;
            if (request.ReminderDays != null && request.ReminderDays.Any())
            {
                normalizedDays = request.ReminderDays
                    .Where(d => d > 0)
                    .Distinct()
                    .OrderByDescending(d => d)
                    .ToList();
            }
            else if (!string.IsNullOrWhiteSpace(request.RawReminderDays))
            {
                normalizedDays = request.RawReminderDays
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => int.TryParse(s.Trim(), out var d) ? d : 0)
                    .Where(d => d > 0)
                    .Distinct()
                    .OrderByDescending(d => d)
                    .ToList();
            }
            else
            {
                normalizedDays = new List<int> { 60, 30, 15, 7, 3, 1 };
            }

            if (!normalizedDays.Any())
            {
                return BadRequest(ApiResponse<object>.Fail("VALIDATION_ERROR", "Phải thiết lập ít nhất 1 mốc ngày gửi thông báo nhắc nhở trước hạn."));
            }

            string reminderDaysStr = string.Join(",", normalizedDays);

            // 1. Lưu vào bảng chuyên biệt tax_settlement_schedules
            var repo = _unitOfWork.Repository<TaxSettlementSchedule>();
            var existingSchedules = await repo.FindAsync(s => s.IsActive && (request.TaxYear == null || s.TaxYear == request.TaxYear));
            var schedule = existingSchedules.OrderByDescending(s => s.TaxYear ?? 0).FirstOrDefault();

            if (schedule != null)
            {
                schedule.DeadlineMonth = request.DeadlineMonth;
                schedule.DeadlineDay = request.DeadlineDay;
                schedule.ReminderDays = reminderDaysStr;
                schedule.TaxYear = request.TaxYear;
                if (!string.IsNullOrWhiteSpace(request.Description))
                {
                    schedule.Description = request.Description;
                }
                schedule.UpdatedAt = DateTimeOffset.UtcNow;
                repo.Update(schedule);
            }
            else
            {
                schedule = new TaxSettlementSchedule
                {
                    TaxYear = request.TaxYear,
                    DeadlineMonth = request.DeadlineMonth,
                    DeadlineDay = request.DeadlineDay,
                    ReminderDays = reminderDaysStr,
                    Description = request.Description ?? $"Hạn chót quyết toán thuế TNCN ngày {request.DeadlineDay:D2}/{request.DeadlineMonth:D2}",
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                await repo.AddAsync(schedule);
            }

            await _unitOfWork.SaveChangesAsync();

            // 2. Đồng bộ sang system_configs để tương thích ngược
            await _configService.UpdateAsync("TAX_SETTLEMENT_DEADLINE_MONTH", new SystemConfigUpdateDto
            {
                ConfigValue = request.DeadlineMonth.ToString(),
                Description = $"Tháng hạn chót quyết toán thuế TNCN ({request.DeadlineMonth})"
            });

            await _configService.UpdateAsync("TAX_SETTLEMENT_DEADLINE_DAY", new SystemConfigUpdateDto
            {
                ConfigValue = request.DeadlineDay.ToString(),
                Description = $"Ngày hạn chót quyết toán thuế TNCN ({request.DeadlineDay})"
            });

            await _configService.UpdateAsync("TAX_SETTLEMENT_REMINDER_DAYS", new SystemConfigUpdateDto
            {
                ConfigValue = reminderDaysStr,
                Description = $"Các mốc ngày gửi thông báo nhắc quyết toán thuế ({reminderDaysStr})"
            });

            var response = MapToResponse(schedule);
            return Ok(ApiResponse<TaxSettlementScheduleResponseDto>.Ok(response, "Cập nhật lịch trình quyết toán thuế và các mốc nhắc nhở thành công."));
        }

        // ── Helper ───────────────────────────────────────────────────────────────

        private static TaxSettlementScheduleResponseDto MapToResponse(TaxSettlementSchedule s)
        {
            var parsedDays = s.ReminderDays
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(d => int.TryParse(d.Trim(), out var val) ? val : 0)
                .Where(v => v > 0)
                .OrderByDescending(v => v)
                .ToList();

            return new TaxSettlementScheduleResponseDto
            {
                Id = s.Id,
                TaxYear = s.TaxYear,
                DeadlineMonth = s.DeadlineMonth,
                DeadlineDay = s.DeadlineDay,
                ReminderDays = parsedDays,
                RawReminderDays = s.ReminderDays,
                Description = s.Description,
                IsActive = s.IsActive,
                UpdatedAt = s.UpdatedAt
            };
        }
    }
}
