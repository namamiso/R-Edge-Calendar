namespace EdgeCalendar.Core
{
    public sealed class CalendarInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string? BackgroundColor { get; set; }
        public bool IsSelected { get; set; }
    }
}
