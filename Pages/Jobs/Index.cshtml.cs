using JobEntryApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.Data;

namespace JobEntryApp.Pages.Jobs
{
    public class IndexModel : PageModel
    {
        private readonly IConfiguration _config;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(IConfiguration config, ILogger<IndexModel> logger)
        {
            _config = config;
            _logger = logger;
        }

        public List<JobListItem> Jobs { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        [BindProperty(SupportsGet = true)]
        public string StatusFilter { get; set; } = "All";

        [BindProperty(SupportsGet = true)]
        public bool ExportCsv { get; set; }

        [BindProperty]
        public List<int> SelectedJobs { get; set; } = new();

        public IActionResult OnGet()
        {
            LoadJobs(Jobs);

            if (ExportCsv)
            {
                var csv = BuildCsv(Jobs);
                return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "jobs.csv");
            }

            return Page();
        }

        public IActionResult OnPostDelete()
        {
            var validJobNumbers = SelectedJobs
                .Where(x => x > 0)
                .Distinct()
                .ToArray();

            if (validJobNumbers.Length == 0)
                return RedirectToPage(new { Search, StatusFilter });

            var cs = _config.GetConnectionString("JobEntryDb")
                ?? throw new InvalidOperationException("Missing connection string 'JobEntryDb'.");

            using var conn = new SqlConnection(cs);
            conn.Open();

            using var tx = conn.BeginTransaction();
            try
            {
                var tasksDeleted = DeleteByJobNumbers(conn, tx, "dbo.Tasks", validJobNumbers);
                var mailDeleted = DeleteByJobNumbers(conn, tx, "dbo.MailChart", validJobNumbers);
                var jobsDeleted = DeleteByJobNumbers(conn, tx, "dbo.Jobs", validJobNumbers);

                tx.Commit();
                _logger.LogInformation(
                    "Bulk delete completed. Jobs selected: {SelectedCount}, Jobs deleted: {JobsDeleted}, Tasks deleted: {TasksDeleted}, Mail rows deleted: {MailDeleted}",
                    validJobNumbers.Length,
                    jobsDeleted,
                    tasksDeleted,
                    mailDeleted);
            }
            catch (Exception ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "Bulk delete failed.");
            }

            return RedirectToPage(new { Search, StatusFilter });
        }

        private static int DeleteByJobNumbers(SqlConnection conn, SqlTransaction tx, string tableName, IReadOnlyList<int> jobNumbers)
        {
            if (jobNumbers.Count == 0)
            {
                return 0;
            }

            string safeTable = tableName switch
            {
                "dbo.Tasks" => "dbo.Tasks",
                "dbo.MailChart" => "dbo.MailChart",
                "dbo.Jobs" => "dbo.Jobs",
                _ => throw new InvalidOperationException("Unsupported table for bulk delete.")
            };

            const int chunkSize = 500; // Stays safely below SQL parameter limits.
            var totalDeleted = 0;

            for (var offset = 0; offset < jobNumbers.Count; offset += chunkSize)
            {
                var count = Math.Min(chunkSize, jobNumbers.Count - offset);
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;

                var parameterNames = new List<string>(count);
                for (var i = 0; i < count; i++)
                {
                    var parameterName = $"@job{offset + i}";
                    parameterNames.Add(parameterName);
                    cmd.Parameters.Add(parameterName, SqlDbType.Int).Value = jobNumbers[offset + i];
                }

                cmd.CommandText = $"DELETE FROM {safeTable} WHERE JobNumber IN ({string.Join(", ", parameterNames)});";
                totalDeleted += cmd.ExecuteNonQuery();
            }

            return totalDeleted;
        }

        private void LoadJobs(List<JobListItem> list1)
        {
            var cs = _config.GetConnectionString("JobEntryDb");
            if (string.IsNullOrWhiteSpace(cs))
            {
                Jobs = new List<JobListItem>();
                return;
            }

            using var conn = new SqlConnection(cs);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT TOP(500)
                       JobNumber, JobName, Status, MailDate, Csr, DataProcessing, Quantity, PostageStyle
                FROM dbo.Jobs
                WHERE 1=1";

            if (!string.IsNullOrWhiteSpace(Search))
            {
                cmd.CommandText += " AND (JobName LIKE @Search OR Customer LIKE @Search OR CAST(JobNumber as varchar(20)) LIKE @Search)";
                cmd.Parameters.AddWithValue("@Search", $"%{Search}%");
            }

            if (!string.IsNullOrWhiteSpace(StatusFilter) && !string.Equals(StatusFilter, "All", StringComparison.OrdinalIgnoreCase))
            {
                cmd.CommandText += " AND Status = @Status";
                cmd.Parameters.AddWithValue("@Status", StatusFilter);
            }

            cmd.CommandText += " ORDER BY JobNumber DESC;";

            var list = new List<JobListItem>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list1.Add(new JobListItem
                {
                    JobNumber = SqlReaderValue.ReadInt32(reader, 0),
                    JobName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    Status = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    MailDate = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3),
                    Csr = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    DataProcessing = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    Quantity = reader.IsDBNull(6) ? 0 : SqlReaderValue.ReadInt32(reader, 6),
                    PostageStyle = reader.IsDBNull(7) ? string.Empty : reader.GetString(7)
                });
            }

            Jobs = list;
        }

        private static string Csv(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";

        private static string BuildCsv(IEnumerable<JobListItem> jobs)
        {
            using var sw = new StringWriter();
            sw.WriteLine("JobNumber,JobName,Status,MailDate,Csr,DataProcessing,Quantity,PostageStyle");
            foreach (var job in jobs)
            {
                sw.WriteLine($"{job.JobNumber},{Csv(job.JobName)},{Csv(job.Status)},{Csv(job.MailDate?.ToString("yyyy-MM-dd"))},{Csv(job.Csr)},{Csv(job.DataProcessing)},{job.Quantity},{Csv(job.PostageStyle)}");
            }

            return sw.ToString();
        }

        public class JobListItem
        {
            public int JobNumber { get; set; }
            public string JobName { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public DateTime? MailDate { get; set; }
            public string Csr { get; set; } = string.Empty;
            public string DataProcessing { get; set; } = string.Empty;
            public int Quantity { get; set; }
            public string PostageStyle { get; set; } = string.Empty;
        }
    }
}
