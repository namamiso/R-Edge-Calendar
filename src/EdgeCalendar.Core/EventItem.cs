using System;

namespace EdgeCalendar.Core
{
    public sealed class EventItem
    {
        public long Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime StartLocal { get; set; }
        public DateTime EndLocal { get; set; }
        public bool IsAllDay { get; set; }
        public string? Location { get; set; }
        public string? Notes { get; set; }
        public DateTime UpdatedAtLocal { get; set; }
        public string Source { get; set; } = "local";
        public string? ExternalId { get; set; }
        public string? CalendarId { get; set; }
        public bool IsReadOnly { get; set; }
        public string? ETag { get; set; }

        public EventItem Clone()
        {
            return new EventItem
            {
                Id = Id,
                Title = Title,
                StartLocal = StartLocal,
                EndLocal = EndLocal,
                IsAllDay = IsAllDay,
                Location = Location,
                Notes = Notes,
                UpdatedAtLocal = UpdatedAtLocal,
                Source = Source,
                ExternalId = ExternalId,
                CalendarId = CalendarId,
                IsReadOnly = IsReadOnly,
                ETag = ETag
            };
        }
    }
}
