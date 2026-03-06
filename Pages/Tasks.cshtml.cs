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

        [TempData]
        public string? StatusMessage { get; set; }

        public IActionResult OnGet(int jobNumber)
        {
            if (jobNumber <= 0)
                return RedirectToPage("/Index");

            JobNumber = jobNumber;
            LoadJobInfo();
            LoadTasks();

            return Page();
        }

        public IActionResult OnPostCompleteTask(int taskId, int jobNumber)
        {
            if (taskId <= 0 || jobNumber <= 0)
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

                using SqlCommand cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    UPDATE dbo.Tasks 
                    SET Status = 'Completed', 
                        CompletedDate = GETDATE()
                    WHERE TaskId = @TaskId
                      AND JobNumber = @JobNumber;";

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

        public IActionResult OnPostUncompleteTask(int taskId, int jobNumber)
        {
            if (taskId <= 0 || jobNumber <= 0)
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

                using SqlCommand cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    UPDATE dbo.Tasks 
                    SET Status = 'Scheduled', 
                        CompletedDate = NULL
                    WHERE TaskId = @TaskId
                      AND JobNumber = @JobNumber;";

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

        public IActionResult OnPostUpdateNotes(int taskId, int jobNumber, string notes)
        {
            if (taskId <= 0 || jobNumber <= 0)
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

                using SqlCommand cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    UPDATE dbo.Tasks 
                    SET Notes = @Notes
                    WHERE TaskId = @TaskId
                      AND JobNumber = @JobNumber;";

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

            using SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT 
                    TaskId, TaskNumber, TaskName, Stage, AssignedTo, Assignee2,
                    DueDate, Dependencies, Status, CompletedDate, Notes
                FROM dbo.Tasks
                WHERE JobNumber = @JobNumber
                ORDER BY TaskNumber;";

            cmd.Parameters.AddWithValue("@JobNumber", JobNumber);

            using SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var taskName = reader.GetString(2);
                var milestoneNames = new[] { "Data Received", "Counts Approved", "Signoffs Due", "Signoffs Approved", "Production Out", "Mail Date" };
                Tasks.Add(new TaskItem
                {
                    TaskId = reader.GetInt32(0),
                    TaskNumber = reader.GetInt32(1),
                    TaskName = taskName,
                    Stage = reader.IsDBNull(3) ? null : reader.GetString(3),
                    AssignedTo = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    Assignee2 = reader.IsDBNull(5) ? null : reader.GetString(5),
                    DueDate = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                    Dependencies = reader.IsDBNull(7) ? null : reader.GetString(7),
                    Status = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                    CompletedDate = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                    Notes = reader.IsDBNull(10) ? null : reader.GetString(10),
                    IsMilestone = milestoneNames.Contains(taskName)
                });
            }
        }

        public class TaskItem
        {
            public int TaskId { get; set; }
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
