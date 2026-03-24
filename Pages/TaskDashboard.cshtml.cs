using JobEntryApp.Models;
using JobEntryApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace JobEntryApp.Pages
{
    public class TaskDashboardModel : PageModel
    {
        private readonly IConfiguration _config;
        private readonly ILogger<TaskDashboardModel> _logger;

        public TaskDashboardModel(IConfiguration config, ILogger<TaskDashboardModel> logger)
        {
            _config = config;
            _logger = logger;
        }

        public List<JobSummary> DataProcessingJobs { get; set; } = new();
        public List<JobSummary> ReadyForProductionJobs { get; set; } = new();
        public List<JobSummary> MailingSoonJobs { get; set; } = new();

        public List<TaskDashboardItem> Tasks { get; set; } = new();
        public CalendarMonthViewModel? MasterCalendar { get; private set; }
        public List<string> Assignees { get; set; } = new();
        public List<string> Stages { get; set; } = new();
        public List<string> Statuses { get; set; } = new();
        public List<string> Dps { get; set; } = new();
        public Dictionary<string, int> CsrTaskCounts { get; set; } = new();
        public Dictionary<string, int> DpJobCounts { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? FilterAssignee { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? FilterDp { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? FilterStage { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? FilterTaskSearch { get; set; }

        [BindProperty(SupportsGet = true)]
        public string ViewMode { get; set; } = "list";

        [BindProperty(SupportsGet = true)]
        public string? CalendarMonth { get; set; }

        [BindProperty(SupportsGet = true)]
        public string FilterDateRange { get; set; } = "thisweek";

        [BindProperty(SupportsGet = true)]
        public string FilterStatus { get; set; } = "active";

        [BindProperty(SupportsGet = true)]
        public bool ExcludeMailedJobs { get; set; } = true;

        [BindProperty(SupportsGet = true)]
        public new int Page { get; set; } = 1;

        public int PageSize { get; set; } = 20;
        public int TotalCount { get; set; }
        public string CalendarMonthLabel { get; private set; } = DateTime.Today.ToString("MMMM yyyy");
        public string PreviousCalendarMonth { get; private set; } = DateTime.Today.AddMonths(-1).ToString("yyyy-MM");
        public string NextCalendarMonth { get; private set; } = DateTime.Today.AddMonths(1).ToString("yyyy-MM");

        public List<TaskDashboardItem> UpcomingTasks =>
            Tasks.Where(t => t.DueDate >= DateTime.Today)
                 .OrderBy(t => t.DueDate)
                 .Take(20)
                 .ToList();

        [TempData]
        public string? StatusMessage { get; set; }

        public void OnGet()
        {
            ViewMode = string.IsNullOrWhiteSpace(ViewMode) ? "list" : ViewMode;
            LoadAssignees();
            LoadStagesAndStatuses();
            LoadDps();
            LoadCsrCounts();
            LoadDpCounts();
            LoadTasks();
            LoadMasterCalendar();
            LoadSectionSummaries();
        }

        private void LoadAssignees()
        {
            var cs = _config.GetConnectionString("JobEntryDb")
                ?? throw new InvalidOperationException("Missing connection string.");

            using var conn = new SqlConnection(cs);
            conn.Open();

            using SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT DISTINCT AssignedTo FROM dbo.Tasks WHERE AssignedTo IS NOT NULL ORDER BY AssignedTo;";
            using SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                Assignees.Add(reader.GetString(0));
            }
        }

        private void LoadStagesAndStatuses()
        {
            var cs = _config.GetConnectionString("JobEntryDb")
                ?? throw new InvalidOperationException("Missing connection string.");

            using var conn = new SqlConnection(cs);
            conn.Open();

            using SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT DISTINCT Stage FROM dbo.Tasks WHERE Stage IS NOT NULL ORDER BY Stage;";
            using (SqlDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    Stages.Add(reader.GetString(0));
                }
            }

            using SqlCommand cmd2 = conn.CreateCommand();
            cmd2.CommandText = @"SELECT DISTINCT Status FROM dbo.Tasks WHERE Status IS NOT NULL ORDER BY Status;";
            using (SqlDataReader reader2 = cmd2.ExecuteReader())
            {
                while (reader2.Read())
                {
                    Statuses.Add(reader2.GetString(0));
                }
            }
        }

        private void LoadDps()
        {
            var cs = _config.GetConnectionString("JobEntryDb")
                ?? throw new InvalidOperationException("Missing connection string.");

            using var conn = new SqlConnection(cs);
            conn.Open();

            using SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT DISTINCT DataProcessing FROM dbo.Jobs WHERE DataProcessing IS NOT NULL ORDER BY DataProcessing;";
            using SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                Dps.Add(reader.GetString(0));
            }
        }

        private void LoadCsrCounts()
        {
            CsrTaskCounts.Clear();
            var cs = _config.GetConnectionString("JobEntryDb")
                ?? throw new InvalidOperationException("Missing connection string.");

            using var conn = new SqlConnection(cs);
            conn.Open();

            using SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT AssignedTo, COUNT(*) FROM dbo.Tasks WHERE AssignedTo IS NOT NULL GROUP BY AssignedTo;";
            using SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var name = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                var cnt = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                CsrTaskCounts[name] = cnt;
            }
        }

        private void LoadDpCounts()
        {
            DpJobCounts.Clear();
            var cs = _config.GetConnectionString("JobEntryDb")
                ?? throw new InvalidOperationException("Missing connection string.");

            using var conn = new SqlConnection(cs);
            conn.Open();

            using SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT DataProcessing, COUNT(*) FROM dbo.Jobs WHERE DataProcessing IS NOT NULL GROUP BY DataProcessing;";
            using SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var name = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                var cnt = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                DpJobCounts[name] = cnt;
            }
        }

        private void LoadTasks()
        {
            var cs = _config.GetConnectionString("JobEntryDb")
                ?? throw new InvalidOperationException("Missing connection string.");

            using var conn = new SqlConnection(cs);
            conn.Open();

            var (whereConditions, parameters) = BuildTaskFilterParts(applyDateRangeFilter: true);
            var whereClause = BuildWhereClause(whereConditions);

            using (SqlCommand countCmd = conn.CreateCommand())
            {
                countCmd.CommandText = $@"
                    SELECT COUNT(*)
                    FROM dbo.Tasks t
                    INNER JOIN dbo.Jobs j ON t.JobNumber = j.JobNumber
                    {whereClause};";
                if (parameters.Count > 0)
                    countCmd.Parameters.AddRange(parameters.Select(CloneParameter).ToArray());
                TotalCount = Convert.ToInt32(countCmd.ExecuteScalar());
            }

            int offset = (Math.Max(1, Page) - 1) * PageSize;

            using SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                SELECT 
                    t.TaskId,
                    t.JobNumber,
                    j.JobName,
                    j.Customer,
                    t.TaskNumber,
                    t.TaskName,
                    t.Stage,
                    t.AssignedTo,
                    t.Assignee2,
                    t.DueDate,
                    t.Status,
                    t.CompletedDate,
                    t.Dependencies,
                    j.MailDate,
                    j.StartDate
                FROM dbo.Tasks t
                INNER JOIN dbo.Jobs j ON t.JobNumber = j.JobNumber
                {whereClause}
                ORDER BY 
                    CASE WHEN t.DueDate < @TodaySort AND t.Status != 'Completed' THEN 0 ELSE 1 END,
                    t.DueDate ASC,
                    t.JobNumber DESC,
                    t.TaskNumber ASC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

            parameters.Add(new SqlParameter("@TodaySort", DateTime.Today));
            parameters.Add(new SqlParameter("@Offset", offset));
            parameters.Add(new SqlParameter("@PageSize", PageSize));
            cmd.Parameters.AddRange(parameters.Select(CloneParameter).ToArray());

            using SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                Tasks.Add(new TaskDashboardItem
                {
                    TaskId = reader.GetGuid(0),
                    JobNumber = SqlReaderValue.ReadInt32(reader, 1),
                    JobName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Customer = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    TaskNumber = SqlReaderValue.ReadInt32(reader, 4),
                    TaskName = reader.GetString(5),
                    Stage = reader.IsDBNull(6) ? null : reader.GetString(6),
                    AssignedTo = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                    Assignee2 = reader.IsDBNull(8) ? null : reader.GetString(8),
                    DueDate = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                    Status = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                    CompletedDate = reader.IsDBNull(11) ? null : reader.GetDateTime(11),
                    Dependencies = reader.IsDBNull(12) ? null : reader.GetString(12),
                    JobMailDate = reader.IsDBNull(13) ? null : reader.GetDateTime(13),
                    JobStartDate = reader.IsDBNull(14) ? null : reader.GetDateTime(14)
                });
            }
        }

        private void LoadMasterCalendar()
        {
            var monthStart = ParseCalendarMonth(CalendarMonth);
            CalendarMonth = monthStart.ToString("yyyy-MM");
            CalendarMonthLabel = monthStart.ToString("MMMM yyyy");
            PreviousCalendarMonth = monthStart.AddMonths(-1).ToString("yyyy-MM");
            NextCalendarMonth = monthStart.AddMonths(1).ToString("yyyy-MM");

            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var gridStart = monthStart.AddDays(-(int)monthStart.DayOfWeek);
            var gridEndExclusive = monthEnd.AddDays(7 - (int)monthEnd.DayOfWeek);

            var cs = _config.GetConnectionString("JobEntryDb")
                ?? throw new InvalidOperationException("Missing connection string.");

            using var conn = new SqlConnection(cs);
            conn.Open();

            var (whereConditions, parameters) = BuildTaskFilterParts(applyDateRangeFilter: false);
            whereConditions.Add("t.DueDate >= @CalendarStart");
            whereConditions.Add("t.DueDate < @CalendarEndExclusive");
            parameters.Add(new SqlParameter("@CalendarStart", gridStart));
            parameters.Add(new SqlParameter("@CalendarEndExclusive", gridEndExclusive));

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                SELECT
                    t.JobNumber,
                    j.JobName,
                    t.TaskNumber,
                    t.TaskName,
                    t.DueDate,
                    t.Status
                FROM dbo.Tasks t
                INNER JOIN dbo.Jobs j ON t.JobNumber = j.JobNumber
                {BuildWhereClause(whereConditions)}
                ORDER BY t.DueDate, t.JobNumber, t.TaskNumber;";
            if (parameters.Count > 0)
            {
                cmd.Parameters.AddRange(parameters.Select(CloneParameter).ToArray());
            }

            var events = new List<CalendarEventInput>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if (reader.IsDBNull(4))
                {
                    continue;
                }

                var taskName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
                if (!TaskMilestoneCatalog.TryGetMilestone(taskName, out var milestone))
                {
                    continue;
                }

                var jobNumber = SqlReaderValue.ReadInt32(reader, 0);
                var jobName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                var taskNumber = reader.IsDBNull(2) ? int.MaxValue : SqlReaderValue.ReadInt32(reader, 2);
                var dueDate = reader.GetDateTime(4).Date;
                var status = reader.IsDBNull(5) ? string.Empty : reader.GetString(5);
                var isCompleted = string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase);

                events.Add(new CalendarEventInput
                {
                    Date = dueDate,
                    Title = $"{milestone.Label} · {jobNumber}",
                    Subtitle = string.IsNullOrWhiteSpace(jobName) ? $"Job {jobNumber}" : jobName,
                    Url = $"/Jobs/Details?jobNumber={jobNumber}",
                    CssClass = isCompleted ? "calendar-event-complete" : milestone.CssClass,
                    IsCompleted = isCompleted,
                    IsMilestone = true,
                    SortOrder = taskNumber
                });
            }

            MasterCalendar = CalendarBuilder.BuildMonth(monthStart, events, compact: false, maxItemsPerDay: 5);
        }

        private (List<string> Conditions, List<SqlParameter> Parameters) BuildTaskFilterParts(bool applyDateRangeFilter)
        {
            var whereConditions = new List<string>();
            var parameters = new List<SqlParameter>();

            if (!string.IsNullOrWhiteSpace(FilterAssignee))
            {
                whereConditions.Add("(t.AssignedTo = @Assignee OR t.Assignee2 = @Assignee)");
                parameters.Add(new SqlParameter("@Assignee", FilterAssignee));
            }

            if (!string.IsNullOrWhiteSpace(FilterDp))
            {
                whereConditions.Add("j.DataProcessing = @Dp");
                parameters.Add(new SqlParameter("@Dp", FilterDp));
            }

            if (!string.IsNullOrWhiteSpace(FilterStage))
            {
                whereConditions.Add("t.Stage = @Stage");
                parameters.Add(new SqlParameter("@Stage", FilterStage));
            }

            if (!string.IsNullOrWhiteSpace(FilterTaskSearch))
            {
                whereConditions.Add("(t.TaskName LIKE @TaskSearch OR j.JobName LIKE @TaskSearch)");
                parameters.Add(new SqlParameter("@TaskSearch", $"%{FilterTaskSearch}%"));
            }

            if (applyDateRangeFilter)
            {
                if (FilterDateRange == "overdue")
                {
                    whereConditions.Add("t.DueDate < @Today AND t.Status != 'Completed'");
                    parameters.Add(new SqlParameter("@Today", DateTime.Today));
                }
                else if (FilterDateRange == "today")
                {
                    whereConditions.Add("CAST(t.DueDate AS DATE) = @Today");
                    parameters.Add(new SqlParameter("@Today", DateTime.Today));
                }
                else if (FilterDateRange == "thisweek")
                {
                    var endOfWeek = DateTime.Today.AddDays(7);
                    whereConditions.Add("t.DueDate BETWEEN @Today AND @EndOfWeek");
                    parameters.Add(new SqlParameter("@Today", DateTime.Today));
                    parameters.Add(new SqlParameter("@EndOfWeek", endOfWeek));
                }
                else if (FilterDateRange == "nextweek")
                {
                    var startNextWeek = DateTime.Today.AddDays(8);
                    var endNextWeek = DateTime.Today.AddDays(14);
                    whereConditions.Add("t.DueDate BETWEEN @StartNextWeek AND @EndNextWeek");
                    parameters.Add(new SqlParameter("@StartNextWeek", startNextWeek));
                    parameters.Add(new SqlParameter("@EndNextWeek", endNextWeek));
                }
            }

            if (FilterStatus == "active")
            {
                whereConditions.Add("t.Status != 'Completed'");
            }
            else if (FilterStatus == "completed")
            {
                whereConditions.Add("t.Status = 'Completed'");
            }

            if (ExcludeMailedJobs)
            {
                whereConditions.Add(@"
                    (j.MailDate IS NULL OR j.MailDate >= @Today OR 
                     NOT EXISTS (
                         SELECT 1 FROM dbo.MailChart mc 
                         WHERE mc.JobNumber = j.JobNumber 
                         AND mc.MailDate < @Today
                     ))
                ");
                if (!parameters.Any(p => p.ParameterName == "@Today"))
                {
                    parameters.Add(new SqlParameter("@Today", DateTime.Today));
                }
            }

            return (whereConditions, parameters);
        }

        private static string BuildWhereClause(List<string> whereConditions)
            => whereConditions.Count > 0 ? "WHERE " + string.Join(" AND ", whereConditions) : string.Empty;

        private static SqlParameter CloneParameter(SqlParameter p)
        {
            var clone = new SqlParameter(p.ParameterName, p.SqlDbType)
            {
                Value = p.Value,
                Direction = p.Direction,
                IsNullable = p.IsNullable,
                Size = p.Size,
                Precision = p.Precision,
                Scale = p.Scale
            };
            return clone;
        }

        private static DateTime ParseCalendarMonth(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)
                && DateTime.TryParse($"{value}-01", out var parsed))
            {
                return new DateTime(parsed.Year, parsed.Month, 1);
            }

            return new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        }

        private void LoadSectionSummaries()
        {
            var cs = _config.GetConnectionString("JobEntryDb")
                ?? throw new InvalidOperationException("Missing connection string.");

            using var conn = new SqlConnection(cs);
            conn.Open();

            // Data Processing section: active jobs where DataProcessing is not null
            using (SqlCommand cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT j.JobNumber, j.JobName, j.Customer, j.Csr, j.DataProcessing,
                           j.Status, j.StartDate, j.MailDate,
                           ISNULL(pt.PendingTasks, 0) AS PendingTasks
                    FROM dbo.Jobs j
                    LEFT JOIN (
                        SELECT JobNumber, COUNT(*) AS PendingTasks
                        FROM dbo.Tasks
                        WHERE Status != 'Completed'
                        GROUP BY JobNumber
                    ) pt ON pt.JobNumber = j.JobNumber
                    WHERE j.DataProcessing IS NOT NULL
                      AND j.Status NOT IN ('Completed','Mailed')
                    ORDER BY j.MailDate ASC;";
                using SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                    DataProcessingJobs.Add(ReadJobSummary(reader));
            }

            // Ready for Production section: active jobs without open data-processing tasks
            using (SqlCommand cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT j.JobNumber, j.JobName, j.Customer, j.Csr, j.DataProcessing,
                           j.Status, j.StartDate, j.MailDate,
                           ISNULL(pt.PendingTasks, 0) AS PendingTasks
                    FROM dbo.Jobs j
                    LEFT JOIN (
                        SELECT JobNumber, COUNT(*) AS PendingTasks
                        FROM dbo.Tasks
                        WHERE Status != 'Completed'
                        GROUP BY JobNumber
                    ) pt ON pt.JobNumber = j.JobNumber
                    WHERE j.Status NOT IN ('Completed','Mailed')
                      AND NOT EXISTS (
                          SELECT 1 FROM dbo.Tasks t
                          WHERE t.JobNumber = j.JobNumber
                            AND t.Stage = 'Data Processing'
                            AND t.Status != 'Completed'
                      )
                    ORDER BY j.MailDate ASC;";
                using SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                    ReadyForProductionJobs.Add(ReadJobSummary(reader));
            }

            // Mailing Soon section: active jobs with mail date within 7 days
            using (SqlCommand cmd = conn.CreateCommand())
            {
                cmd.Parameters.AddWithValue("@Today", DateTime.Today);
                cmd.Parameters.AddWithValue("@Soon", DateTime.Today.AddDays(7));
                cmd.CommandText = @"
                    SELECT j.JobNumber, j.JobName, j.Customer, j.Csr, j.DataProcessing,
                           j.Status, j.StartDate, j.MailDate,
                           ISNULL(pt.PendingTasks, 0) AS PendingTasks
                    FROM dbo.Jobs j
                    LEFT JOIN (
                        SELECT JobNumber, COUNT(*) AS PendingTasks
                        FROM dbo.Tasks
                        WHERE Status != 'Completed'
                        GROUP BY JobNumber
                    ) pt ON pt.JobNumber = j.JobNumber
                    WHERE j.Status NOT IN ('Completed','Mailed')
                      AND j.MailDate IS NOT NULL
                      AND j.MailDate >= @Today
                      AND j.MailDate <= @Soon
                    ORDER BY j.MailDate ASC;";
                using SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                    MailingSoonJobs.Add(ReadJobSummary(reader));
            }
        }

        private static JobSummary ReadJobSummary(SqlDataReader reader) => new JobSummary
        {
            JobNumber      = SqlReaderValue.ReadInt32(reader, 0),
            JobName        = reader.IsDBNull(1) ? "" : reader.GetString(1),
            Customer       = reader.IsDBNull(2) ? "" : reader.GetString(2),
            Csr            = reader.IsDBNull(3) ? null : reader.GetString(3),
            DataProcessing = reader.IsDBNull(4) ? null : reader.GetString(4),
            Status         = reader.IsDBNull(5) ? "" : reader.GetString(5),
            StartDate      = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
            MailDate       = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
            PendingTaskCount = SqlReaderValue.ReadInt32(reader, 8)
        };

        public IActionResult OnPostCompleteTask(Guid taskId)
        {
            if (taskId == Guid.Empty)
            {
                StatusMessage = "Invalid task id.";
                return RedirectToPage(new { FilterAssignee, FilterDateRange, FilterStatus, ExcludeMailedJobs, FilterStage, FilterTaskSearch, FilterDp, Page });
            }

            var cs = _config.GetConnectionString("JobEntryDb")
                ?? throw new InvalidOperationException("Missing connection string.");

            try
            {
                using var conn = new SqlConnection(cs);
                conn.Open();

                using SqlCommand cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    UPDATE dbo.Tasks 
                    SET Status = 'Completed', 
                        CompletedDate = GETDATE()
                    WHERE TaskId = @TaskId;";

                cmd.Parameters.AddWithValue("@TaskId", taskId);
                var rows = cmd.ExecuteNonQuery();
                StatusMessage = rows > 0 ? "Task marked complete." : "Task was not found.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to complete task {TaskId} from dashboard.", taskId);
                StatusMessage = "Could not complete task.";
            }

            return RedirectToPage(new { FilterAssignee, FilterDateRange, FilterStatus, ExcludeMailedJobs, FilterStage, FilterTaskSearch, FilterDp, Page });
        }

        public IActionResult OnPostUncompleteTask(Guid taskId)
        {
            if (taskId == Guid.Empty)
            {
                StatusMessage = "Invalid task id.";
                return RedirectToPage(new { FilterAssignee, FilterDateRange, FilterStatus, ExcludeMailedJobs, FilterStage, FilterTaskSearch, FilterDp, Page });
            }

            var cs = _config.GetConnectionString("JobEntryDb")
                ?? throw new InvalidOperationException("Missing connection string.");

            try
            {
                using var conn = new SqlConnection(cs);
                conn.Open();

                using SqlCommand cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    UPDATE dbo.Tasks 
                    SET Status = 'Scheduled', 
                        CompletedDate = NULL
                    WHERE TaskId = @TaskId;";

                cmd.Parameters.AddWithValue("@TaskId", taskId);
                var rows = cmd.ExecuteNonQuery();
                StatusMessage = rows > 0 ? "Task marked incomplete." : "Task was not found.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to uncomplete task {TaskId} from dashboard.", taskId);
                StatusMessage = "Could not update task.";
            }

            return RedirectToPage(new { FilterAssignee, FilterDateRange, FilterStatus, ExcludeMailedJobs, FilterStage, FilterTaskSearch, FilterDp, Page });
        }

        public class TaskDashboardItem
        {
            public Guid TaskId { get; set; }
            public int JobNumber { get; set; }
            public string JobName { get; set; } = string.Empty;
            public string Customer { get; set; } = string.Empty;
            public int TaskNumber { get; set; }
            public string TaskName { get; set; } = string.Empty;
            public string? Stage { get; set; }
            public string AssignedTo { get; set; } = string.Empty;
            public string? Assignee2 { get; set; }
            public DateTime? DueDate { get; set; }
            public string Status { get; set; } = string.Empty;
            public DateTime? CompletedDate { get; set; }
            public string? Dependencies { get; set; }
            public DateTime? JobMailDate { get; set; }
            public DateTime? JobStartDate { get; set; }

            public bool IsCompleted => Status == "Completed";
            public bool IsOverdue => DueDate.HasValue && DueDate.Value.Date < DateTime.Today && !IsCompleted;
            public bool IsDueToday => DueDate.HasValue && DueDate.Value.Date == DateTime.Today && !IsCompleted;
            public bool IsDueSoon => DueDate.HasValue && DueDate.Value.Date > DateTime.Today && DueDate.Value.Date <= DateTime.Today.AddDays(3) && !IsCompleted;

            public string GetDueDateDisplay()
            {
                if (!DueDate.HasValue) return "-";

                var days = (DueDate.Value.Date - DateTime.Today).Days;
                var dateStr = DueDate.Value.ToString("MM/dd/yyyy");

                if (days < 0) return $"{dateStr} ({Math.Abs(days)} days ago)";
                if (days == 0) return $"{dateStr} (TODAY)";
                if (days == 1) return $"{dateStr} (Tomorrow)";
                if (days <= 7) return $"{dateStr} (in {days} days)";

                return dateStr;
            }
        }
    }
}
