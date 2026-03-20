namespace JobEntryApp.Infrastructure
{
    public sealed class CalendarEventInput
    {
        public DateTime Date { get; init; }
        public string Title { get; init; } = string.Empty;
        public string? Subtitle { get; init; }
        public string? Url { get; init; }
        public string CssClass { get; init; } = "calendar-event-default";
        public bool IsCompleted { get; init; }
        public bool IsMilestone { get; init; }
        public int SortOrder { get; init; }
    }
}
