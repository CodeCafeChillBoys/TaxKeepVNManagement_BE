using System;
using System.Threading.Tasks;

namespace TaxKeepVN.Application.Service.Interfaces
{
    /// <summary>
    /// Giao diện phát thông báo thời gian thực cho phòng tư vấn trực tuyến (SignalR Adapter)
    /// </summary>
    public interface IConsultationRealtimeNotifier
    {
        Task NotifyParticipantJoinedAsync(Guid bookingId, Guid userId, string role, bool bothPresent);
        Task NotifySessionStartedAsync(Guid bookingId, DateTimeOffset startedAt);
        Task NotifyNotesUpdatedAsync(Guid bookingId, string notes);
        Task NotifySessionCompletedAsync(Guid bookingId, int actualDurationMinutes, string sharedNotes);
        Task NotifyCountdownWarningAsync(Guid bookingId, int remainingMinutes);
    }
}
