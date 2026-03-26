using System.IO;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;

namespace JobEntryApp.Pages
{
    public class NewJobModel : PageModel
    {
        private readonly IConfiguration _config;

        public NewJobModel(IConfiguration config)
        {
            _config = config;
        }

        [BindProperty]
        public JobInputModel Job { get; set; } = new();

        public string? StatusMessage { get; set; }

        public List<SelectListItem> CustomerList { get; set; } = new();
        public List<SelectListItem> SubAccountList { get; set; } = new();
        public List<SelectListItem> CsrList { get; set; } = new();
        public List<SelectListItem> DataProcessingList { get; set; } = new();
        public List<SelectListItem> SalesList { get; set; } = new();

        // ===============================
        // FOLDER LOGIC
        // ===============================
        private string EnsureJobFolderStructure(string customer, string subAccount, int jobNumber, string jobName)
        {
            var basePath = _config["JobFoldersBasePath"] ?? "P:\\Danielle\\JOB FOLDERS";

            string Normalize(string s) => Regex.Replace(s, "\\s+", "").ToLowerInvariant();

            string? FindFolder(string parent, string target)
            {
                var normTarget = Normalize(target);
                var dirs = Directory.Exists(parent) ? Directory.GetDirectories(parent) : Array.Empty<string>();

                foreach (var dir in dirs)
                {
                    var name = Path.GetFileName(dir);
                    if (Normalize(name) == normTarget)
                        return dir;
                }

                return null;
            }

            var customerFolder = FindFolder(basePath, customer) ?? Path.Combine(basePath, customer);
            if (!Directory.Exists(customerFolder)) Directory.CreateDirectory(customerFolder);

            var subAccountFolder = FindFolder(customerFolder, subAccount) ?? Path.Combine(customerFolder, subAccount);
            if (!Directory.Exists(subAccountFolder)) Directory.CreateDirectory(subAccountFolder);

            var jobFolderName = $"{jobNumber} {jobName}";
            var jobFolder = FindFolder(subAccountFolder, jobFolderName) ?? Path.Combine(subAccountFolder, jobFolderName);
            if (!Directory.Exists(jobFolder)) Directory.CreateDirectory(jobFolder);

            var subfolders = new[] { "Art", "Data", "Signoffs", "Reports", "Control Cards", "PO & Instructions", "Postal" };

            foreach (var sub in subfolders)
            {
                var subPath = Path.Combine(jobFolder, sub);
                if (!Directory.Exists(subPath)) Directory.CreateDirectory(subPath);
            }

            return jobFolder;
        }

        // ===============================
        // LOAD FORM
        // ===============================
        public void OnGet()
        {
            LoadDropdowns();

            try
            {
                Job = new JobInputModel
                {
                    JobNumber = GetNextJobNumber()
                };
            }
            catch
            {
                Job = new JobInputModel
                {
                    JobNumber = 0
                };
                StatusMessage = "The database is unavailable right now, so the next job number could not be loaded.";
            }

            ModelState.Clear();
        }

        // ===============================
        // SAVE JOB + CREATE TASKS
        // ===============================
        public IActionResult OnPost()
        {
            LoadDropdowns();

            if (!ModelState.IsValid)
                return Page();

            var folder = EnsureJobFolderStructure(
                Job.Customer ?? "Unknown",
                Job.SubAccount ?? "Unknown",
                Job.JobNumber,
                Job.JobName ?? "Unnamed"
            );

            SaveJob(Job);

            // 🔥 THIS IS THE NEW PART
            GenerateTasksSimple(Job);

            StatusMessage = $"Job saved. Folder: {folder}";

            return RedirectToPage("/Jobs/Index");
        }

        // ===============================
        // SIMPLE TASK GENERATION
        // ===============================
        private void GenerateTasksSimple(JobInputModel job)
        {
            var cs = _config.GetConnectionString("JobEntryDb");

            using var conn = new SqlConnection(cs);
            conn.Open();

            var tasks = new List<(string Name, string Stage, int Offset)>
            {
                ("Job Scheduled", "Initiate", -21),
                ("Counts Approved", "Prep", -14),
                ("Data Received", "Prep", -10),
                ("Production Start", "Production", -2),
                ("Mail Date", "Finalize", 0)
            };

            int taskNumber = 1;

            foreach (var t in tasks)
            {
                var dueDate = (job.StartDate ?? DateTime.Today).AddDays(t.Offset);

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO Tasks
                    (JobNumber, TaskNumber, TaskName, Stage, DueDate, Status)
                    VALUES
                    (@JobNumber, @TaskNumber, @TaskName, @Stage, @DueDate, 'Scheduled')";

                cmd.Parameters.AddWithValue("@JobNumber", job.JobNumber);
                cmd.Parameters.AddWithValue("@TaskNumber", taskNumber++);
                cmd.Parameters.AddWithValue("@TaskName", t.Name);
                cmd.Parameters.AddWithValue("@Stage", t.Stage);
                cmd.Parameters.AddWithValue("@DueDate", dueDate);

                cmd.ExecuteNonQuery();
            }
        }

        // ===============================
        // SAVE JOB
        // ===============================
        private void SaveJob(JobInputModel job)
        {
            var cs = _config.GetConnectionString("JobEntryDb");

            using var conn = new SqlConnection(cs);
            conn.Open();

            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                INSERT INTO Jobs
                (JobNumber, JobName, Customer, SubAccount, Quantity, StartDate, MailDate, Status)
                VALUES
                (@JobNumber, @JobName, @Customer, @SubAccount, @Quantity, @StartDate, @MailDate, @Status)";

            cmd.Parameters.AddWithValue("@JobNumber", job.JobNumber);
            cmd.Parameters.AddWithValue("@JobName", job.JobName ?? "");
            cmd.Parameters.AddWithValue("@Customer", job.Customer ?? "");
            cmd.Parameters.AddWithValue("@SubAccount", job.SubAccount ?? "");
            cmd.Parameters.AddWithValue("@Quantity", (object?)job.Quantity ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@StartDate", job.StartDate ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@MailDate", job.MailDate ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Status", "New");

            cmd.ExecuteNonQuery();
        }

        // ===============================
        // JOB NUMBER
        // ===============================
        private int GetNextJobNumber()
        {
            var cs = _config.GetConnectionString("JobEntryDb");

            using var conn = new SqlConnection(cs);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT ISNULL(MAX(JobNumber),0) + 1 FROM Jobs";

            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        // ===============================
        // DROPDOWNS
        // ===============================
        private void LoadDropdowns()
        {
            CustomerList = new List<SelectListItem>
            {
                new SelectListItem { Text = "RWT", Value = "RWT" },
                new SelectListItem { Text = "MBI", Value = "MBI" }
            };
        }

        // ===============================
        // MODEL
        // ===============================
        public class JobInputModel
        {
            public int JobNumber { get; set; }

            public string? Customer { get; set; }
            public string? JobName { get; set; }
            public string? SubAccount { get; set; }

            public int? Quantity { get; set; }

            public DateTime? StartDate { get; set; }
            public DateTime? MailDate { get; set; }

            public string? PostageClass { get; set; }
            public string? PostageStyle { get; set; }

            public string? Csr { get; set; }
            public string? DataProcessing { get; set; }
            public string? Sales { get; set; }

            public bool RushJob { get; set; }

            public bool Print { get; set; }
            public int? PrintPieceCount { get; set; }

            public string? PrintComponent1Name { get; set; }
            public string? PrintComponent1FacingDirection { get; set; }
            public string? PrintComponent2Name { get; set; }
            public string? PrintComponent2FacingDirection { get; set; }
            public string? PrintComponent3Name { get; set; }
            public string? PrintComponent3FacingDirection { get; set; }
            public string? PrintComponent4Name { get; set; }
            public string? PrintComponent4FacingDirection { get; set; }

            public bool TwoWayMatch { get; set; }
            public int? MatchWayCount { get; set; }

            public string? MatchComponent1 { get; set; }
            public string? MatchComponent1FacingDirection { get; set; }
            public string? MatchComponent2 { get; set; }
            public string? MatchComponent2FacingDirection { get; set; }
            public string? MatchComponent3 { get; set; }
            public string? MatchComponent3FacingDirection { get; set; }
            public string? MatchComponent4 { get; set; }
            public string? MatchComponent4FacingDirection { get; set; }
            public string? MatchComponent5 { get; set; }
            public string? MatchComponent5FacingDirection { get; set; }
        }
    }
}
