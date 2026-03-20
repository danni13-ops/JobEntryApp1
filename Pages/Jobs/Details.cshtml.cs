using JobEntryApp.Models;
using JobEntryApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace JobEntryApp.Pages.Jobs
{
    public class DetailsModel : PageModel
    {
        private readonly IConfiguration _config;
        private readonly ILogger<DetailsModel> _logger;

        public DetailsModel(IConfiguration config, ILogger<DetailsModel> logger)
        {
            _config = config;
            _logger = logger;
        }

        [BindProperty(SupportsGet = true)]
        public int JobNumber { get; set; }

        public JobModel? Job { get; set; }
        public TaskOverviewData TaskOverview { get; private set; } = new();
        public List<CalendarMonthViewModel> JobCalendars { get; private set; } = new();
        public string? JobFolderPath { get; private set; }
        public bool JobFolderExists { get; private set; }
        public FolderSyncResult FolderSync { get; private set; } = new();

        public IActionResult OnGet(int jobNumber)
        {
            if (jobNumber <= 0)
                return RedirectToPage("/Jobs/Index");

            try
            {
                var cs = _config.GetConnectionString("JobEntryDb")
                    ?? throw new InvalidOperationException("Missing connection string 'JobEntryDb'.");

                using var conn = new SqlConnection(cs);
                using SqlCommand cmd = conn.CreateCommand();

                cmd.CommandText = @"
                SELECT JobNumber, JobName, MailDate, Customer, SubAccount, Csr, DataProcessing,
                       Quantity, PostageStyle, PostageClass, Sales, StartDate, Status,
                       [Print], TwoWayMatch, RushJob,
                       COALESCE(NULLIF(JobSpeed, ''), CASE WHEN RushJob = 1 THEN 'Rush' ELSE 'Standard' END) AS JobSpeed,
                       MatchWayCount,
                       MatchComponent1, MatchComponent2, MatchComponent3, MatchComponent4, MatchComponent5,
                       MatchComponent1FacingDirection, MatchComponent2FacingDirection,
                       MatchComponent3FacingDirection, MatchComponent4FacingDirection, MatchComponent5FacingDirection,
                       PrintPieceCount,
                       PrintComponent1Name, PrintComponent1FacingDirection,
                       PrintComponent2Name, PrintComponent2FacingDirection,
                       PrintComponent3Name, PrintComponent3FacingDirection,
                       PrintComponent4Name, PrintComponent4FacingDirection,
                       PrintComponent5Name, PrintComponent5FacingDirection
                FROM dbo.Jobs
                WHERE JobNumber = @jobNumber;";
                cmd.Parameters.AddWithValue("@jobNumber", JobNumber);

                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return NotFound();
                    }

                    Job = new JobModel
                    {
                        JobNumber = SqlReaderValue.ReadInt32(reader, 0),
                        JobName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        MailDate = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2),
                        Customer = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                        SubAccount = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                        Csr = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                        DataProcessing = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                        Quantity = reader.IsDBNull(7) ? 0 : SqlReaderValue.ReadInt32(reader, 7),
                        PostageStyle = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                        PostageClass = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                        Sales = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                        StartDate = reader.IsDBNull(11) ? (DateTime?)null : reader.GetDateTime(11),
                        Status = reader.IsDBNull(12) ? string.Empty : reader.GetString(12),
                        Print = !reader.IsDBNull(13) && reader.GetBoolean(13),
                        TwoWayMatch = !reader.IsDBNull(14) && reader.GetBoolean(14),
                        RushJob = !reader.IsDBNull(15) && reader.GetBoolean(15),
                        JobSpeed = reader.IsDBNull(16) ? "Standard" : reader.GetString(16),
                        MatchWayCount = SqlReaderValue.ReadNullableInt32(reader, 17),
                        MatchComponent1 = reader.IsDBNull(18) ? string.Empty : reader.GetString(18),
                        MatchComponent2 = reader.IsDBNull(19) ? string.Empty : reader.GetString(19),
                        MatchComponent3 = reader.IsDBNull(20) ? string.Empty : reader.GetString(20),
                        MatchComponent4 = reader.IsDBNull(21) ? string.Empty : reader.GetString(21),
                        MatchComponent5 = reader.IsDBNull(22) ? string.Empty : reader.GetString(22),
                        MatchComponent1FacingDirection = reader.IsDBNull(23) ? null : reader.GetString(23),
                        MatchComponent2FacingDirection = reader.IsDBNull(24) ? null : reader.GetString(24),
                        MatchComponent3FacingDirection = reader.IsDBNull(25) ? null : reader.GetString(25),
                        MatchComponent4FacingDirection = reader.IsDBNull(26) ? null : reader.GetString(26),
                        MatchComponent5FacingDirection = reader.IsDBNull(27) ? null : reader.GetString(27),
                        PrintPieceCount = SqlReaderValue.ReadNullableInt32(reader, 28),
                        PrintComponent1Name = reader.IsDBNull(29) ? string.Empty : reader.GetString(29),
                        PrintComponent1FacingDirection = reader.IsDBNull(30) ? string.Empty : reader.GetString(30),
                        PrintComponent2Name = reader.IsDBNull(31) ? string.Empty : reader.GetString(31),
                        PrintComponent2FacingDirection = reader.IsDBNull(32) ? string.Empty : reader.GetString(32),
                        PrintComponent3Name = reader.IsDBNull(33) ? string.Empty : reader.GetString(33),
                        PrintComponent3FacingDirection = reader.IsDBNull(34) ? string.Empty : reader.GetString(34),
                        PrintComponent4Name = reader.IsDBNull(35) ? string.Empty : reader.GetString(35),
                        PrintComponent4FacingDirection = reader.IsDBNull(36) ? string.Empty : reader.GetString(36),
                        PrintComponent5Name = reader.IsDBNull(37) ? string.Empty : reader.GetString(37),
                        PrintComponent5FacingDirection = reader.IsDBNull(38) ? string.Empty : reader.GetString(38)
                    };
                }

                var basePath = _config["JobFoldersBasePath"]?.Trim() ?? @"P:\Danielle\JOB FOLDERS";

                try
                {
                    FolderSync = JobFolderAutomationService.SyncJobArtifactsForJob(conn, basePath, Job.JobNumber, _logger);
                    if (FolderSync.CountsWorkbook?.TotalQuantity > 0)
                    {
                        Job.Quantity = FolderSync.CountsWorkbook.TotalQuantity;
                    }
                }
                catch
                {
                    FolderSync = new FolderSyncResult();
                }

                try
                {
                    TaskOverview = LoadTaskOverview(conn, Job.JobNumber);
                    JobCalendars = BuildJobCalendars(TaskOverview.AllTasks, Job.JobNumber, Job.StartDate, Job.MailDate);
                }
                catch
                {
                    TaskOverview = new TaskOverviewData();
                    JobCalendars = new List<CalendarMonthViewModel>();
                }

                try
                {
                    JobFolderPath = JobFolderService.BuildJobFolderPath(basePath, Job.Customer, Job.SubAccount, Job.JobNumber, Job.JobName);
                    JobFolderExists = FolderSync.FolderExists || Directory.Exists(JobFolderPath);
                }
                catch
                {
                    JobFolderPath = null;
                    JobFolderExists = false;
                }
            }
            catch
            {
                return RedirectToPage("/Jobs/Index");
            }

            return Page();
        }

        private static List<CalendarMonthViewModel> BuildJobCalendars(
            IEnumerable<TaskSnapshot> tasks,
            int jobNumber,
            DateTime? startDate,
            DateTime? mailDate)
        {
            var events = tasks
                .Where(t => t.DueDate.HasValue)
                .Select(t =>
                {
                    var isCompleted = string.Equals(t.Status, "Completed", StringComparison.OrdinalIgnoreCase);
                    var milestone = TaskMilestoneCatalog.TryGetMilestone(t.TaskName, out var milestoneDisplay)
                        ? milestoneDisplay
                        : default;

                    return new CalendarEventInput
                    {
                        Date = t.DueDate!.Value.Date,
                        Title = t.TaskName,
                        Subtitle = isCompleted ? "Completed" : t.Stage,
                        Url = $"/Tasks?jobNumber={jobNumber}",
                        CssClass = ResolveCalendarCss(t, isCompleted, milestone),
                        IsCompleted = isCompleted,
                        IsMilestone = milestone != default,
                        SortOrder = t.TaskNumber ?? int.MaxValue
                    };
                })
                .ToList();

            if (events.Count == 0 && !startDate.HasValue && !mailDate.HasValue)
            {
                return new List<CalendarMonthViewModel>();
            }

            var rangeStart = startDate ?? events.Select(e => e.Date).DefaultIfEmpty(DateTime.Today).Min();
            var rangeEnd = mailDate ?? events.Select(e => e.Date).DefaultIfEmpty(rangeStart).Max();

            return CalendarBuilder.BuildMonths(events, rangeStart, rangeEnd, compact: true, maxItemsPerDay: 3, maxMonths: 3);
        }

        private static TaskOverviewData LoadTaskOverview(SqlConnection conn, int jobNumber)
        {
            var tasksTable = DatabaseSchema.FindTable(conn, null, "Tasks", "tasks");
            if (tasksTable is null)
            {
                return new TaskOverviewData();
            }

            var taskColumns = DatabaseSchema.GetColumns(conn, tasksTable);
            var jobNumberColumn = DatabaseSchema.FindColumn(taskColumns, "JobNumber", "job_number");
            var taskNameColumn = DatabaseSchema.FindColumn(taskColumns, "TaskName", "task_name");
            if (jobNumberColumn is null || taskNameColumn is null)
            {
                return new TaskOverviewData();
            }

            var taskNumberExpr = SelectColumnOrNull(taskColumns, "CAST(NULL AS int)", "TaskNumber", "task_number");
            var stageExpr = SelectColumnOrNull(taskColumns, "CAST(NULL AS nvarchar(100))", "Stage", "stage");
            var dueDateExpr = SelectColumnOrNull(taskColumns, "CAST(NULL AS datetime2)", "DueDate", "due_date");
            var statusExpr = SelectColumnOrNull(taskColumns, "CAST(NULL AS nvarchar(100))", "Status", "status");

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                SELECT
                    {taskNumberExpr} AS [TaskNumber],
                    [{taskNameColumn}] AS [TaskName],
                    {stageExpr} AS [Stage],
                    {dueDateExpr} AS [DueDate],
                    {statusExpr} AS [Status]
                FROM {tasksTable.SqlName}
                WHERE [{jobNumberColumn}] = @JobNumber
                ORDER BY
                    CASE WHEN {dueDateExpr} IS NULL THEN 1 ELSE 0 END,
                    {dueDateExpr},
                    CASE WHEN {taskNumberExpr} IS NULL THEN 1 ELSE 0 END,
                    {taskNumberExpr};";
            cmd.Parameters.AddWithValue("@JobNumber", jobNumber);

            var tasks = new List<TaskSnapshot>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                tasks.Add(new TaskSnapshot
                {
                    TaskNumber = reader.IsDBNull(0) ? (int?)null : SqlReaderValue.ReadInt32(reader, 0),
                    TaskName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    Stage = reader.IsDBNull(2) ? "Other" : reader.GetString(2),
                    DueDate = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3),
                    Status = reader.IsDBNull(4) ? "Scheduled" : reader.GetString(4)
                });
            }

            if (tasks.Count == 0)
            {
                return new TaskOverviewData();
            }

            var completedCount = tasks.Count(t => string.Equals(t.Status, "Completed", StringComparison.OrdinalIgnoreCase));
            var overdueCount = tasks.Count(t =>
                t.DueDate.HasValue &&
                t.DueDate.Value.Date < DateTime.Today &&
                !string.Equals(t.Status, "Completed", StringComparison.OrdinalIgnoreCase));

            return new TaskOverviewData
            {
                TotalCount = tasks.Count,
                CompletedCount = completedCount,
                OverdueCount = overdueCount,
                AllTasks = tasks,
                UpcomingTasks = tasks
                    .Where(t => !string.Equals(t.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(t => t.DueDate ?? DateTime.MaxValue)
                    .ThenBy(t => t.TaskNumber ?? int.MaxValue)
                    .Take(6)
                    .ToList(),
                StageSummaries = tasks
                    .GroupBy(t => string.IsNullOrWhiteSpace(t.Stage) ? "Other" : t.Stage)
                    .Select(g => new TaskStageSummary
                    {
                        Stage = g.Key,
                        TotalCount = g.Count(),
                        CompletedCount = g.Count(t => string.Equals(t.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                    })
                    .OrderBy(g => g.Stage)
                    .ToList()
            };
        }

        private static string SelectColumnOrNull(ISet<string> columns, string nullExpression, params string[] candidates)
        {
            var column = DatabaseSchema.FindColumn(columns, candidates);
            return column is null ? nullExpression : $"[{column}]";
        }

        private static string ResolveCalendarCss(
            TaskSnapshot task,
            bool isCompleted,
            TaskMilestoneCatalog.MilestoneDisplay milestone)
        {
            if (isCompleted)
            {
                return "calendar-event-complete";
            }

            if (milestone != default)
            {
                return milestone.CssClass;
            }

            if (task.DueDate.HasValue && task.DueDate.Value.Date < DateTime.Today)
            {
                return "calendar-event-overdue";
            }

            return task.Stage switch
            {
                "Finalize" => "calendar-event-mail",
                "Production" => "calendar-event-production",
                _ => "calendar-event-default"
            };
        }

        public class TaskOverviewData
        {
            public int TotalCount { get; set; }
            public int CompletedCount { get; set; }
            public int OverdueCount { get; set; }
            public List<TaskSnapshot> AllTasks { get; set; } = new();
            public List<TaskSnapshot> UpcomingTasks { get; set; } = new();
            public List<TaskStageSummary> StageSummaries { get; set; } = new();

            public int OpenCount => Math.Max(0, TotalCount - CompletedCount);
            public int ProgressPercent => TotalCount == 0 ? 0 : (int)Math.Round((double)CompletedCount * 100 / TotalCount);
        }

        public class TaskSnapshot
        {
            public int? TaskNumber { get; set; }
            public string TaskName { get; set; } = string.Empty;
            public string Stage { get; set; } = "Other";
            public DateTime? DueDate { get; set; }
            public string Status { get; set; } = "Scheduled";
        }

        public class TaskStageSummary
        {
            public string Stage { get; set; } = string.Empty;
            public int TotalCount { get; set; }
            public int CompletedCount { get; set; }
        }
    }
}
