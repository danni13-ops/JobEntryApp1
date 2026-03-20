using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;
using JobEntryApp.Infrastructure;

namespace JobEntryApp.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly IConfiguration _config;

        public IndexModel(ILogger<IndexModel> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;
        }

        public List<TaskSummary> RecentTasks { get; set; } = new();
        public bool ShowDueThisWeek { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? CsrFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? TaskNameFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ViewMode { get; set; }

        public List<string> Csrs { get; set; } = new();

        public IActionResult OnGet(bool dueThisWeek = false)
        {
            ShowDueThisWeek = dueThisWeek;
            try
            {
                LoadCsrs();
                LoadRecentTasks();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Dashboard task data could not be loaded.");
                RecentTasks = new List<TaskSummary>();
                Csrs = new List<string>();
            }

            if (string.Equals(ViewMode, "report", StringComparison.OrdinalIgnoreCase))
            {
                var csv = GenerateCsv(RecentTasks);
                return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "tasks_report.csv");
            }

            return Page();
        }

        private void LoadCsrs()
        {
            var cs = _config.GetConnectionString("JobEntryDb")
                ?? throw new InvalidOperationException("Missing connection string.");

            using var conn = new SqlConnection(cs);
            conn.Open();

            using SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT DISTINCT AssignedTo
                FROM dbo.Tasks
                WHERE AssignedTo IS NOT NULL
                ORDER BY AssignedTo;";

            using SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var val = reader.IsDBNull(0) ? null : reader.GetString(0);
                if (!string.IsNullOrWhiteSpace(val)) Csrs.Add(val);
            }
        }

        private static string SanitizeText(string? input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            var cleaned = Regex.Replace(input, "[\u0000-\u001F\u007F]+", " ");
            cleaned = Regex.Replace(cleaned, "\\s+", " ").Trim();
            return cleaned;
        }

        private void LoadRecentTasks()
        {
            var cs = _config.GetConnectionString("JobEntryDb")
                ?? throw new InvalidOperationException("Missing connection string.");

            using var conn = new SqlConnection(cs);
            conn.Open();

            using SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT TOP(200) TaskId, JobNumber, TaskNumber, TaskName, AssignedTo, DueDate, Status
                FROM dbo.Tasks
                WHERE 1=1";

            if (ShowDueThisWeek)
            {
                cmd.CommandText += " AND DueDate BETWEEN @Today AND @EndOfWeek";
                cmd.Parameters.AddWithValue("@Today", DateTime.Today);
                cmd.Parameters.AddWithValue("@EndOfWeek", DateTime.Today.AddDays(7));
            }

            if (!string.IsNullOrWhiteSpace(CsrFilter))
            {
                cmd.CommandText += " AND AssignedTo = @Csr";
                cmd.Parameters.AddWithValue("@Csr", CsrFilter);
            }

            if (!string.IsNullOrWhiteSpace(TaskNameFilter))
            {
                cmd.CommandText += " AND TaskName LIKE @TaskName";
                cmd.Parameters.AddWithValue("@TaskName", $"%{TaskNameFilter}%");
            }

            cmd.CommandText += " ORDER BY TaskNumber ASC, COALESCE(DueDate, '9999-12-31') ASC;";

            using SqlDataReader reader = cmd.ExecuteReader();
            var items = new List<TaskSummary>();
            while (reader.Read())
            {
                items.Add(new TaskSummary
                {
                    TaskId = reader.GetGuid(0),
                    JobNumber = SqlReaderValue.ReadInt32(reader, 1),
                    TaskNumber = SqlReaderValue.ReadInt32(reader, 2),
                    TaskName = SanitizeText(reader.IsDBNull(3) ? string.Empty : reader.GetString(3)),
                    AssignedTo = SanitizeText(reader.IsDBNull(4) ? string.Empty : reader.GetString(4)),
                    DueDate = reader.IsDBNull(5) ? (DateTime?)null : reader.GetDateTime(5),
                    Status = SanitizeText(reader.IsDBNull(6) ? string.Empty : reader.GetString(6))
                });
            }

            IEnumerable<TaskSummary> filtered = items.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(CsrFilter))
                filtered = filtered.Where(i => string.Equals(i.AssignedTo, CsrFilter, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(TaskNameFilter))
                filtered = filtered.Where(i => i.TaskName.IndexOf(TaskNameFilter, StringComparison.OrdinalIgnoreCase) >= 0);

            RecentTasks = filtered.ToList();
        }

        private static string Csv(string? value)
            => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";

        private string GenerateCsv(IEnumerable<TaskSummary> items)
        {
            using var sw = new StringWriter();
            sw.WriteLine("TaskId,JobNumber,TaskNumber,TaskName,AssignedTo,DueDate,Status");
            foreach (TaskSummary t in items)
            {
                var due = t.DueDate?.ToString("yyyy-MM-dd") ?? string.Empty;
                sw.WriteLine($"{t.TaskId},{t.JobNumber},{t.TaskNumber},{Csv(t.TaskName)},{Csv(t.AssignedTo)},{Csv(due)},{Csv(t.Status)}");
            }

            return sw.ToString();
        }

        public class TaskSummary
        {
            public Guid TaskId { get; set; }
            public int JobNumber { get; set; }
            public int TaskNumber { get; set; }
            public string TaskName { get; set; } = string.Empty;
            public string AssignedTo { get; set; } = string.Empty;
            public DateTime? DueDate { get; set; }
            public string Status { get; set; } = string.Empty;
            public bool IsOverdue => DueDate.HasValue && DueDate.Value.Date < DateTime.Today;
        }
    }
}
