using JobEntryApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace JobEntryApp.Pages
{
public class TasksModel : PageModel
{
    private readonly IConfiguration _config;
    private readonly ILogger<TasksModel> _logger;

    public TasksModel(IConfiguration config, ILogger<TasksModel> logger)
    {
        _config = config;
        _logger = logger;
    }

        public int JobNumber { get; set; }
        public string JobName { get; set; } = string.Empty;
        public DateTime? MailDate { get; set; }
        public DateTime? StartDate { get; set; }
        public string JobStatus { get; set; } = string.Empty;

        public List<TaskItem> Tasks { get; set; } = new();
        public bool CanUpdateTaskStatus { get; set; }
        public bool CanEditTaskNotes { get; set; }

        [TempData]
        public string? StatusMessage { get; set; }

        public IActionResult OnGet(int jobNumber)
        {
            if (jobNumber <= 0)
                return RedirectToPage("/Index");

            JobNumber = jobNumber;
            LoadJobInfo();
            SyncFolderAutomation();
            LoadTasks();

            return Page();
        }

        public IActionResult OnPostCompleteTask(Guid taskId, int jobNumber)
        {
            if (taskId == Guid.Empty || jobNumber <= 0)
            {
                StatusMessage = "Invalid task update request.";
                return RedirectToPage(new { jobNumber });
            }

            var cs = _config.GetConnectionString("JobEntryDb")
                ?? throw new InvalidOperationException("Missing connection string.");

            try
            {
                using var conn = new SqlConnection(cs);
                conn.Open();
                var tasksTable = GetTasksTable(conn);
                var taskColumns = DatabaseSchema.GetColumns(conn, tasksTable);
                var taskIdColumn = FindTaskKeyColumn(taskColumns);
                var statusColumn = DatabaseSchema.FindColumn(taskColumns, "Status", "status");
                if (taskIdColumn is null || statusColumn is null)
                {
                    StatusMessage = "This Tasks table does not support task status updates.";
                    return RedirectToPage(new { jobNumber });
                }

                var completedDateColumn = DatabaseSchema.FindColumn(taskColumns, "CompletedDate", "completed_date");

                using SqlCommand cmd = conn.CreateCommand();
                var setClauses = new List<string> { $"[{statusColumn}] = 'Completed'" };
                if (completedDateColumn is not null)
                {
                    setClauses.Add($"[{completedDateColumn}] = GETDATE()");
                }

                cmd.CommandText = $@"
                    UPDATE {tasksTable.SqlName}
                    SET {string.Join(", ", setClauses)}
                    WHERE [{taskIdColumn}] = @TaskId
                      AND [JobNumber] = @JobNumber;";

                cmd.Parameters.AddWithValue("@TaskId", taskId);
                cmd.Parameters.AddWithValue("@JobNumber", jobNumber);
                var rows = cmd.ExecuteNonQuery();

                StatusMessage = rows > 0
                    ? "Task marked as completed!"
                    : "Task not found for this job.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to complete task {TaskId} for job {JobNumber}.", taskId, jobNumber);
                StatusMessage = "Could not complete task.";
            }

            return RedirectToPage(new { jobNumber });
        }

        public IActionResult OnPostUncompleteTask(Guid taskId, int jobNumber)
        {
            if (taskId == Guid.Empty || jobNumber <= 0)
            {
                StatusMessage = "Invalid task update request.";
                return RedirectToPage(new { jobNumber });
            }

            var cs = _config.GetConnectionString("JobEntryDb")
                ?? throw new InvalidOperationException("Missing connection string.");

            try
            {
                using var conn = new SqlConnection(cs);
                conn.Open();
                var tasksTable = GetTasksTable(conn);
                var taskColumns = DatabaseSchema.GetColumns(conn, tasksTable);
                var taskIdColumn = FindTaskKeyColumn(taskColumns);
                var statusColumn = DatabaseSchema.FindColumn(taskColumns, "Status", "status");
                if (taskIdColumn is null || statusColumn is null)
                {
                    StatusMessage = "This Tasks table does not support task status updates.";
                    return RedirectToPage(new { jobNumber });
                }

                var completedDateColumn = DatabaseSchema.FindColumn(taskColumns, "CompletedDate", "completed_date");

                using SqlCommand cmd = conn.CreateCommand();
                var setClauses = new List<string> { $"[{statusColumn}] = 'Scheduled'" };
                if (completedDateColumn is not null)
                {
                    setClauses.Add($"[{completedDateColumn}] = NULL");
                }

                cmd.CommandText = $@"
                    UPDATE {tasksTable.SqlName}
                    SET {string.Join(", ", setClauses)}
                    WHERE [{taskIdColumn}] = @TaskId
                      AND [JobNumber] = @JobNumber;";

                cmd.Parameters.AddWithValue("@TaskId", taskId);
                cmd.Parameters.AddWithValue("@JobNumber", jobNumber);
                var rows = cmd.ExecuteNonQuery();

                StatusMessage = rows > 0
                    ? "Task marked as incomplete."
                    : "Task not found for this job.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to uncomplete task {TaskId} for job {JobNumber}.", taskId, jobNumber);
                StatusMessage = "Could not update task.";
            }

            return RedirectToPage(new { jobNumber });
        }

        public IActionResult OnPostUpdateNotes(Guid taskId, int jobNumber, string notes)
        {
            if (taskId == Guid.Empty || jobNumber <= 0)
            {
                StatusMessage = "Invalid notes update request.";
                return RedirectToPage(new { jobNumber });
            }

            var cleanedNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
            if (cleanedNotes?.Length > 2000)
            {
                StatusMessage = "Notes are too long (max 2000 characters).";
                return RedirectToPage(new { jobNumber });
            }

            var cs = _config.GetConnectionString("JobEntryDb")
                ?? throw new InvalidOperationException("Missing connection string.");

            try
            {
                using var conn = new SqlConnection(cs);
                conn.Open();
                var tasksTable = GetTasksTable(conn);
                var taskColumns = DatabaseSchema.GetColumns(conn, tasksTable);
                var taskIdColumn = FindTaskKeyColumn(taskColumns);
                var notesColumn = DatabaseSchema.FindColumn(taskColumns, "Notes", "notes");
                if (taskIdColumn is null || notesColumn is null)
                {
                    StatusMessage = "This Tasks table does not support notes.";
                    return RedirectToPage(new { jobNumber });
                }

                using SqlCommand cmd = conn.CreateCommand();
                cmd.CommandText = $@"
                    UPDATE {tasksTable.SqlName}
                    SET [{notesColumn}] = @Notes
                    WHERE [{taskIdColumn}] = @TaskId
                      AND [JobNumber] = @JobNumber;";

                cmd.Parameters.AddWithValue("@TaskId", taskId);
                cmd.Parameters.AddWithValue("@JobNumber", jobNumber);
                cmd.Parameters.AddWithValue("@Notes", (object?)cleanedNotes ?? DBNull.Value);
                var rows = cmd.ExecuteNonQuery();

                StatusMessage = rows > 0
                    ? "Notes updated!"
                    : "Task not found for this job.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update notes for task {TaskId} job {JobNumber}.", taskId, jobNumber);
                StatusMessage = "Could not update notes.";
            }

            return RedirectToPage(new { jobNumber });
        }

        private void LoadJobInfo()
        {
            var cs = _config.GetConnectionString("JobEntryDb")
                ?? throw new InvalidOperationException("Missing connection string.");

            using var conn = new SqlConnection(cs);
            conn.Open();

            using SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT JobName, MailDate, StartDate, Status
                FROM dbo.Jobs
                WHERE JobNumber = @JobNumber;";

            cmd.Parameters.AddWithValue("@JobNumber", JobNumber);

            using SqlDataReader reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                JobName = reader.IsDBNull(0) ? "" : reader.GetString(0);
                MailDate = reader.IsDBNull(1) ? null : reader.GetDateTime(1);
                StartDate = reader.IsDBNull(2) ? null : reader.GetDateTime(2);
                JobStatus = reader.IsDBNull(3) ? "" : reader.GetString(3);
            }
        }

        private void LoadTasks()
        {
            var cs = _config.GetConnectionString("JobEntryDb")
                ?? throw new InvalidOperationException("Missing connection string.");

            using var conn = new SqlConnection(cs);
            conn.Open();
            var tasksTable = GetTasksTable(conn);
            var taskColumns = DatabaseSchema.GetColumns(conn, tasksTable);
            var taskIdColumn = FindTaskKeyColumn(taskColumns);
            CanUpdateTaskStatus = taskIdColumn is not null && DatabaseSchema.FindColumn(taskColumns, "Status", "status") is not null;
            CanEditTaskNotes = taskIdColumn is not null && DatabaseSchema.FindColumn(taskColumns, "Notes", "notes") is not null;

            var taskIdExpr = taskIdColumn is null ? "CAST(NULL AS uniqueidentifier)" : $"[{taskIdColumn}]";
            var taskNumberExpr = SelectColumnOrNull(taskColumns, "CAST(NULL AS int)", "TaskNumber", "task_number");
            var taskNameExpr = SelectColumnOrNull(taskColumns, "CAST(NULL AS nvarchar(max))", "TaskName", "task_name");
            var stageExpr = SelectColumnOrNull(taskColumns, "CAST(NULL AS nvarchar(max))", "Stage", "stage");
            var assignedToExpr = SelectColumnOrNull(taskColumns, "CAST(NULL AS nvarchar(max))", "AssignedTo", "assigned_to");
            var assignee2Expr = SelectColumnOrNull(taskColumns, "CAST(NULL AS nvarchar(max))", "Assignee2", "assigned_to_2");
            var dueDateExpr = SelectColumnOrNull(taskColumns, "CAST(NULL AS datetime2)", "DueDate", "due_date");
            var dependenciesExpr = SelectColumnOrNull(taskColumns, "CAST(NULL AS nvarchar(max))", "Dependencies", "dependencies");
            var statusExpr = SelectColumnOrNull(taskColumns, "CAST(NULL AS nvarchar(max))", "Status", "status");
            var completedDateExpr = SelectColumnOrNull(taskColumns, "CAST(NULL AS datetime2)", "CompletedDate", "completed_date");
            var notesExpr = SelectColumnOrNull(taskColumns, "CAST(NULL AS nvarchar(max))", "Notes", "notes");

            using SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                SELECT 
                    {taskIdExpr} AS [TaskId],
                    {taskNumberExpr} AS [TaskNumber],
                    {taskNameExpr} AS [TaskName],
                    {stageExpr} AS [Stage],
                    {assignedToExpr} AS [AssignedTo],
                    {assignee2Expr} AS [Assignee2],
                    {dueDateExpr} AS [DueDate],
                    {dependenciesExpr} AS [Dependencies],
                    {statusExpr} AS [Status],
                    {completedDateExpr} AS [CompletedDate],
                    {notesExpr} AS [Notes]
                FROM {tasksTable.SqlName}
                WHERE [JobNumber] = @JobNumber
                ORDER BY CASE WHEN {taskNumberExpr} IS NULL THEN 1 ELSE 0 END, {taskNumberExpr}, {dueDateExpr};";

            cmd.Parameters.AddWithValue("@JobNumber", JobNumber);

            using SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var taskName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                Tasks.Add(new TaskItem
                {
                    TaskId = reader.IsDBNull(0) ? Guid.Empty : reader.GetGuid(0),
                    TaskNumber = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1)),
                    TaskName = taskName,
                    Stage = reader.IsDBNull(3) ? null : reader.GetString(3),
                    AssignedTo = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    Assignee2 = reader.IsDBNull(5) ? null : reader.GetString(5),
                    DueDate = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                    Dependencies = reader.IsDBNull(7) ? null : reader.GetString(7),
                    Status = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                    CompletedDate = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                    Notes = reader.IsDBNull(10) ? null : reader.GetString(10),
                    IsMilestone = TaskMilestoneCatalog.TryGetMilestone(taskName, out _)
                });
            }
        }

        private void SyncFolderAutomation()
        {
            var cs = _config.GetConnectionString("JobEntryDb")
                ?? throw new InvalidOperationException("Missing connection string.");

            try
            {
                using var conn = new SqlConnection(cs);
                conn.Open();
                var basePath = _config["JobFoldersBasePath"]?.Trim() ?? @"P:\Danielle\JOB FOLDERS";
                JobFolderAutomationService.SyncJobArtifactsForJob(conn, basePath, JobNumber, _logger);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Folder automation sync skipped while loading tasks for job {JobNumber}.", JobNumber);
            }
        }

        private static DatabaseTable GetTasksTable(SqlConnection conn)
            => DatabaseSchema.FindTable(conn, null, "Tasks", "tasks")
                ?? throw new InvalidOperationException("Could not find a Tasks table.");

        private static string? FindTaskKeyColumn(ISet<string> columns)
            => DatabaseSchema.FindColumn(columns, "TaskId", "task_id");

        private static string SelectColumnOrNull(ISet<string> columns, string nullExpression, params string[] candidates)
        {
            var column = DatabaseSchema.FindColumn(columns, candidates);
            return column is null ? nullExpression : $"[{column}]";
        }

        public class TaskItem
        {
            public Guid TaskId { get; set; }
            public int TaskNumber { get; set; }
            public string TaskName { get; set; } = string.Empty;
            public string? Stage { get; set; }
            public string AssignedTo { get; set; } = string.Empty;
            public string? Assignee2 { get; set; }
            public DateTime? DueDate { get; set; }
            public string? Dependencies { get; set; }
            public string Status { get; set; } = string.Empty;
            public DateTime? CompletedDate { get; set; }
            public string? Notes { get; set; }
            public bool IsMilestone { get; set; }

            public bool IsCompleted => Status == "Completed";
            public bool IsOverdue => DueDate.HasValue && DueDate.Value.Date < DateTime.Today && !IsCompleted;
        }
    }
}
