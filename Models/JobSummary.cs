namespace JobEntryApp.Models
{
    public class JobSummary
    {
        public int JobNumber { get; set; }
        public string JobName { get; set; } = string.Empty;
        public string Customer { get; set; } = string.Empty;
        public string? Csr { get; set; }
        public string? DataProcessing { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? StartDate { get; set; }
        public DateTime? MailDate { get; set; }
        public int PendingTaskCount { get; set; }

        public bool IsMailingSoon =>
            MailDate.HasValue &&
            MailDate.Value.Date >= DateTime.Today &&
            MailDate.Value.Date <= DateTime.Today.AddDays(7);
    }
}
