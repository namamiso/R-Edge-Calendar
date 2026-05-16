using EdgeCalendar.Core;

namespace EdgeCalendar.App
{
    public sealed class CalendarEventPreviewItem
    {
        public CalendarEventPreviewItem(EventItem item, string timeLabel, string colorHex)
        {
            Item = item;
            Title = item.Title;
            TimeLabel = timeLabel;
            DisplayText = item.Title;
            ColorHex = colorHex;
            IsReadOnly = item.IsReadOnly;
        }

        public EventItem Item { get; }
        public string Title { get; }
        public string TimeLabel { get; }
        public string DisplayText { get; }
        public string ColorHex { get; }
        public bool IsReadOnly { get; }
    }
}
