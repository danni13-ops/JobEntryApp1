using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using JobEntryApp.Infrastructure;

namespace JobEntryApp.Pages.Mailchart
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

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? CsrFilter { get; set; }

        [BindProperty]
        public EditMailChartInput EditInput { get; set; } = new();

        [TempData]
        public string? StatusMessage { get; set; }

        public List<MailChartItem> TodayJobs { get; set; } = new();
        public List<MailChartItem> OtherJobs { get; set; } = new();
        public DateTime Today { get; set; } = DateTime.Today;

        public void OnGet()
        {
            SyncVisibleJobs();
            LoadMailChart();
        }

        public IActionResult OnPostEditRow()
        {
            if (EditInput.MailChartId <= 0)
            {
                StatusMessage = "Invalid row selected for edit.";
                return RedirectToPage(new { Search, CsrFilter });
            }

            if (EditInput.Kit < 0)
            {
                StatusMessage = "Kit cannot be negative.";
                return RedirectToPage(new { Search = EditInput.Search, CsrFilter = EditInput.CsrFilter });
            }

            if (EditInput.Kit > 9999)
            {
                StatusMessage = "Kit value is too large.";
                return RedirectToPage(new { Search = EditInput.Search, CsrFilter = EditInput.CsrFilter });
            }

            if (EditInput.Quantity < 0)
            {
                StatusMessage = "Quantity cannot be negative.";
                return RedirectToPage(new { Search = EditInput.Search, CsrFilter = EditInput.CsrFilter });
            }

            if (EditInput.Quantity > 2_000_000_000)
            {
                StatusMessage = "Quantity is too large.";
                return RedirectToPage(new { Search = EditInput.Search, CsrFilter = EditInput.CsrFilter });
            }

            if (!EditInput.MailDate.HasValue)
            {
                StatusMessage = "Mail Date is required for Mail Chart rows.";
                return RedirectToPage(new { Search = EditInput.Search, CsrFilter = EditInput.CsrFilter });
            }

            if (EditInput.MailDate.HasValue && EditInput.MailDate.Value.Date < Today)
            {
                StatusMessage = "Mail Date cannot be before today on the Mail Chart.";
                return RedirectToPage(new { Search = EditInput.Search, CsrFilter = EditInput.CsrFilter });
            }

            if (string.IsNullOrWhiteSpace(EditInput.Customer))
            {
                StatusMessage = "Customer is required.";
                return RedirectToPage(new { Search = EditInput.Search, CsrFilter = EditInput.CsrFilter });
            }

            if (string.IsNullOrWhiteSpace(EditInput.JobName))
            {
                StatusMessage = "Job Name is required.";
                return RedirectToPage(new { Search = EditInput.Search, CsrFilter = EditInput.CsrFilter });
            }

            if (string.IsNullOrWhiteSpace(EditInput.AE))
            {
                StatusMessage = "CSR (AE) is required.";
                return RedirectToPage(new { Search = EditInput.Search, CsrFilter = EditInput.CsrFilter });
            }

            var customer = TrimOrNull(EditInput.Customer);
            var jobName = TrimOrNull(EditInput.JobName);
            var postageClass = TrimOrNull(EditInput.Class);
            var ae = TrimOrNull(EditInput.AE);
            var styleTruck = TrimOrNull(EditInput.StyleTruck);
            var commingler = TrimOrNull(EditInput.Commingler);

            if (customer is null || customer.Length > 200
                || jobName is null || jobName.Length > 200
                || postageClass?.Length > 100
                || ae is null || ae.Length > 100
                || styleTruck?.Length > 100
                || commingler?.Length > 100)
            {
                StatusMessage = "One or more fields are invalid or too long.";
                return RedirectToPage(new { Search = EditInput.Search, CsrFilter = EditInput.CsrFilter });
            }

            try
            {
                var cs = _config.GetConnectionString("JobEntryDb")
                    ?? throw new InvalidOperationException("Missing connection string 'JobEntryDb'.");

                using var conn = new SqlConnection(cs);
                conn.Open();

                using SqlCommand cmd = conn.CreateCommand();
                using SqlCommand lookupCmd = conn.CreateCommand();
                lookupCmd.CommandText = @"SELECT JobNumber FROM dbo.MailChart WHERE MailChartId = @MailChartId;";
                lookupCmd.Parameters.AddWithValue("@MailChartId", EditInput.MailChartId);
                var jobNumberObj = lookupCmd.ExecuteScalar();
                if (jobNumberObj is null || jobNumberObj == DBNull.Value)
                {
                    StatusMessage = "Selected Mail Chart row was not found.";
                    return RedirectToPage(new { Search = EditInput.Search, CsrFilter = EditInput.CsrFilter });
                }

                var rowJobNumber = Convert.ToInt32(jobNumberObj);

                using SqlCommand dupCmd = conn.CreateCommand();
                dupCmd.CommandText = @"
                    SELECT COUNT(*)
                    FROM dbo.MailChart
                    WHERE JobNumber = @JobNumber
                      AND Kit = @Kit
                      AND MailChartId <> @MailChartId;";
                dupCmd.Parameters.AddWithValue("@JobNumber", rowJobNumber);
                dupCmd.Parameters.AddWithValue("@Kit", EditInput.Kit);
                dupCmd.Parameters.AddWithValue("@MailChartId", EditInput.MailChartId);
                var dupCount = Convert.ToInt32(dupCmd.ExecuteScalar());
                if (dupCount > 0)
                {
                    StatusMessage = $"Kit {EditInput.Kit} already exists for job {rowJobNumber}.";
                    return RedirectToPage(new { Search = EditInput.Search, CsrFilter = EditInput.CsrFilter });
                }

                cmd.CommandText = @"
                    UPDATE dbo.MailChart
                    SET Kit = @Kit,
                        Customer = @Customer,
                        JobName = @JobName,
                        Class = @Class,
                        AE = @AE,
                        MailDate = @MailDate,
                        Quantity = @Quantity,
                        StyleTruck = @StyleTruck,
                        Commingler = @Commingler
                    WHERE MailChartId = @MailChartId;";

                cmd.Parameters.AddWithValue("@MailChartId", EditInput.MailChartId);
                cmd.Parameters.AddWithValue("@Kit", EditInput.Kit);
                cmd.Parameters.AddWithValue("@Customer", customer);
                cmd.Parameters.AddWithValue("@JobName", jobName);
                cmd.Parameters.AddWithValue("@Class", (object?)postageClass ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@AE", ae);
                cmd.Parameters.AddWithValue("@MailDate", EditInput.MailDate.HasValue ? EditInput.MailDate.Value : (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Quantity", EditInput.Quantity);
                cmd.Parameters.AddWithValue("@StyleTruck", (object?)styleTruck ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Commingler", (object?)commingler ?? DBNull.Value);

                var rows = cmd.ExecuteNonQuery();
                StatusMessage = rows > 0 ? "Mail Chart row updated." : "No row updated (record may have been removed).";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update MailChart row {MailChartId}", EditInput.MailChartId);
                StatusMessage = "Update failed. Please try again.";
            }

            return RedirectToPage(new { Search = EditInput.Search, CsrFilter = EditInput.CsrFilter });
        }

        private static string? TrimOrNull(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }

        private void SyncVisibleJobs()
        {
            var cs = _config.GetConnectionString("JobEntryDb")
                ?? throw new InvalidOperationException("Missing connection string 'JobEntryDb'.");

            try
            {
                using var conn = new SqlConnection(cs);
                conn.Open();

                var basePath = _config["JobFoldersBasePath"]?.Trim() ?? @"P:\Danielle\JOB FOLDERS";
                foreach (var jobNumber in LoadVisibleJobNumbers(conn))
                {
                    JobFolderAutomationService.SyncJobArtifactsForJob(conn, basePath, jobNumber, _logger);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MailChart folder automation sync skipped.");
            }
        }

        private List<int> LoadVisibleJobNumbers(SqlConnection conn)
        {
            using var cmd = conn.CreateCommand();

            var where = new List<string>
            {
                "CAST(MailDate AS date) >= @today"
            };
            cmd.Parameters.AddWithValue("@today", Today);

            if (!string.IsNullOrWhiteSpace(Search))
            {
                where.Add("(JobName LIKE @search OR Customer LIKE @search OR CAST(JobNumber AS nvarchar(20)) LIKE @search OR CAST(Kit AS nvarchar(20)) LIKE @search)");
                cmd.Parameters.AddWithValue("@search", $"%{Search}%");
            }

            if (!string.IsNullOrWhiteSpace(CsrFilter) && CsrFilter != "All")
            {
                where.Add("AE = @csr");
                cmd.Parameters.AddWithValue("@csr", CsrFilter);
            }

            cmd.CommandText = $@"
                SELECT DISTINCT JobNumber
                FROM dbo.MailChart
                WHERE {string.Join(" AND ", where)}
                ORDER BY JobNumber;";

            var jobNumbers = new List<int>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if (!reader.IsDBNull(0))
                {
                    jobNumbers.Add(SqlReaderValue.ReadInt32(reader, 0));
                }
            }

            return jobNumbers;
        }

        private void LoadMailChart()
        {
            var cs = _config.GetConnectionString("JobEntryDb")
                ?? throw new InvalidOperationException("Missing connection string 'JobEntryDb'.");

            using var conn = new SqlConnection(cs);
            using var cmd = conn.CreateCommand();

            var where = new List<string>
            {
                "CAST(MailDate AS date) >= @today"
            };
            cmd.Parameters.AddWithValue("@today", Today);

            if (!string.IsNullOrWhiteSpace(Search))
            {
                where.Add("(JobName LIKE @search OR Customer LIKE @search OR CAST(JobNumber AS nvarchar(20)) LIKE @search OR CAST(Kit AS nvarchar(20)) LIKE @search)");
                cmd.Parameters.AddWithValue("@search", $"%{Search}%");
            }

            if (!string.IsNullOrWhiteSpace(CsrFilter) && CsrFilter != "All")
            {
                where.Add("AE = @csr");
                cmd.Parameters.AddWithValue("@csr", CsrFilter);
            }

            var whereClause = "WHERE " + string.Join(" AND ", where);

            cmd.CommandText = $@"
                SELECT MailChartId, JobNumber, Kit, Customer, JobName, Class, AE, MailDate, Quantity, StyleTruck, Commingler
                FROM dbo.MailChart
                {whereClause}
                ORDER BY CASE WHEN CAST(MailDate AS date) = @today THEN 0 ELSE 1 END,
                         MailDate,
                         AE,
                         JobNumber,
                         Kit;";

            conn.Open();

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var item = new MailChartItem
                {
                    MailChartId = SqlReaderValue.ReadInt32(reader, 0),
                    JobNumber = SqlReaderValue.ReadInt32(reader, 1),
                    Kit = SqlReaderValue.ReadInt32(reader, 2),
                    Customer = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    JobName = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    Class = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    AE = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                    MailDate = reader.IsDBNull(7) ? (DateTime?)null : reader.GetDateTime(7),
                    Quantity = reader.IsDBNull(8) ? 0 : SqlReaderValue.ReadInt32(reader, 8),
                    StyleTruck = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                    Commingler = reader.IsDBNull(10) ? string.Empty : reader.GetString(10)
                };

                if (item.MailDate.HasValue && item.MailDate.Value.Date == Today)
                {
                    TodayJobs.Add(item);
                }
                else if (item.MailDate.HasValue && item.MailDate.Value.Date > Today)
                {
                    OtherJobs.Add(item);
                }
            }

            TodayJobs = TodayJobs.OrderBy(j => j.AE).ThenBy(j => j.JobNumber).ThenBy(j => j.Kit).ToList();
            OtherJobs = OtherJobs
                .OrderBy(j => j.MailDate ?? DateTime.MaxValue)
                .ThenBy(j => j.AE)
                .ThenBy(j => j.JobNumber)
                .ThenBy(j => j.Kit)
                .ToList();
        }

        public class EditMailChartInput
        {
            public int MailChartId { get; set; }
            public int Kit { get; set; }
            public string? Customer { get; set; }
            public string? JobName { get; set; }
            public string? Class { get; set; }
            public string? AE { get; set; }
            public DateTime? MailDate { get; set; }
            public int Quantity { get; set; }
            public string? StyleTruck { get; set; }
            public string? Commingler { get; set; }
            public string? Search { get; set; }
            public string? CsrFilter { get; set; }
        }

        public class MailChartItem
        {
            public int MailChartId { get; set; }
            public int JobNumber { get; set; }
            public int Kit { get; set; }
            public string Customer { get; set; } = string.Empty;
            public string JobName { get; set; } = string.Empty;
            public string Class { get; set; } = string.Empty;
            public string AE { get; set; } = string.Empty;
            public DateTime? MailDate { get; set; }
            public int Quantity { get; set; }
            public string StyleTruck { get; set; } = string.Empty;
            public string Commingler { get; set; } = string.Empty;
        }
    }
}
