using System;
using System.Collections.Generic;

namespace TaxKeepVN.Application.DTOs
{
    public class AgeReminderItemDto
    {
        public Guid DependentId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string BirthDate { get; set; } = string.Empty;
        public string CurrentGroup { get; set; } = string.Empty;
        public string RecommendedGroup { get; set; } = string.Empty;
        public string TransitionStatus { get; set; } = string.Empty;
        public string Turning18Date { get; set; } = string.Empty;
        public int DaysRemaining { get; set; }
        public string Message { get; set; } = string.Empty;
        public string RequiredAction { get; set; } = string.Empty;
    }

    public class DependentAgeReminderResponseDto
    {
        public int TotalReminders { get; set; }
        public List<AgeReminderItemDto> Reminders { get; set; } = new List<AgeReminderItemDto>();
    }
}
