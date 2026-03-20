namespace JobEntryApp.Models
{
    public sealed class CalendarMonthViewModel
    {
        public DateTime MonthStart { get; init; }
        public string Title { get; init; } = string.Empty;
        public bool Compact { get; init; }
        public List<CalendarWeekViewModel> Weeks { get; init; } = new();
    }

    public sealed class CalendarWeekViewModel
    {
        public List<CalendarDayViewModel> Days { get; init; } = new();
    }

    public sealed class CalendarDayViewModel
    {
        public DateTime Date { get; init; }
        public bool IsCurrentMonth { get; init; }
        public bool IsToday { get; init; }
        public List<CalendarEventViewModel> Events { get; init; } = new();
        public int OverflowCount { get; init; }
    }

    public sealed class CalendarEventViewModel
    {
        public string Title { get; init; } = string.Empty;
        public string? Subtitle { get; init; }
        public string? Url { get; init; }
        public string CssClass { get; init; } = "calendar-event-default";
        public bool IsCompleted { get; init; }
        public bool IsMilestone { get; init; }
    }
}
