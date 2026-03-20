using JobEntryApp.Models;

namespace JobEntryApp.Infrastructure
{
    public static class CalendarBuilder
    {
        public static IReadOnlyList<string> DayHeaders { get; } = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

        public static List<CalendarMonthViewModel> BuildMonths(
            IEnumerable<CalendarEventInput> events,
            DateTime? rangeStart = null,
            DateTime? rangeEnd = null,
            bool compact = false,
            int maxItemsPerDay = 3,
            int maxMonths = 3)
        {
            var eventList = events
                .OrderBy(e => e.Date)
                .ThenBy(e => e.SortOrder)
                .ThenBy(e => e.Title)
                .ToList();

            var firstDate = rangeStart?.Date
                ?? eventList.Select(e => e.Date.Date).DefaultIfEmpty(DateTime.Today).Min();
            var lastDate = rangeEnd?.Date
                ?? eventList.Select(e => e.Date.Date).DefaultIfEmpty(firstDate).Max();

            var monthStart = new DateTime(firstDate.Year, firstDate.Month, 1);
            var monthEnd = new DateTime(lastDate.Year, lastDate.Month, 1);
            var months = new List<DateTime>();

            while (monthStart <= monthEnd)
            {
                months.Add(monthStart);
                monthStart = monthStart.AddMonths(1);
            }

            if (months.Count == 0)
            {
                months.Add(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1));
            }

            if (months.Count > maxMonths)
            {
                months = months.Take(maxMonths).ToList();
            }

            return months
                .Select(month => BuildMonth(month, eventList, compact, maxItemsPerDay))
                .ToList();
        }

        public static CalendarMonthViewModel BuildMonth(
            DateTime monthStart,
            IEnumerable<CalendarEventInput> events,
            bool compact = false,
            int maxItemsPerDay = 4)
        {
            var normalizedMonth = new DateTime(monthStart.Year, monthStart.Month, 1);
            var monthEnd = normalizedMonth.AddMonths(1).AddDays(-1);
            var gridStart = normalizedMonth.AddDays(-(int)normalizedMonth.DayOfWeek);
            var gridEnd = monthEnd.AddDays(6 - (int)monthEnd.DayOfWeek);

            var eventLookup = events
                .GroupBy(e => e.Date.Date)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(e => e.SortOrder).ThenBy(e => e.Title).ToList());

            var weeks = new List<CalendarWeekViewModel>();
            var cursor = gridStart;
            while (cursor <= gridEnd)
            {
                var days = new List<CalendarDayViewModel>();
                for (var i = 0; i < 7; i++)
                {
                    var dayEvents = eventLookup.TryGetValue(cursor.Date, out var dayEntries)
                        ? dayEntries
                        : new List<CalendarEventInput>();

                    var visibleEvents = dayEvents
                        .Take(maxItemsPerDay)
                        .Select(e => new CalendarEventViewModel
                        {
                            Title = e.Title,
                            Subtitle = e.Subtitle,
                            Url = e.Url,
                            CssClass = e.CssClass,
                            IsCompleted = e.IsCompleted,
                            IsMilestone = e.IsMilestone
                        })
                        .ToList();

                    days.Add(new CalendarDayViewModel
                    {
                        Date = cursor,
                        IsCurrentMonth = cursor.Month == normalizedMonth.Month,
                        IsToday = cursor.Date == DateTime.Today,
                        Events = visibleEvents,
                        OverflowCount = Math.Max(0, dayEvents.Count - visibleEvents.Count)
                    });

                    cursor = cursor.AddDays(1);
                }

                weeks.Add(new CalendarWeekViewModel { Days = days });
            }

            return new CalendarMonthViewModel
            {
                MonthStart = normalizedMonth,
                Title = normalizedMonth.ToString("MMMM yyyy"),
                Compact = compact,
                Weeks = weeks
            };
        }
    }
}
