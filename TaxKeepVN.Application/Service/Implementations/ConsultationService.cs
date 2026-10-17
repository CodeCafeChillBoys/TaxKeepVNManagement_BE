using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TaxKeepVN.Application.DTOs.Consultations;
using TaxKeepVN.Application.Exceptions;
using TaxKeepVN.Application.Helpers;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.Entities;
using TaxKeepVN.Domain.Enums;
using TaxKeepVN.Domain.IRepositories;

namespace TaxKeepVN.Application.Service.Implementations
{
    /// <summary>
    /// Service xử lý phiên tư vấn trực tuyến 1-1 (Live Consultation Execution) (Đặc tả 5)
    /// Tuân thủ: Zero-Hardcode (ngưỡng thời gian đọc từ system_configs), Zero-Mock Data (dữ liệu thật qua PostgreSQL & Jitsi URL).
    /// </summary>
    public class ConsultationService : IConsultationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConsultationRealtimeNotifier? _realtimeNotifier;
        private readonly ILogger<ConsultationService> _logger;

        public ConsultationService(
            IUnitOfWork unitOfWork,
            ILogger<ConsultationService> logger,
            IConsultationRealtimeNotifier? realtimeNotifier = null)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _realtimeNotifier = realtimeNotifier;
        }

        // ── 1. ĐIỂM DANH VÀO PHÒNG TƯ VẤN (CHECK-IN / JOIN) ─────────────────────────
        public async Task<JoinConsultationResponse> JoinSessionAsync(Guid currentUserId, string currentUserRole, Guid bookingId)
        {
            var booking = await _unitOfWork.Repository<Booking>().GetByIdAsync(bookingId);
            if (booking == null)
            {
                throw new NotFoundException($"Không tìm thấy lịch hẹn với Id = {bookingId}.");
            }

            var slot = await _unitOfWork.Repository<ExpertSlot>().GetByIdAsync(booking.ExpertSlotId);
            if (slot == null)
            {
                throw new NotFoundException("Không tìm thấy khung giờ của lịch hẹn.");
            }

            var profile = await _unitOfWork.Repository<ExpertProfile>().GetByIdAsync(slot.ExpertProfileId);
            if (profile == null)
            {
                throw new NotFoundException("Không tìm thấy thông tin chuyên gia.");
            }

            var isClient = booking.UserId == currentUserId;
            var isExpert = profile.UserId == currentUserId;
            var isAdmin = string.Equals(currentUserRole, "admin", StringComparison.OrdinalIgnoreCase);

            if (!isClient && !isExpert && !isAdmin)
            {
                throw new ForbiddenException("FORBIDDEN", "Bạn không có quyền tham gia phòng tư vấn của ca này.");
            }

            if (booking.Status != BookingStatus.CONFIRMED && 
                booking.Status != BookingStatus.IN_PROGRESS && 
                booking.Status != BookingStatus.COMPLETED &&
                booking.Status != BookingStatus.SETTLED)
            {
                throw new BadRequestException("INVALID_STATUS", 
                    $"Lịch hẹn hiện tại ở trạng thái '{booking.Status}'. Phòng tư vấn chỉ mở khi lịch hẹn đã được xác nhận (CONFIRMED) hoặc đang diễn ra (IN_PROGRESS).");
            }

            var nowUtc = DateTimeOffset.UtcNow;
            var policy = await BookingHelper.GetPlatformPolicyAsync(_unitOfWork);
            var openBeforeMinutes = policy.SessionOpenBeforeMinutes;

            var slotStartUtc = BookingHelper.ConvertSlotToUtc(slot.SlotDate, slot.StartTime);
            var earliestOpenUtc = slotStartUtc.AddMinutes(-openBeforeMinutes);

            if (nowUtc < earliestOpenUtc)
            {
                var waitMinutes = (int)Math.Ceiling((earliestOpenUtc - nowUtc).TotalMinutes);
                throw new BadRequestException("ROOM_NOT_OPEN_YET", 
                    $"Phòng tư vấn chỉ mở trước giờ hẹn tối đa {openBeforeMinutes} phút (từ {earliestOpenUtc:HH:mm dd/MM/yyyy}). Vui lòng quay lại sau {waitMinutes} phút nữa.");
            }

            // Tìm hoặc khởi tạo bản ghi ConsultationSession
            var existingSessions = await _unitOfWork.Repository<ConsultationSession>().FindAsync(s => s.BookingId == bookingId);
            var session = existingSessions.FirstOrDefault();
            var isNewSession = false;

            if (session == null)
            {
                isNewSession = true;
                var cleanCode = booking.BookingCode.Replace("-", "_");
                var roomName = $"TaxKeepVN_{cleanCode}";
                var roomUrl = $"https://meet.jit.si/{roomName}#config.prejoinPageEnabled=false&config.startWithAudioMuted=false&config.startWithVideoMuted=false";

                session = new ConsultationSession
                {
                    Id = Guid.NewGuid(),
                    BookingId = bookingId,
                    RoomName = roomName,
                    RoomUrl = roomUrl,
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc
                };
            }

            // Ghi nhận mốc điểm danh (Check-in time)
            if (isClient)
            {
                session.UserCheckInAt ??= nowUtc;
            }
            if (isExpert)
            {
                session.ExpertCheckInAt ??= nowUtc;
            }

            var bothPresent = session.UserCheckInAt.HasValue && session.ExpertCheckInAt.HasValue;

            // Nếu cả 2 bên cùng có mặt và booking đang CONFIRMED -> Chuyển sang IN_PROGRESS
            if (bothPresent && booking.Status == BookingStatus.CONFIRMED)
            {
                booking.Status = BookingStatus.IN_PROGRESS;
                booking.UpdatedAt = nowUtc;
                session.StartedAt ??= nowUtc;
                _unitOfWork.Repository<Booking>().Update(booking);

                if (_realtimeNotifier != null)
                {
                    await _realtimeNotifier.NotifySessionStartedAsync(bookingId, session.StartedAt.Value);
                }
            }

            session.UpdatedAt = nowUtc;

            if (isNewSession)
            {
                await _unitOfWork.Repository<ConsultationSession>().AddAsync(session);
            }
            else
            {
                _unitOfWork.Repository<ConsultationSession>().Update(session);
            }
            await _unitOfWork.SaveChangesAsync();

            var participantRole = isExpert ? "expert" : (isClient ? "taxpayer" : "admin");
            if (_realtimeNotifier != null)
            {
                await _realtimeNotifier.NotifyParticipantJoinedAsync(bookingId, currentUserId, participantRole, bothPresent);
            }

            return new JoinConsultationResponse
            {
                SessionId = session.Id,
                BookingId = booking.Id,
                BookingCode = booking.BookingCode,
                RoomName = session.RoomName,
                RoomUrl = session.RoomUrl,
                CheckedInAt = isExpert ? (session.ExpertCheckInAt ?? nowUtc) : (session.UserCheckInAt ?? nowUtc),
                ParticipantRole = participantRole,
                BothPresent = bothPresent,
                BookingStatus = booking.Status,
                Message = bothPresent 
                    ? "Cả 2 bên đã có mặt trong phòng. Phiên tư vấn chính thức bắt đầu!" 
                    : $"Bạn đã điểm danh thành công với vai trò '{participantRole}'. Đang đợi đối phương vào phòng..."
            };
        }

        // ── 2. XEM TRẠNG THÁI PHIÊN TƯ VẤN (GET SESSION STATUS) ──────────────────────
        public async Task<ConsultationSessionResponse> GetSessionAsync(Guid currentUserId, string currentUserRole, Guid bookingId)
        {
            var booking = await _unitOfWork.Repository<Booking>().GetByIdAsync(bookingId);
            if (booking == null)
            {
                throw new NotFoundException($"Không tìm thấy lịch hẹn với Id = {bookingId}.");
            }

            var slot = await _unitOfWork.Repository<ExpertSlot>().GetByIdAsync(booking.ExpertSlotId);
            if (slot == null)
            {
                throw new NotFoundException("Không tìm thấy khung giờ của lịch hẹn.");
            }

            var profile = await _unitOfWork.Repository<ExpertProfile>().GetByIdAsync(slot.ExpertProfileId);
            var isClient = booking.UserId == currentUserId;
            var isExpert = profile != null && profile.UserId == currentUserId;
            var isAdmin = string.Equals(currentUserRole, "admin", StringComparison.OrdinalIgnoreCase);

            if (!isClient && !isExpert && !isAdmin)
            {
                throw new ForbiddenException("FORBIDDEN", "Bạn không có quyền truy cập thông tin phiên tư vấn này.");
            }

            var sessions = await _unitOfWork.Repository<ConsultationSession>().FindAsync(s => s.BookingId == bookingId);
            var session = sessions.FirstOrDefault();

            var nowUtc = DateTimeOffset.UtcNow;
            var policy = await BookingHelper.GetPlatformPolicyAsync(_unitOfWork);
            var openBeforeMinutes = policy.SessionOpenBeforeMinutes;

            var slotStartUtc = BookingHelper.ConvertSlotToUtc(slot.SlotDate, slot.StartTime);
            var isRoomOpen = nowUtc >= slotStartUtc.AddMinutes(-openBeforeMinutes);

            var scheduledDuration = (int)(slot.EndTime - slot.StartTime).TotalMinutes;
            int remainingMinutes;
            if (session?.EndedAt.HasValue == true)
            {
                remainingMinutes = 0;
            }
            else if (session?.StartedAt.HasValue == true)
            {
                var elapsed = (int)(nowUtc - session.StartedAt.Value).TotalMinutes;
                remainingMinutes = Math.Max(0, scheduledDuration - elapsed);
            }
            else
            {
                remainingMinutes = scheduledDuration;
            }

            return new ConsultationSessionResponse
            {
                Id = session?.Id ?? Guid.Empty,
                BookingId = booking.Id,
                BookingCode = booking.BookingCode,
                TopicTitle = booking.TopicTitle,
                ProblemDescription = booking.ProblemDescription,
                SessionType = booking.SessionType.ToString(),
                ScheduledDurationMinutes = scheduledDuration,
                SlotDate = slot.SlotDate,
                StartTime = slot.StartTime,
                EndTime = slot.EndTime,
                RoomName = session?.RoomName ?? $"TaxKeepVN_{booking.BookingCode.Replace("-", "_")}",
                RoomUrl = session?.RoomUrl ?? $"https://meet.jit.si/TaxKeepVN_{booking.BookingCode.Replace("-", "_")}",
                UserCheckInAt = session?.UserCheckInAt,
                ExpertCheckInAt = session?.ExpertCheckInAt,
                StartedAt = session?.StartedAt,
                EndedAt = session?.EndedAt,
                ActualDurationMinutes = session?.ActualDurationMinutes,
                RemainingMinutes = remainingMinutes,
                Status = booking.Status,
                SharedNotes = session?.SharedNotes,
                NoShowType = session?.NoShowType ?? "NONE",
                IsRoomOpen = isRoomOpen,
                IsUserPresent = session?.UserCheckInAt.HasValue == true,
                IsExpertPresent = session?.ExpertCheckInAt.HasValue == true,
                CreatedAt = session?.CreatedAt ?? booking.CreatedAt,
                UpdatedAt = session?.UpdatedAt ?? booking.UpdatedAt
            };
        }

        // ── 3. XEM CHUNG HỒ SƠ THUẾ ĐƯỢC CHIA SẺ (SHARED TAX DOSSIER) ────────────────
        public async Task<SharedTaxDossierResponse> GetSharedTaxDossierAsync(Guid currentUserId, string currentUserRole, Guid bookingId)
        {
            var booking = await _unitOfWork.Repository<Booking>().GetByIdAsync(bookingId);
            if (booking == null)
            {
                throw new NotFoundException($"Không tìm thấy lịch hẹn với Id = {bookingId}.");
            }

            var slot = await _unitOfWork.Repository<ExpertSlot>().GetByIdAsync(booking.ExpertSlotId);
            if (slot == null)
            {
                throw new NotFoundException("Không tìm thấy khung giờ của lịch hẹn.");
            }

            var profile = await _unitOfWork.Repository<ExpertProfile>().GetByIdAsync(slot.ExpertProfileId);
            var isClient = booking.UserId == currentUserId;
            var isExpert = profile != null && profile.UserId == currentUserId;
            var isAdmin = string.Equals(currentUserRole, "admin", StringComparison.OrdinalIgnoreCase);

            if (!isClient && !isExpert && !isAdmin)
            {
                throw new ForbiddenException("FORBIDDEN", "Bạn không có quyền truy cập bộ hồ sơ thuế của ca tư vấn này.");
            }

            // Quyền xem chỉ mở khi lịch hẹn ở trạng thái CONFIRMED, IN_PROGRESS, COMPLETED hoặc SETTLED (BR-3.4 & BR-5.2)
            if (booking.Status != BookingStatus.CONFIRMED && 
                booking.Status != BookingStatus.IN_PROGRESS && 
                booking.Status != BookingStatus.COMPLETED &&
                booking.Status != BookingStatus.SETTLED)
            {
                throw new ForbiddenException("FORBIDDEN", 
                    "Quyền xem trước hồ sơ thuế nội bộ chỉ được kích hoạt khi lịch hẹn đã được xác nhận (CONFIRMED) hoặc đang diễn ra.");
            }

            var taxpayer = await _unitOfWork.Repository<User>().GetByIdAsync(booking.UserId);
            var taxYear = slot.SlotDate.Year;

            // 1. Nguồn thu nhập & bảng lương
            var incomeSources = (await _unitOfWork.Repository<IncomeSource>().FindAsync(s => s.TaxpayerId == booking.UserId)).ToList();
            var monthlyIncomes = (await _unitOfWork.Repository<Income>().FindAsync(i => i.UserId == booking.UserId && i.Year == taxYear)).ToList();

            var sourceDtos = incomeSources.Select(s => new SharedIncomeSourceDto
            {
                Id = s.Id,
                CompanyName = s.CompanyName,
                CompanyTaxCode = s.CompanyTaxCode,
                TaxYear = s.TaxYear,
                TotalIncome = s.TotalIncome,
                TaxWithheld = s.TaxWithheld,
                InsuranceDeducted = s.InsuranceDeducted
            }).ToList();

            var monthlyDtos = monthlyIncomes.Select(i => new SharedMonthlyIncomeDto
            {
                Id = i.Id,
                Month = i.Month,
                Year = i.Year,
                OrganizationName = i.OrganizationName,
                TaxIdNumber = i.TaxIdNumber,
                TotalTaxableIncome = i.TotalTaxableIncome,
                InsuranceDeducted = i.InsuranceDeducted,
                TaxAlreadyDeducted = i.TaxAlreadyDeducted,
                PayslipFileUrl = i.PayslipFileUrl
            }).ToList();

            var totalAnnualIncome = monthlyIncomes.Sum(i => i.TotalTaxableIncome);
            var totalTaxWithheld = monthlyIncomes.Sum(i => i.TaxAlreadyDeducted);

            // 2. Người phụ thuộc (Thông tư 87/2026/TT-BTC)
            var dependents = (await _unitOfWork.Repository<Dependent>().FindAsync(d => d.TaxpayerId == booking.UserId && !d.IsDeleted)).ToList();
            var dependentDtos = dependents.Select(d => new SharedDependentDto
            {
                Id = d.Id,
                FullName = d.FullName,
                Relationship = d.Relationship.ToString(),
                CurrentGroup = d.CurrentGroup.ToString(),
                CitizenId = d.CitizenId,
                BirthCertNumber = d.BirthCertNumber,
                Status = d.Status.ToString(),
                IsProfileComplete = d.IsProfileComplete
            }).ToList();

            // 3. Chứng từ giảm trừ Y tế & Giáo dục
            var periods = (await _unitOfWork.Repository<TaxPeriod>().FindAsync(p => p.UserId == booking.UserId && p.TaxYear == (short)taxYear)).ToList();
            var periodIds = periods.Select(p => p.Id).ToList();
            var documents = (await _unitOfWork.Repository<Document>().FindAsync(d => periodIds.Contains(d.PeriodId))).ToList();
            var documentDtos = documents.Select(d => new SharedDocumentDto
            {
                Id = d.Id,
                DocTypeCode = d.DocTypeCode,
                InvoiceSeries = d.InvoiceSeries,
                InvoiceNumber = d.InvoiceNumber,
                InvoiceDate = d.InvoiceDate,
                SellerName = d.SellerName,
                SellerTaxCode = d.SellerTaxCode,
                FileUrl = d.FileUrl
            }).ToList();

            // 4. Dự thảo quyết toán thuế (TaxSettlementDossier)
            var dossiers = (await _unitOfWork.Repository<TaxSettlementDossier>().FindAsync(d => d.TaxpayerId == booking.UserId && d.TaxYear == taxYear)).ToList();
            var activeDossier = dossiers.OrderByDescending(d => d.CutoffDate).FirstOrDefault();
            SharedDraftSettlementDto? draftDto = null;
            if (activeDossier != null)
            {
                draftDto = new SharedDraftSettlementDto
                {
                    DossierId = activeDossier.Id,
                    TaxYear = activeDossier.TaxYear,
                    TotalGrossIncome = activeDossier.TotalGrossIncome,
                    TotalTaxWithheld = activeDossier.TotalTaxWithheld,
                    TotalInsuranceDeduction = activeDossier.TotalInsuranceDeduction,
                    PersonalDeductionAmount = activeDossier.PersonalDeductionAmount
                };
            }

            // 5. Booking Attachments (File bổ trợ ngoài)
            var attachments = (await _unitOfWork.Repository<BookingAttachment>().FindAsync(a => a.BookingId == bookingId)).ToList();
            var attachmentDtos = attachments.Select(a => new SharedAttachmentDto
            {
                Id = a.Id,
                FileName = a.FileName,
                FileUrl = a.FileUrl,
                ContentType = a.ContentType,
                FileSize = a.FileSize
            }).ToList();

            return new SharedTaxDossierResponse
            {
                BookingId = booking.Id,
                BookingCode = booking.BookingCode,
                TaxYear = taxYear,
                TaxpayerFullName = taxpayer?.FullName ?? "Người nộp thuế",
                TaxpayerEmail = taxpayer?.Email ?? string.Empty,
                TaxpayerPhone = taxpayer?.PhoneNumber,
                TaxpayerCitizenId = taxpayer?.CitizenId,
                TaxpayerTaxId = taxpayer?.TaxIdNumber,
                IncomeSources = sourceDtos,
                MonthlyIncomes = monthlyDtos,
                TotalAnnualIncome = totalAnnualIncome,
                TotalTaxWithheld = totalTaxWithheld,
                Dependents = dependentDtos,
                TotalDependentsCount = dependentDtos.Count,
                DeductibleDocuments = documentDtos,
                TotalMedicalEducationExpenses = 0,
                DraftSettlement = draftDto,
                BookingAttachments = attachmentDtos
            };
        }

        // ── 4. CHUYÊN GIA CẬP NHẬT GHI CHÚ TƯ VẤN (UPDATE NOTES) ─────────────────────
        public async Task<ConsultationSessionResponse> UpdateNotesAsync(
            Guid currentUserId, 
            string currentUserRole, 
            Guid bookingId, 
            UpdateConsultationNotesRequest request)
        {
            var booking = await _unitOfWork.Repository<Booking>().GetByIdAsync(bookingId);
            if (booking == null)
            {
                throw new NotFoundException($"Không tìm thấy lịch hẹn với Id = {bookingId}.");
            }

            var slot = await _unitOfWork.Repository<ExpertSlot>().GetByIdAsync(booking.ExpertSlotId);
            var profile = slot != null ? await _unitOfWork.Repository<ExpertProfile>().GetByIdAsync(slot.ExpertProfileId) : null;
            var isExpert = profile != null && profile.UserId == currentUserId;
            var isAdmin = string.Equals(currentUserRole, "admin", StringComparison.OrdinalIgnoreCase);

            if (!isExpert && !isAdmin)
            {
                throw new ForbiddenException("FORBIDDEN", "Chỉ chuyên gia được phân công cho ca này mới có quyền ghi chú biên bản tư vấn.");
            }

            var sessions = await _unitOfWork.Repository<ConsultationSession>().FindAsync(s => s.BookingId == bookingId);
            var session = sessions.FirstOrDefault();
            if (session == null)
            {
                throw new NotFoundException("Chưa khởi tạo phiên tư vấn cho lịch hẹn này. Vui lòng bấm vào phòng trước.");
            }

            var nowUtc = DateTimeOffset.UtcNow;
            var cleanNotes = request.SharedNotes.Trim();
            session.SharedNotes = cleanNotes;
            session.UpdatedAt = nowUtc;

            _unitOfWork.Repository<ConsultationSession>().Update(session);
            await _unitOfWork.SaveChangesAsync();

            if (_realtimeNotifier != null)
            {
                await _realtimeNotifier.NotifyNotesUpdatedAsync(bookingId, cleanNotes);
            }

            return await GetSessionAsync(currentUserId, currentUserRole, bookingId);
        }

        // ── 5. HOÀN THÀNH PHIÊN TƯ VẤN (COMPLETE SESSION - BR-5.3) ───────────────────
        public async Task<ConsultationSessionResponse> CompleteSessionAsync(
            Guid currentUserId, 
            string currentUserRole, 
            Guid bookingId, 
            CompleteConsultationRequest request)
        {
            var booking = await _unitOfWork.Repository<Booking>().GetByIdAsync(bookingId);
            if (booking == null)
            {
                throw new NotFoundException($"Không tìm thấy lịch hẹn với Id = {bookingId}.");
            }

            var slot = await _unitOfWork.Repository<ExpertSlot>().GetByIdAsync(booking.ExpertSlotId);
            var profile = slot != null ? await _unitOfWork.Repository<ExpertProfile>().GetByIdAsync(slot.ExpertProfileId) : null;
            var isExpert = profile != null && profile.UserId == currentUserId;
            var isAdmin = string.Equals(currentUserRole, "admin", StringComparison.OrdinalIgnoreCase);

            if (!isExpert && !isAdmin)
            {
                throw new ForbiddenException("FORBIDDEN", "Chỉ chuyên gia phụ trách mới có quyền kết thúc phiên tư vấn.");
            }

            if (booking.Status != BookingStatus.IN_PROGRESS && booking.Status != BookingStatus.CONFIRMED)
            {
                throw new BadRequestException("INVALID_STATUS", $"Lịch hẹn hiện tại ở trạng thái '{booking.Status}', không thể hoàn tất.");
            }

            // Quy định bắt buộc BR-5.3: Bắt buộc nhập biên bản tóm tắt giải pháp tư vấn
            if (string.IsNullOrWhiteSpace(request.SharedNotes) || request.SharedNotes.Trim().Length < 10)
            {
                throw new BadRequestException("NOTES_REQUIRED", "Bắt buộc nhập biên bản tóm tắt giải pháp tư vấn (tối thiểu 10 ký tự) theo quy định BR-5.3.");
            }

            var sessions = await _unitOfWork.Repository<ConsultationSession>().FindAsync(s => s.BookingId == bookingId);
            var session = sessions.FirstOrDefault();
            var nowUtc = DateTimeOffset.UtcNow;

            if (session == null)
            {
                var roomName = $"TaxKeepVN_{booking.BookingCode.Replace("-", "_")}";
                session = new ConsultationSession
                {
                    Id = Guid.NewGuid(),
                    BookingId = bookingId,
                    RoomName = roomName,
                    RoomUrl = $"https://meet.jit.si/{roomName}",
                    StartedAt = nowUtc.AddMinutes(-30),
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc
                };
                await _unitOfWork.Repository<ConsultationSession>().AddAsync(session);
            }

            var cleanNotes = request.SharedNotes.Trim();
            session.EndedAt = nowUtc;
            session.SharedNotes = cleanNotes;

            var startedAt = session.StartedAt ?? session.UserCheckInAt ?? nowUtc.AddMinutes(-30);
            var actualMinutes = (int)Math.Max(1, Math.Round((nowUtc - startedAt).TotalMinutes));
            session.ActualDurationMinutes = actualMinutes;
            session.UpdatedAt = nowUtc;

            booking.Status = BookingStatus.COMPLETED;
            booking.CompletedAt = nowUtc;
            var ratingWindowDays = await BookingHelper.GetConfigIntOrDefaultAsync(_unitOfWork, BookingHelper.ConfigKeyRatingWindowDays, 7);
            booking.ReviewDeadline = nowUtc.AddDays(ratingWindowDays);
            booking.UpdatedAt = nowUtc;

            // Chuyển khoản tiền Escrow sang trạng thái CoolingDown (Bắt đầu 24 giờ chờ khiếu nại - BR-5)
            var escrows = await _unitOfWork.Repository<EscrowTransaction>().FindAsync(e => e.BookingId == bookingId);
            var escrow = escrows.FirstOrDefault();
            if (escrow != null && escrow.EscrowStatus == EscrowStatus.HeldInEscrow)
            {
                escrow.EscrowStatus = EscrowStatus.CoolingDown;
                escrow.UpdatedAt = nowUtc;
                _unitOfWork.Repository<EscrowTransaction>().Update(escrow);
            }

            if (profile != null)
            {
                profile.CompletedConsultationsCount += 1;
                profile.UpdatedAt = nowUtc;
                _unitOfWork.Repository<ExpertProfile>().Update(profile);
            }

            _unitOfWork.Repository<ConsultationSession>().Update(session);
            _unitOfWork.Repository<Booking>().Update(booking);

            // Gửi thông báo hoàn thành ca cho khách hàng và chuyên gia
            await _unitOfWork.Repository<SystemNotification>().AddAsync(new SystemNotification
            {
                NotificationId = Guid.NewGuid(),
                UserId = booking.UserId,
                Title = "Phiên tư vấn thuế 1-1 đã hoàn tất thành công!",
                Message = $"Phiên tư vấn #{booking.BookingCode} đã kết thúc. Chuyên gia đã lập biên bản tư vấn. Bạn có thể xem kết luận và đánh giá chất lượng phiên tư vấn trong vòng 7 ngày.",
                NotificationType = "CONSULTATION_COMPLETED",
                IsRead = false,
                TargetActionUrl = $"/consultations/{booking.Id}/summary",
                CreatedAt = nowUtc.UtcDateTime
            });

            await _unitOfWork.SaveChangesAsync();

            if (_realtimeNotifier != null)
            {
                await _realtimeNotifier.NotifySessionCompletedAsync(bookingId, actualMinutes, cleanNotes);
            }

            _logger.LogInformation("Phiên tư vấn {BookingCode} đã kết thúc thành công bởi chuyên gia {UserId}. Thời lượng: {Minutes} phút.",
                booking.BookingCode, currentUserId, actualMinutes);

            return await GetSessionAsync(currentUserId, currentUserRole, bookingId);
        }

        // ── 6. BÁO CÁO VẮNG MẶT NO-SHOW (REPORT NO-SHOW) ──────────────────────────────
        public async Task<ConsultationSessionResponse> ReportNoShowAsync(
            Guid currentUserId, 
            string currentUserRole, 
            Guid bookingId, 
            ReportNoShowRequest request)
        {
            var booking = await _unitOfWork.Repository<Booking>().GetByIdAsync(bookingId);
            if (booking == null)
            {
                throw new NotFoundException($"Không tìm thấy lịch hẹn với Id = {bookingId}.");
            }

            var slot = await _unitOfWork.Repository<ExpertSlot>().GetByIdAsync(booking.ExpertSlotId);
            if (slot == null)
            {
                throw new NotFoundException("Không tìm thấy khung giờ của lịch hẹn.");
            }

            var profile = await _unitOfWork.Repository<ExpertProfile>().GetByIdAsync(slot.ExpertProfileId);
            var isClient = booking.UserId == currentUserId;
            var isExpert = profile != null && profile.UserId == currentUserId;
            var isAdmin = string.Equals(currentUserRole, "admin", StringComparison.OrdinalIgnoreCase);

            if (!isClient && !isExpert && !isAdmin)
            {
                throw new ForbiddenException("FORBIDDEN", "Bạn không có quyền báo cáo vắng mặt cho ca tư vấn này.");
            }

            var policy = await BookingHelper.GetPlatformPolicyAsync(_unitOfWork);
            var noShowMinutes = policy.SessionNoShowMinutes;

            var slotStartUtc = BookingHelper.ConvertSlotToUtc(slot.SlotDate, slot.StartTime);
            var nowUtc = DateTimeOffset.UtcNow;
            var noShowThresholdUtc = slotStartUtc.AddMinutes(noShowMinutes);

            if (nowUtc < noShowThresholdUtc)
            {
                var remainingMinutes = (int)Math.Ceiling((noShowThresholdUtc - nowUtc).TotalMinutes);
                throw new BadRequestException("NO_SHOW_EARLY", 
                    $"Chưa đủ thời gian xác định vắng mặt không phép (Quy định là sau {noShowMinutes} phút kể từ giờ bắt đầu). Vui lòng đợi thêm {remainingMinutes} phút.");
            }

            var sessions = await _unitOfWork.Repository<ConsultationSession>().FindAsync(s => s.BookingId == bookingId);
            var session = sessions.FirstOrDefault();
            if (session == null)
            {
                session = new ConsultationSession
                {
                    Id = Guid.NewGuid(),
                    BookingId = bookingId,
                    RoomName = $"TaxKeepVN_{booking.BookingCode.Replace("-", "_")}",
                    RoomUrl = $"https://meet.jit.si/TaxKeepVN_{booking.BookingCode.Replace("-", "_")}",
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc
                };
                await _unitOfWork.Repository<ConsultationSession>().AddAsync(session);
            }

            var escrows = await _unitOfWork.Repository<EscrowTransaction>().FindAsync(e => e.BookingId == bookingId);
            var escrow = escrows.FirstOrDefault();

            // TRƯỜNG HỢP 1: Khách hàng có mặt, Chuyên gia vắng mặt (EXPERT NO-SHOW)
            if (isClient || (isAdmin && session.UserCheckInAt.HasValue && !session.ExpertCheckInAt.HasValue))
            {
                if (session.ExpertCheckInAt.HasValue)
                {
                    throw new BadRequestException("EXPERT_ALREADY_CHECKED_IN", "Chuyên gia đã điểm danh vào phòng, không thể báo vắng mặt.");
                }

                session.NoShowType = "EXPERT_NO_SHOW";
                session.NoShowReportedBy = isClient ? "USER" : "ADMIN";
                session.NoShowReportedAt = nowUtc;
                session.EndedAt = nowUtc;
                session.ActualDurationMinutes = 0;
                session.SharedNotes = "Hủy phiên do Chuyên gia vắng mặt quá 15 phút (No-Show). Hệ thống đã hoàn tiền 100% cho khách hàng.";
                session.UpdatedAt = nowUtc;

                booking.Status = BookingStatus.CANCELLED_BY_EXPERT;
                booking.CancelledBy = "EXPERT_NO_SHOW";
                booking.CancelledAt = nowUtc;
                booking.CancellationReason = request.Reason ?? "Chuyên gia vắng mặt quá 15 phút (Expert No-Show).";
                booking.RefundStatus = "REFUNDED";
                booking.RefundedAt = nowUtc;
                booking.UpdatedAt = nowUtc;

                // Hoàn tiền 100% từ Escrow về cho User
                if (escrow != null)
                {
                    escrow.EscrowStatus = EscrowStatus.Refunded;
                    escrow.RefundAmount = escrow.Amount;
                    escrow.RefundReason = "Chuyên gia vắng mặt không phép quá 15 phút.";
                    escrow.RefundedAt = nowUtc;
                    escrow.UpdatedAt = nowUtc;
                    _unitOfWork.Repository<EscrowTransaction>().Update(escrow);

                    // Cộng tiền hoàn vào ví User
                    var userWallets = await _unitOfWork.Repository<UserWallet>().FindAsync(w => w.UserId == booking.UserId && w.WalletType == "USER");
                    var userWallet = userWallets.FirstOrDefault();
                    if (userWallet == null)
                    {
                        userWallet = new UserWallet
                        {
                            Id = Guid.NewGuid(),
                            UserId = booking.UserId,
                            WalletType = "USER",
                            AvailableBalance = 0m,
                            CreatedAt = nowUtc,
                            UpdatedAt = nowUtc
                        };
                        await _unitOfWork.Repository<UserWallet>().AddAsync(userWallet);
                    }
                    var beforeBal = userWallet.AvailableBalance;
                    userWallet.AvailableBalance += escrow.Amount;
                    userWallet.UpdatedAt = nowUtc;
                    _unitOfWork.Repository<UserWallet>().Update(userWallet);

                    await _unitOfWork.Repository<WalletTransaction>().AddAsync(new WalletTransaction
                    {
                        Id = Guid.NewGuid(),
                        WalletId = userWallet.Id,
                        UserId = booking.UserId,
                        Amount = escrow.Amount,
                        BalanceBefore = beforeBal,
                        BalanceAfter = userWallet.AvailableBalance,
                        TransactionType = "REFUND",
                        ReferenceId = booking.Id,
                        Description = $"Hoàn tiền ca tư vấn #{booking.BookingCode} (Chuyên gia vắng mặt)",
                        CreatedAt = nowUtc
                    });
                }

                // Giải phóng slot
                slot.IsBooked = false;
                slot.HoldExpiresAt = null;
                slot.HoldUserId = null;
                slot.UpdatedAt = nowUtc;
                _unitOfWork.Repository<ExpertSlot>().Update(slot);

                // Gửi thông báo xin lỗi User
                await _unitOfWork.Repository<SystemNotification>().AddAsync(new SystemNotification
                {
                    NotificationId = Guid.NewGuid(),
                    UserId = booking.UserId,
                    Title = "Ca tư vấn đã bị hủy do Chuyên gia vắng mặt – Hoàn tiền 100%",
                    Message = $"Chuyên gia đã không có mặt trong phòng tư vấn #{booking.BookingCode} quá 15 phút. Nền tảng xin lỗi vì sự bất tiện này. Toàn bộ {booking.Fee:N0} VNĐ đã được hoàn trả lại cho bạn.",
                    NotificationType = "EXPERT_NO_SHOW_REFUNDED",
                    IsRead = false,
                    TargetActionUrl = "/experts",
                    CreatedAt = nowUtc.UtcDateTime
                });
            }
            // TRƯỜNG HỢP 2: Chuyên gia có mặt, Khách hàng vắng mặt (USER NO-SHOW)
            else if (isExpert || (isAdmin && session.ExpertCheckInAt.HasValue && !session.UserCheckInAt.HasValue))
            {
                if (session.UserCheckInAt.HasValue)
                {
                    throw new BadRequestException("USER_ALREADY_CHECKED_IN", "Khách hàng đã điểm danh vào phòng, không thể báo vắng mặt.");
                }

                session.NoShowType = "USER_NO_SHOW";
                session.NoShowReportedBy = isExpert ? "EXPERT" : "ADMIN";
                session.NoShowReportedAt = nowUtc;
                session.EndedAt = nowUtc;
                session.ActualDurationMinutes = 15;
                session.SharedNotes = "Đóng ca do Khách hàng vắng mặt quá 15 phút (No-Show). Bảo vệ thù lao làm việc cho chuyên gia.";
                session.UpdatedAt = nowUtc;

                booking.Status = BookingStatus.COMPLETED;
                booking.CompletedAt = nowUtc;
                var ratingDaysNoShow = await BookingHelper.GetConfigIntOrDefaultAsync(_unitOfWork, BookingHelper.ConfigKeyRatingWindowDays, 7);
                booking.ReviewDeadline = nowUtc.AddDays(ratingDaysNoShow);
                booking.UpdatedAt = nowUtc;

                // Vẫn giữ tiền Escrow để giải ngân cho chuyên gia
                if (escrow != null && escrow.EscrowStatus == EscrowStatus.HeldInEscrow)
                {
                    escrow.EscrowStatus = EscrowStatus.CoolingDown;
                    escrow.UpdatedAt = nowUtc;
                    _unitOfWork.Repository<EscrowTransaction>().Update(escrow);
                }

                // Gửi thông báo đến User
                await _unitOfWork.Repository<SystemNotification>().AddAsync(new SystemNotification
                {
                    NotificationId = Guid.NewGuid(),
                    UserId = booking.UserId,
                    Title = "Lịch hẹn tư vấn đã đóng do bạn vắng mặt quá 15 phút",
                    Message = $"Bạn đã không vào phòng tư vấn #{booking.BookingCode} quá 15 phút sau giờ hẹn. Ca tư vấn đã được đóng theo chính sách bảo vệ thời gian của chuyên gia.",
                    NotificationType = "USER_NO_SHOW_CLOSED",
                    IsRead = false,
                    TargetActionUrl = $"/bookings/{booking.Id}",
                    CreatedAt = nowUtc.UtcDateTime
                });
            }

            _unitOfWork.Repository<ConsultationSession>().Update(session);
            _unitOfWork.Repository<Booking>().Update(booking);
            await _unitOfWork.SaveChangesAsync();

            return await GetSessionAsync(currentUserId, currentUserRole, bookingId);
        }

        // ── 7. NHẮC NHỞ LỊCH HẸN MEETING SẮP TỚI (UPCOMING MEETING REMINDER) ──────────────
        public async Task<UpcomingMeetingReminderDto> GetUpcomingMeetingReminderAsync(Guid currentUserId)
        {
            var nowUtc = DateTimeOffset.UtcNow;

            int sessionOpenBeforeMinutes = 10;
            try
            {
                var policy = await BookingHelper.GetPlatformPolicyAsync(_unitOfWork);
                sessionOpenBeforeMinutes = policy.SessionOpenBeforeMinutes;
            }
            catch
            {
                // Fallback mặc định 10 phút
            }

            // Lấy tất cả ca CONFIRMED hoặc IN_PROGRESS mà user là khách hàng
            var clientBookings = await _unitOfWork.Repository<Booking>().FindAsync(b =>
                b.UserId == currentUserId &&
                (b.Status == BookingStatus.CONFIRMED || b.Status == BookingStatus.IN_PROGRESS));

            // Tìm hồ sơ ExpertProfile của user (nếu user này cũng là chuyên gia)
            var expertProfiles = await _unitOfWork.Repository<ExpertProfile>().FindAsync(ep => ep.UserId == currentUserId);
            var expertProfile = expertProfiles.FirstOrDefault();

            var expertBookings = new List<Booking>();
            if (expertProfile != null)
            {
                var expertSlots = await _unitOfWork.Repository<ExpertSlot>().FindAsync(s => s.ExpertProfileId == expertProfile.Id);
                var slotIds = expertSlots.Select(s => s.Id).ToHashSet();

                var allConfirmed = await _unitOfWork.Repository<Booking>().FindAsync(b =>
                    slotIds.Contains(b.ExpertSlotId) &&
                    (b.Status == BookingStatus.CONFIRMED || b.Status == BookingStatus.IN_PROGRESS));

                expertBookings.AddRange(allConfirmed);
            }

            var allUserBookings = clientBookings.Concat(expertBookings).DistinctBy(b => b.Id).ToList();
            if (!allUserBookings.Any())
            {
                return new UpcomingMeetingReminderDto
                {
                    HasUpcomingMeeting = false,
                    ReminderMessage = "Bạn hiện không có lịch hẹn tư vấn trực tuyến nào sắp diễn ra."
                };
            }

            // Tải thông tin slot và tìm ca gần nhất
            Booking? nearestBooking = null;
            ExpertSlot? nearestSlot = null;
            DateTimeOffset nearestSlotStartUtc = DateTimeOffset.MaxValue;
            DateTimeOffset nearestSlotEndUtc = DateTimeOffset.MaxValue;

            foreach (var b in allUserBookings)
            {
                var s = await _unitOfWork.Repository<ExpertSlot>().GetByIdAsync(b.ExpertSlotId);
                if (s == null) continue;

                var startUtc = BookingHelper.ConvertSlotToUtc(s.SlotDate, s.StartTime);
                var endUtc = BookingHelper.ConvertSlotToUtc(s.SlotDate, s.EndTime);

                // Ca đã kết thúc -> bỏ qua
                if (nowUtc > endUtc) continue;

                if (startUtc < nearestSlotStartUtc)
                {
                    nearestSlotStartUtc = startUtc;
                    nearestSlotEndUtc = endUtc;
                    nearestBooking = b;
                    nearestSlot = s;
                }
            }

            if (nearestBooking == null || nearestSlot == null)
            {
                return new UpcomingMeetingReminderDto
                {
                    HasUpcomingMeeting = false,
                    ReminderMessage = "Bạn hiện không có lịch hẹn tư vấn trực tuyến nào sắp diễn ra."
                };
            }

            bool isClient = nearestBooking.UserId == currentUserId;
            string partnerRole = isClient ? "EXPERT" : "TAXPAYER";
            string partnerName = "Đối tác";

            if (isClient)
            {
                var ep = await _unitOfWork.Repository<ExpertProfile>().GetByIdAsync(nearestSlot.ExpertProfileId);
                if (ep != null)
                {
                    var expUser = await _unitOfWork.Repository<User>().GetByIdAsync(ep.UserId);
                    partnerName = expUser?.FullName ?? ep.JobTitle;
                }
            }
            else
            {
                var taxUser = await _unitOfWork.Repository<User>().GetByIdAsync(nearestBooking.UserId);
                partnerName = taxUser?.FullName ?? "Khách hàng";
            }

            var diff = nearestSlotStartUtc - nowUtc;
            int minutesRemaining = (int)Math.Round(diff.TotalMinutes);
            bool isRoomOpen = minutesRemaining <= sessionOpenBeforeMinutes && nowUtc <= nearestSlotEndUtc;
            bool canJoinRoom = isRoomOpen;

            // Xác định UrgencyLevel & Thông điệp
            string urgencyLevel;
            string reminderMessage;

            if (nearestBooking.Status == BookingStatus.IN_PROGRESS || (minutesRemaining <= 0 && nowUtc <= nearestSlotEndUtc))
            {
                urgencyLevel = "IN_PROGRESS";
                reminderMessage = $"Ca tư vấn #{nearestBooking.BookingCode} với {partnerName} đang diễn ra! Nhấn vào để tham gia ngay.";
            }
            else if (isRoomOpen)
            {
                urgencyLevel = "OPENED";
                reminderMessage = $"Phòng tư vấn #{nearestBooking.BookingCode} với {partnerName} đã mở trước {sessionOpenBeforeMinutes} phút. Bạn có thể vào phòng ngay bây giờ.";
            }
            else if (minutesRemaining <= 60)
            {
                urgencyLevel = "SOON";
                reminderMessage = $"Ca tư vấn #{nearestBooking.BookingCode} với {partnerName} sẽ bắt đầu sau {minutesRemaining} phút nữa ({nearestSlot.StartTime:HH\\:mm}).";
            }
            else
            {
                urgencyLevel = "NORMAL";
                var hoursRemaining = (int)Math.Ceiling(minutesRemaining / 60.0);
                reminderMessage = $"Lịch hẹn tư vấn #{nearestBooking.BookingCode} với {partnerName} còn {hoursRemaining} giờ nữa sẽ diễn ra ({nearestSlot.StartTime:HH\\:mm} ngày {nearestSlot.SlotDate:dd/MM/yyyy}).";
            }

            string roomUrl = $"https://meet.jit.si/TaxKeepVN_{nearestBooking.BookingCode.Replace("-", "_")}";

            return new UpcomingMeetingReminderDto
            {
                HasUpcomingMeeting = true,
                BookingId = nearestBooking.Id,
                BookingCode = nearestBooking.BookingCode,
                TopicTitle = nearestBooking.TopicTitle,
                PartnerName = partnerName,
                PartnerRole = partnerRole,
                SlotDate = nearestSlot.SlotDate,
                StartTime = nearestSlot.StartTime,
                EndTime = nearestSlot.EndTime,
                MinutesRemaining = minutesRemaining,
                IsRoomOpen = isRoomOpen,
                CanJoinRoom = canJoinRoom,
                RoomUrl = isRoomOpen ? roomUrl : null,
                UrgencyLevel = urgencyLevel,
                ReminderMessage = reminderMessage
            };
        }
    }
}
