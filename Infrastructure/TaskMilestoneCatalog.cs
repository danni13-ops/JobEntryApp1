namespace JobEntryApp.Infrastructure
{
    public static class TaskMilestoneCatalog
    {
        private static readonly IReadOnlyList<MilestoneDefinition> Definitions =
        [
            new("Kickoff", "calendar-event-kickoff", name => Contains(name, "open job jacket")),
            new("Data Received", "calendar-event-data", name =>
                Contains(name, "data and instructions received") || Contains(name, "data received")),
            new("Counts Received", "calendar-event-counts", IsCountsReceivedTask),
            new("PDF Approved", "calendar-event-approval", name => Contains(name, "signoffs received")),
            new("Laser Approved", "calendar-event-approval", name => Contains(name, "laser signoffs approved")),
            new("Production", "calendar-event-production", name => Contains(name, "production counts")),
            new("Mail Date", "calendar-event-mail", name => string.Equals(name.Trim(), "Mail Date", StringComparison.OrdinalIgnoreCase)),
            new("Postal Receipt", "calendar-event-postal", name => Contains(name, "postal receipt")),
            new("Invoice", "calendar-event-postal", name => Contains(name, "invoice job"))
        ];

        public static bool TryGetMilestone(string? taskName, out MilestoneDisplay milestone)
        {
            milestone = default;
            if (string.IsNullOrWhiteSpace(taskName))
            {
                return false;
            }

            foreach (var definition in Definitions)
            {
                if (definition.Matches(taskName))
                {
                    milestone = new MilestoneDisplay(definition.Label, definition.CssClass);
                    return true;
                }
            }

            return false;
        }

        public static bool IsCountsReceivedTask(string? taskName)
        {
            if (string.IsNullOrWhiteSpace(taskName))
            {
                return false;
            }

            return Contains(taskName, "approval on counts")
                || (Contains(taskName, "counts")
                    && Contains(taskName, "receive")
                    && !Contains(taskName, "production"));
        }

        public static bool IsDataReceivedTask(string? taskName)
        {
            if (string.IsNullOrWhiteSpace(taskName))
            {
                return false;
            }

            return Contains(taskName, "data and instructions received")
                || Contains(taskName, "start date for project")
                || string.Equals(taskName.Trim(), "Data Received", StringComparison.OrdinalIgnoreCase);
        }

        private static bool Contains(string source, string value)
            => source.Contains(value, StringComparison.OrdinalIgnoreCase);

        public readonly record struct MilestoneDisplay(string Label, string CssClass);

        private sealed record MilestoneDefinition(string Label, string CssClass, Func<string, bool> Matches);
    }
}
