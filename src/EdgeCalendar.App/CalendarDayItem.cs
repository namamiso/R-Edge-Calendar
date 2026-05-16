using System;
using System.Collections.Generic;

namespace EdgeCalendar.App
{
    public sealed class CalendarDayItem
    {
        public DateTime Date { get; init; }
        public string DayText { get; init; } = string.Empty;
        public bool IsCurrentMonth { get; init; }
        public bool IsToday { get; init; }
        public bool IsSelected { get; init; }
        public bool IsRedDay { get; init; }
        public string? HolidayName { get; init; }
        public IReadOnlyList<CalendarEventPreviewItem> Events { get; init; } = Array.Empty<CalendarEventPreviewItem>();
    }
}
