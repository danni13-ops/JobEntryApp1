namespace JobEntryApp.Infrastructure
{
    public static class JobSchedules
    {
        public const string Standard = "Standard";
        public const string Rush = "Rush";
        public const string Critical = "Critical";

        public static IReadOnlyList<string> All { get; } = [Standard, Rush, Critical];

        public static string Normalize(string? value)
        {
            if (string.Equals(value, Rush, StringComparison.OrdinalIgnoreCase))
            {
                return Rush;
            }

            if (string.Equals(value, Critical, StringComparison.OrdinalIgnoreCase))
            {
                return Critical;
            }

            return Standard;
        }

        public static int GetBusinessDaysBack(string? schedule)
            => Normalize(schedule) switch
            {
                Critical => 5,
                Rush => 12,
                _ => 15
            };

        public static bool IsExpedited(string? schedule)
            => !string.Equals(Normalize(schedule), Standard, StringComparison.Ordinal);
    }
}
