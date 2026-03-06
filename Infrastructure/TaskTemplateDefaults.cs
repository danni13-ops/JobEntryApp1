namespace JobEntryApp.Infrastructure
{
    public sealed record TaskTemplateDefault(
        int TaskNumber,
        string TaskName,
        string Stage,
        string DefaultAssignee,
        int DaysOffset,
        string? Dependencies);

    public static class TaskTemplateDefaults
    {
        public static IReadOnlyList<TaskTemplateDefault> All { get; } =
        [
            new(1, "Job Intake", "Intake", "CSR", 0, null),
            new(2, "Data Received", "Data", "DP", 0, "1"),
            new(3, "Data Review", "Data", "DP", 1, "2"),
            new(4, "Counts Approved", "Data", "CSR", 2, "3"),
            new(5, "Art Proof Ready", "Prepress", "CSR", 3, "4"),
            new(6, "Signoffs Due", "Prepress", "CSR", 5, "5"),
            new(7, "Signoffs Approved", "Prepress", "CSR", 7, "6"),
            new(8, "Merge/Purge", "Data", "DP", 8, "4"),
            new(9, "Print Files Ready", "Prepress", "DP", 9, "8"),
            new(10, "Print Production Start", "Production", "CSR", 10, "9"),
            new(11, "Print Quality Check", "Production", "CSR", 11, "10"),
            new(12, "Lettershop Setup", "Production", "CSR", 12, "11"),
            new(13, "Personalization", "Production", "DP", 13, "12"),
            new(14, "Insert Setup", "Production", "CSR", 14, "12"),
            new(15, "Match Setup", "Production", "DP", 15, "13"),
            new(16, "Addressing", "Production", "DP", 16, "15"),
            new(17, "Postal Prep", "Postal", "CSR", 17, "16"),
            new(18, "Postal Docs Complete", "Postal", "CSR", 18, "17"),
            new(19, "Production Out", "Production", "CSR", 19, "18"),
            new(20, "Final QC", "Production", "CSR", 20, "19"),
            new(21, "Truck/Style Confirmed", "Postal", "CSR", 21, "20"),
            new(22, "Mail Date", "Postal", "CSR", 22, "21")
        ];
    }
}
