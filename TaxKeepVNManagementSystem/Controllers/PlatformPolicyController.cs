using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaxKeepVN.Application.DTOs.PlatformPolicy;
using TaxKeepVN.Application.DTOs.Responses;
using TaxKeepVN.Application.DTOs.SystemConfigs;
using TaxKeepVN.Application.Helpers;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVNManagementSystem.Controllers
{
    [ApiController]
    [Route("api/v1/admin/platform-policy")]
    [Authorize(Roles = "admin")]
    [Tags("Admin - Platform Policy (Chính sách sàn tư vấn & Escrow)")]
    public class PlatformPolicyController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISystemConfigService _configService;

        public PlatformPolicyController(
            IUnitOfWork unitOfWork,
            ISystemConfigService configService)
        {
            _unitOfWork = unitOfWork;
            _configService = configService;
        }

        /// <summary>
        /// [Admin] Xem toàn bộ các thông số vận hành sàn tư vấn và quy chế Escrow hiện hành.
        /// </summary>
        // GET /api/v1/admin/platform-policy
        [HttpGet(Name = "GetPlatformPolicy")]
        [EndpointSummary("[Admin] Xem toàn bộ 15 thông số vận hành sàn tư vấn và Escrow hiện hành")]
        [ProducesResponseType(typeof(ApiResponse<PlatformPolicyResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPlatformPolicy()
        {
            var repo = _unitOfWork.Repository<ConsultationPlatformPolicy>();
            var policies = await repo.FindAsync(p => p.IsActive);
            var policy = policies.OrderByDescending(p => p.Id).FirstOrDefault();

            if (policy == null)
            {
                policy = (await repo.FindAsync(p => p.Id == 1)).FirstOrDefault();
            }

            if (policy == null)
            {
                // Fallback mặc định theo quy chế sàn tư vấn
                policy = new ConsultationPlatformPolicy
                {
                    Id = 1,
                    ExpertMinFeeVnd = 100000m,
                    ExpertMaxFeeVnd = 2000000m,
                    BookingSlotHoldMinutes = 10,
                    BookingMinLeadTimeHours = 4,
                    ExpertConfirmTimeoutHours = 2,
                    PlatformCommissionRatePercent = 20m,
                    SessionOpenBeforeMinutes = 10,
                    SessionNoShowMinutes = 15,
                    RatingWindowDays = 7,
                    EscrowCooldownHours = 24,
                    CancelFullRefundHours = 24,
                    CancelPartialRefundHours = 4,
                    CancelPartialRefundPercent = 50m,
                    ExpertMaxDailySlots = 6,
                    ExpertMinWithdrawalVnd = 500000m,
                    ExpertLateCancelHours = 12,
                    ExpertLateCancelPenaltyVnd = 100000m,
                    DisputeWindowHours = 24,
                    DisputeExpertReplyHours = 24,
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
            }

            var response = MapToResponse(policy);
            return Ok(ApiResponse<PlatformPolicyResponseDto>.Ok(response, "Lấy thông tin chính sách sàn tư vấn và Escrow thành công."));
        }

        /// <summary>
        /// [Admin] Cập nhật các tham số chính sách sàn tư vấn (hoa hồng %, thời gian giữ slot, timeout, escrow cooldown...).
        /// </summary>
        // PUT /api/v1/admin/platform-policy
        [HttpPut(Name = "UpdatePlatformPolicy")]
        [EndpointSummary("[Admin] Cập nhật hoa hồng %, thời gian giữ slot, timeout, escrow cooldown...")]
        [ProducesResponseType(typeof(ApiResponse<PlatformPolicyResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdatePlatformPolicy([FromBody] UpdatePlatformPolicyRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse<object>.ValidationFail(ModelState));
            }

            var repo = _unitOfWork.Repository<ConsultationPlatformPolicy>();
            var policies = await repo.FindAsync(p => p.IsActive);
            var policy = policies.OrderByDescending(p => p.Id).FirstOrDefault();

            if (policy == null)
            {
                policy = (await repo.FindAsync(p => p.Id == 1)).FirstOrDefault();
            }

            bool isNew = false;
            if (policy == null)
            {
                isNew = true;
                policy = new ConsultationPlatformPolicy
                {
                    Id = 1,
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow
                };
            }

            // Kiểm tra tính nhất quán logic nghiệp vụ
            var effectiveMinFee = request.ExpertMinFeeVnd ?? policy.ExpertMinFeeVnd;
            var effectiveMaxFee = request.ExpertMaxFeeVnd ?? policy.ExpertMaxFeeVnd;
            if (effectiveMinFee > effectiveMaxFee)
            {
                return BadRequest(ApiResponse<object>.Fail("INVALID_FEE_RANGE",
                    $"Phí tư vấn tối thiểu ({effectiveMinFee:N0} đ) không được lớn hơn phí tư vấn tối đa ({effectiveMaxFee:N0} đ)."));
            }

            var effectiveCancelFull = request.CancelFullRefundHours ?? policy.CancelFullRefundHours;
            var effectiveCancelPartial = request.CancelPartialRefundHours ?? policy.CancelPartialRefundHours;
            if (effectiveCancelFull < effectiveCancelPartial)
            {
                return BadRequest(ApiResponse<object>.Fail("INVALID_CANCEL_HOURS",
                    $"Mốc hủy lịch hoàn tiền 100% ({effectiveCancelFull}h) phải lớn hơn hoặc bằng mốc hoàn một phần ({effectiveCancelPartial}h)."));
            }

            // Gán các giá trị được cập nhật
            if (request.ExpertMinFeeVnd.HasValue) policy.ExpertMinFeeVnd = request.ExpertMinFeeVnd.Value;
            if (request.ExpertMaxFeeVnd.HasValue) policy.ExpertMaxFeeVnd = request.ExpertMaxFeeVnd.Value;
            if (request.BookingSlotHoldMinutes.HasValue) policy.BookingSlotHoldMinutes = request.BookingSlotHoldMinutes.Value;
            if (request.BookingMinLeadTimeHours.HasValue) policy.BookingMinLeadTimeHours = request.BookingMinLeadTimeHours.Value;
            if (request.ExpertConfirmTimeoutHours.HasValue) policy.ExpertConfirmTimeoutHours = request.ExpertConfirmTimeoutHours.Value;
            if (request.PlatformCommissionRatePercent.HasValue) policy.PlatformCommissionRatePercent = request.PlatformCommissionRatePercent.Value;
            if (request.SessionOpenBeforeMinutes.HasValue) policy.SessionOpenBeforeMinutes = request.SessionOpenBeforeMinutes.Value;
            if (request.SessionNoShowMinutes.HasValue) policy.SessionNoShowMinutes = request.SessionNoShowMinutes.Value;
            if (request.RatingWindowDays.HasValue) policy.RatingWindowDays = request.RatingWindowDays.Value;
            if (request.EscrowCooldownHours.HasValue) policy.EscrowCooldownHours = request.EscrowCooldownHours.Value;
            if (request.CancelFullRefundHours.HasValue) policy.CancelFullRefundHours = request.CancelFullRefundHours.Value;
            if (request.CancelPartialRefundHours.HasValue) policy.CancelPartialRefundHours = request.CancelPartialRefundHours.Value;
            if (request.CancelPartialRefundPercent.HasValue) policy.CancelPartialRefundPercent = request.CancelPartialRefundPercent.Value;
            if (request.ExpertMaxDailySlots.HasValue) policy.ExpertMaxDailySlots = request.ExpertMaxDailySlots.Value;
            if (request.ExpertMinWithdrawalVnd.HasValue) policy.ExpertMinWithdrawalVnd = request.ExpertMinWithdrawalVnd.Value;
            if (request.ExpertLateCancelHours.HasValue) policy.ExpertLateCancelHours = request.ExpertLateCancelHours.Value;
            if (request.ExpertLateCancelPenaltyVnd.HasValue) policy.ExpertLateCancelPenaltyVnd = request.ExpertLateCancelPenaltyVnd.Value;
            if (request.DisputeWindowHours.HasValue) policy.DisputeWindowHours = request.DisputeWindowHours.Value;
            if (request.DisputeExpertReplyHours.HasValue) policy.DisputeExpertReplyHours = request.DisputeExpertReplyHours.Value;

            policy.UpdatedAt = DateTimeOffset.UtcNow;

            if (isNew)
            {
                await repo.AddAsync(policy);
            }
            else
            {
                repo.Update(policy);
            }

            await _unitOfWork.SaveChangesAsync();

            // Đồng bộ sang bảng system_configs để tương thích ngược cho các query cũ (nếu có)
            await SyncToSystemConfigSafelyAsync(policy);

            var response = MapToResponse(policy);
            return Ok(ApiResponse<PlatformPolicyResponseDto>.Ok(response, "Cập nhật chính sách sàn tư vấn và Escrow thành công."));
        }

        // ── Helper Mapping & Sync ────────────────────────────────────────────────

        private static PlatformPolicyResponseDto MapToResponse(ConsultationPlatformPolicy p)
        {
            return new PlatformPolicyResponseDto
            {
                Id = p.Id,
                ExpertMinFeeVnd = p.ExpertMinFeeVnd,
                ExpertMaxFeeVnd = p.ExpertMaxFeeVnd,
                BookingSlotHoldMinutes = p.BookingSlotHoldMinutes,
                BookingMinLeadTimeHours = p.BookingMinLeadTimeHours,
                ExpertConfirmTimeoutHours = p.ExpertConfirmTimeoutHours,
                PlatformCommissionRatePercent = p.PlatformCommissionRatePercent,
                SessionOpenBeforeMinutes = p.SessionOpenBeforeMinutes,
                SessionNoShowMinutes = p.SessionNoShowMinutes,
                RatingWindowDays = p.RatingWindowDays,
                EscrowCooldownHours = p.EscrowCooldownHours,
                CancelFullRefundHours = p.CancelFullRefundHours,
                CancelPartialRefundHours = p.CancelPartialRefundHours,
                CancelPartialRefundPercent = p.CancelPartialRefundPercent,
                ExpertMaxDailySlots = p.ExpertMaxDailySlots,
                ExpertMinWithdrawalVnd = p.ExpertMinWithdrawalVnd,
                ExpertLateCancelHours = p.ExpertLateCancelHours,
                ExpertLateCancelPenaltyVnd = p.ExpertLateCancelPenaltyVnd,
                DisputeWindowHours = p.DisputeWindowHours,
                DisputeExpertReplyHours = p.DisputeExpertReplyHours,
                IsActive = p.IsActive,
                UpdatedAt = p.UpdatedAt
            };
        }

        private async Task SyncToSystemConfigSafelyAsync(ConsultationPlatformPolicy p)
        {
            var syncMap = new (string Key, string Value, string Description)[]
            {
                (BookingHelper.ConfigKeyExpertMinFeeVnd, p.ExpertMinFeeVnd.ToString("0.##"), "Phí tư vấn sàn tối thiểu chung của hệ thống (VNĐ)"),
                (BookingHelper.ConfigKeyExpertMaxFeeVnd, p.ExpertMaxFeeVnd.ToString("0.##"), "Phí tư vấn trần tối đa chung của hệ thống (VNĐ)"),
                (BookingHelper.ConfigKeySlotHoldMinutes, p.BookingSlotHoldMinutes.ToString(), "Thời gian tạm giữ slot chờ thanh toán (Phút)"),
                (BookingHelper.ConfigKeyMinLeadTimeHours, p.BookingMinLeadTimeHours.ToString(), "Thời gian tối thiểu phải đặt trước giờ hẹn (Giờ)"),
                (BookingHelper.ConfigKeyExpertConfirmTimeoutHours, p.ExpertConfirmTimeoutHours.ToString(), "Hạn chót chuyên gia xác nhận ca đặt lịch (Giờ)"),
                (BookingHelper.ConfigKeyPlatformCommissionRatePercent, p.PlatformCommissionRatePercent.ToString("0.##"), "Tỷ lệ phí hoa hồng nền tảng trích từ ca tư vấn (%)"),
                (BookingHelper.ConfigKeySessionOpenBeforeMinutes, p.SessionOpenBeforeMinutes.ToString(), "Thời gian mở phòng trước giờ hẹn (Phút)"),
                (BookingHelper.ConfigKeySessionNoShowMinutes, p.SessionNoShowMinutes.ToString(), "Ngưỡng thời gian xác định vắng mặt No-Show (Phút)"),
                (BookingHelper.ConfigKeyRatingWindowDays, p.RatingWindowDays.ToString(), "Thời hạn cho phép gửi đánh giá sau ca tư vấn (Ngày)"),
                (BookingHelper.ConfigKeyEscrowCooldownHours, p.EscrowCooldownHours.ToString(), "Thời gian tạm giữ tiền Escrow chờ khiếu nại trước khi tự giải ngân (Giờ)"),
                (BookingHelper.ConfigKeyCancelFullRefundHours, p.CancelFullRefundHours.ToString(), "Mốc hủy lịch sớm được hoàn tiền 100% (Giờ)"),
                (BookingHelper.ConfigKeyCancelPartialRefundHours, p.CancelPartialRefundHours.ToString(), "Mốc hủy lịch cận giờ được hoàn một phần (Giờ)"),
                (BookingHelper.ConfigKeyCancelPartialRefundPercent, p.CancelPartialRefundPercent.ToString("0.##"), "Tỷ lệ hoàn tiền khi hủy cận giờ (%)"),
                (BookingHelper.ConfigKeyExpertMaxDailySlots, p.ExpertMaxDailySlots.ToString(), "Số ca tư vấn tối đa chuyên gia được mở trong ngày"),
                (BookingHelper.ConfigKeyExpertMinWithdrawalVnd, p.ExpertMinWithdrawalVnd.ToString("0.##"), "Hạn mức rút tiền tối thiểu từ ví chuyên gia (VNĐ)")
            };

            foreach (var item in syncMap)
            {
                try
                {
                    await _configService.UpdateAsync(item.Key, new SystemConfigUpdateDto
                    {
                        ConfigValue = item.Value,
                        Description = item.Description
                    });
                }
                catch
                {
                    // Nuốt lỗi sync-back để không làm gián đoạn cập nhật chính vào consultation_platform_policies
                }
            }
        }
    }
}
