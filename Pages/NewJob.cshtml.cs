using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JobEntryApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Mvc.Rendering; // Add this at the top if not present

namespace JobEntryApp.Pages
{
    public class NewJobModel : PageModel, IValidatableObject
    {
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<NewJobModel> _logger;
        private readonly IWebHostEnvironment _environment;

		// Replace the old field and property definitions with these auto-properties:
		public List<SelectListItem> CustomerList { get; set; } = new();
		public List<SelectListItem> SubAccountList { get; set; } = new();
		public List<SelectListItem> CsrList { get; set; } = new();
		public List<SelectListItem> DataProcessingList { get; set; } = new();
		public List<SelectListItem> SalesList { get; set; } = new();

		public NewJobModel(
            IConfiguration config,
            IHttpClientFactory httpClientFactory,
            ILogger<NewJobModel> logger,
            IWebHostEnvironment environment)
        {
            _config = config;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _environment = environment;
        }

        [BindProperty]
        public JobModel Job { get; set; } = new JobModel
        {
            Quantity = 0,
            Status = "New"
        };

        [TempData]
        public string? StatusMessage { get; set; }

        public void OnGet()
        {
            var last = GetLastCommittedJobNumber();
            Job.JobNumber = last + 1;

            if (!Job.MatchWayCount.HasValue)
                Job.MatchWayCount = 2;

            if (!Job.PrintPieceCount.HasValue)
                Job.PrintPieceCount = 1;
        }

        public async Task<IActionResult> OnPost(string action)
        {
            if (!TryValidateModel(Job, nameof(Job)))
            {
                var last = GetLastCommittedJobNumber();
                Job.JobNumber = last + 1;

                if (!Job.MatchWayCount.HasValue)
                    Job.MatchWayCount = 2;
                if (!Job.PrintPieceCount.HasValue)
                    Job.PrintPieceCount = 1;

                return Page();
            }

            var lastCommitted = GetLastCommittedJobNumber();
            Job.JobNumber = lastCommitted + 1;

            try
            {
                var tasksCreated = SaveJobToDatabase(Job);
                StatusMessage = tasksCreated > 0
                    ? $"Job {Job.JobNumber} saved successfully with {tasksCreated} tasks created."
                    : $"Job {Job.JobNumber} saved successfully. Tasks were skipped because Mail Date is before today or missing.";
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "Error saving job: " + ex.Message);

                var last = GetLastCommittedJobNumber();
                Job.JobNumber = last + 1;
                if (!Job.MatchWayCount.HasValue)
                    Job.MatchWayCount = 2;
                if (!Job.PrintPieceCount.HasValue)
                    Job.PrintPieceCount = 1;

                return Page();
            }

            if (action == "createAndHome")
            {
                return RedirectToPage("/Index");
            }

            return RedirectToPage("/NewJob");
        }

        private void LoadSales()
        {
            var cs = _config.GetConnectionString("JobEntryDb");
            using var conn = new SqlConnection(cs);
            conn.Open();

            using var cmd = new SqlCommand("SELECT Name FROM Sales ORDER BY Name", conn);
            using var reader = cmd.ExecuteReader();

            SalesList.Clear();
            while (reader.Read())
            {
                SalesList.Add(new SelectListItem
                {
                    Value = reader.GetString(0),
                    Text = reader.GetString(0)
                });
            }
        }

        private async Task CreateJobFoldersAsync(int jobNumber)
        {
            var googleParentFolderId = _config["GoogleDrive:ParentFolderId"];
            if (!string.IsNullOrWhiteSpace(googleParentFolderId))
            {
                await CreateGoogleDriveFoldersAsync(jobNumber, googleParentFolderId);
                return;
            }

            var basePath = _config["JobFoldersBasePath"];
            if (string.IsNullOrWhiteSpace(basePath)) return;

            var subfolders = new[] { "Art", "Control Cards", "Data", "Reports", "Postal", "Signoffs", "PO & Instructions" };
            var jobPath = Path.Combine(basePath, jobNumber.ToString());
            Directory.CreateDirectory(jobPath);
            foreach (var sub in subfolders)
            {
                Directory.CreateDirectory(Path.Combine(jobPath, sub));
            }
        }

        private async Task CreateGoogleDriveFoldersAsync(int jobNumber, string parentFolderId)
        {
            var serviceAccountEmail = _config["GoogleDrive:ServiceAccountEmail"];
            var privateKeyPem = _config["GoogleDrive:PrivateKey"];
            TryLoadGoogleCredentialsFromJsonFile(ref serviceAccountEmail, ref privateKeyPem);

            if (string.IsNullOrWhiteSpace(serviceAccountEmail) || string.IsNullOrWhiteSpace(privateKeyPem))
            {
                _logger.LogWarning("Google Drive folder creation is configured, but ServiceAccountEmail/PrivateKey is missing.");
                return;
            }

            var accessToken = await GetGoogleAccessTokenAsync(serviceAccountEmail, privateKeyPem);
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                _logger.LogWarning("Could not obtain Google Drive access token.");
                return;
            }

            var http = _httpClientFactory.CreateClient();
            http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var jobFolderId = await CreateDriveFolderAsync(http, jobNumber.ToString(), parentFolderId);
            var subfolders = new[] { "Art", "Control Cards", "Data", "Reports", "Postal", "Signoffs", "PO & Instructions" };

            foreach (var subfolder in subfolders)
            {
                await CreateDriveFolderAsync(http, subfolder, jobFolderId);
            }
        }

        private void TryLoadGoogleCredentialsFromJsonFile(ref string? serviceAccountEmail, ref string? privateKeyPem)
        {
            var credentialsPath = _config["GoogleDrive:CredentialsJsonPath"];
            if (string.IsNullOrWhiteSpace(credentialsPath))
            {
                return;
            }

            var resolvedPath = Path.IsPathRooted(credentialsPath)
                ? credentialsPath
                : Path.Combine(_environment.ContentRootPath, credentialsPath);

            if (!System.IO.File.Exists(resolvedPath))
            {
                _logger.LogWarning("Google credentials JSON file not found at {Path}.", resolvedPath);
                return;
            }

            try
            {
                var json = System.IO.File.ReadAllText(resolvedPath);
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("client_email", out var emailEl))
                {
                    serviceAccountEmail = emailEl.GetString();
                }

                if (doc.RootElement.TryGetProperty("private_key", out var keyEl))
                {
                    privateKeyPem = keyEl.GetString();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to parse Google credentials JSON file.");
            }
        }

        private async Task<string?> GetGoogleAccessTokenAsync(string serviceAccountEmail, string privateKeyPem)
        {
            const string tokenEndpoint = "https://oauth2.googleapis.com/token";
            var now = DateTimeOffset.UtcNow;

            var header = Base64UrlEncode(Encoding.UTF8.GetBytes("{\"alg\":\"RS256\",\"typ\":\"JWT\"}"));
            var payloadObj = new
            {
                iss = serviceAccountEmail,
                scope = "https://www.googleapis.com/auth/drive",
                aud = tokenEndpoint,
                iat = now.ToUnixTimeSeconds(),
                exp = now.AddMinutes(55).ToUnixTimeSeconds()
            };
            var payload = Base64UrlEncode(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payloadObj)));
            var unsignedJwt = $"{header}.{payload}";

            var normalizedPem = privateKeyPem.Replace("\\n", "\n");
            using var rsa = RSA.Create();
            rsa.ImportFromPem(normalizedPem.ToCharArray());
            var signature = rsa.SignData(Encoding.UTF8.GetBytes(unsignedJwt), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var signedJwt = $"{unsignedJwt}.{Base64UrlEncode(signature)}";

            using var client = _httpClientFactory.CreateClient();
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = signedJwt
            });

            using var response = await client.PostAsync(tokenEndpoint, content);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google token endpoint error: {StatusCode} {Body}", response.StatusCode, body);
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("access_token", out var tokenElement))
            {
                return tokenElement.GetString();
            }

            return null;
        }

        private static async Task<string> CreateDriveFolderAsync(HttpClient http, string folderName, string parentFolderId)
        {
            var payload = new
            {
                name = folderName,
                mimeType = "application/vnd.google-apps.folder",
                parents = new[] { parentFolderId }
            };

            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var response = await http.PostAsync("https://www.googleapis.com/drive/v3/files?fields=id&supportsAllDrives=true", content);
            var responseBody = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(responseBody);
            return doc.RootElement.GetProperty("id").GetString() ?? throw new InvalidOperationException("Google Drive did not return an id.");
        }

        private static string Base64UrlEncode(byte[] input)
        {
            return Convert.ToBase64String(input)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private int GetLastCommittedJobNumber()
        {
            var cs = _config.GetConnectionString("JobEntryDb")
                ?? throw new InvalidOperationException("Missing connection string 'JobEntryDb'.");

            using var conn = new SqlConnection(cs);
            using SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT ISNULL(MAX(JobNumber), 43352) FROM dbo.Jobs;";

            conn.Open();
            var result = cmd.ExecuteScalar();
            return Convert.ToInt32(result);
        }

        private int SaveJobToDatabase(JobModel job)
        {
            var cs = _config.GetConnectionString("JobEntryDb")
                ?? throw new InvalidOperationException("Missing connection string 'JobEntryDb'.");

            using var conn = new SqlConnection(cs);
            conn.Open();

            using SqlTransaction tx = conn.BeginTransaction();
            var tasksCreated = 0;

            try
            {
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;

                    cmd.CommandText = @"INSERT INTO dbo.Jobs (
                        JobNumber, JobName, MailDate, Customer, SubAccount, Csr, DataProcessing,
                        Quantity, PostageStyle, PostageClass, Sales, StartDate, Status,
                        [Print], TwoWayMatch,
                        MatchWayCount, MatchComponent1, MatchComponent2, MatchComponent3, MatchComponent4, MatchComponent5,
                        PrintPieceCount, PrintComponent1Name, PrintComponent1FacingDirection,
                        PrintComponent2Name, PrintComponent2FacingDirection,
                        PrintComponent3Name, PrintComponent3FacingDirection,
                        PrintComponent4Name, PrintComponent4FacingDirection,
                        Commingler
                    ) VALUES (
                        @JobNumber, @JobName, @MailDate, @Customer, @SubAccount, @Csr, @DataProcessing,
                        @Quantity, @PostageStyle, @PostageClass, @Sales, @StartDate, @Status,
                        @Print, @TwoWayMatch,
                        @MatchWayCount, @MatchComponent1, @MatchComponent2, @MatchComponent3, @MatchComponent4, @MatchComponent5,
                        @PrintPieceCount, @PrintComponent1Name, @PrintComponent1FacingDirection,
                        @PrintComponent2Name, @PrintComponent2FacingDirection,
                        @PrintComponent3Name, @PrintComponent3FacingDirection,
                        @PrintComponent4Name, @PrintComponent4FacingDirection,
                        @Commingler
                    );";

                    cmd.Parameters.AddWithValue("@JobNumber", job.JobNumber);
                    cmd.Parameters.AddWithValue("@JobName", job.JobName ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@MailDate", job.MailDate.HasValue ? job.MailDate.Value : (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Customer", job.Customer ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@SubAccount", job.SubAccount ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Csr", job.Csr ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@DataProcessing", job.DataProcessing ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Quantity", job.Quantity ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@PostageStyle", job.PostageStyle ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@PostageClass", job.PostageClass ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Sales", job.Sales ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Commingler", (object?)job.Commingler ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@StartDate", job.StartDate.HasValue ? job.StartDate.Value : (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Status", job.Status ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Print", job.Print);
                    cmd.Parameters.AddWithValue("@TwoWayMatch", job.TwoWayMatch);
                    cmd.Parameters.AddWithValue("@MatchWayCount", job.MatchWayCount.HasValue ? job.MatchWayCount.Value : (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@MatchComponent1", job.MatchComponent1 ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@MatchComponent2", job.MatchComponent2 ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@MatchComponent3", job.MatchComponent3 ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@MatchComponent4", job.MatchComponent4 ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@MatchComponent5", job.MatchComponent5 ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@PrintPieceCount", job.PrintPieceCount.HasValue ? job.PrintPieceCount.Value : (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@PrintComponent1Name", job.PrintComponent1Name ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@PrintComponent1FacingDirection", job.PrintComponent1FacingDirection ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@PrintComponent2Name", job.PrintComponent2Name ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@PrintComponent2FacingDirection", job.PrintComponent2FacingDirection ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@PrintComponent3Name", job.PrintComponent3Name ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@PrintComponent3FacingDirection", job.PrintComponent3FacingDirection ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@PrintComponent4Name", job.PrintComponent4Name ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@PrintComponent4FacingDirection", job.PrintComponent4FacingDirection ?? (object)DBNull.Value);

                    cmd.ExecuteNonQuery();
                }

                using (SqlCommand mailCmd = conn.CreateCommand())
                {
                    mailCmd.Transaction = tx;

                    mailCmd.CommandText = @"
                        INSERT INTO dbo.MailChart (
                            JobNumber, Kit, Customer, JobName, Class, AE, MailDate, Quantity, StyleTruck, Commingler
                        ) VALUES (
                            @JobNumber, @Kit, @Customer, @JobName, @Class, @AE, @MailDate, @Quantity, @StyleTruck, @Commingler
                        );";

                    mailCmd.Parameters.AddWithValue("@JobNumber", job.JobNumber);
                    mailCmd.Parameters.AddWithValue("@Kit", 1);
                    mailCmd.Parameters.AddWithValue("@Customer", (object?)job.Customer ?? DBNull.Value);
                    mailCmd.Parameters.AddWithValue("@JobName", (object?)job.JobName ?? DBNull.Value);
                    mailCmd.Parameters.AddWithValue("@Class", (object?)job.PostageClass ?? DBNull.Value);
                    mailCmd.Parameters.AddWithValue("@AE", (object?)job.Csr ?? DBNull.Value);
                    mailCmd.Parameters.AddWithValue("@MailDate", job.MailDate.HasValue ? job.MailDate.Value : (object)DBNull.Value);
                    mailCmd.Parameters.AddWithValue("@Quantity", 0);
                    mailCmd.Parameters.AddWithValue("@StyleTruck", (object?)job.PostageStyle ?? DBNull.Value);
                    mailCmd.Parameters.AddWithValue("@Commingler", (object?)job.Commingler ?? DBNull.Value);

                    mailCmd.ExecuteNonQuery();
                }

                if (ShouldCreateTasksForJob(job))
                {
                    tasksCreated = CreateTasksFromTemplates(conn, tx, job);
                }
                else
                {
                    _logger.LogInformation(
                        "Skipping task generation for Job {JobNumber}: MailDate is null or before today ({Today}).",
                        job.JobNumber,
                        DateTime.Today.ToString("yyyy-MM-dd"));
                }

                tx.Commit();
                return tasksCreated;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        private static bool ShouldCreateTasksForJob(JobModel job)
        {
            return job.MailDate.HasValue && job.MailDate.Value.Date >= DateTime.Today;
        }

        private int CreateTasksFromTemplates(SqlConnection conn, SqlTransaction tx, JobModel job)
        {
            var hasIsActiveColumn = HasTaskTemplateColumn(conn, tx, "IsActive");
            var templates = ReadTaskTemplates(conn, tx, activeOnly: hasIsActiveColumn);

            if (hasIsActiveColumn && templates.Count == 0)
            {
                _logger.LogWarning("No active task templates found. Falling back to all task templates for job {JobNumber}.", job.JobNumber);
                templates = ReadTaskTemplates(conn, tx, activeOnly: false);
            }

            if (templates.Count == 0)
            {
                templates = GetBuiltInDefaultTemplates();
                _logger.LogWarning("No DB task templates found. Using built-in defaults for job {JobNumber}.", job.JobNumber);
            }

            var createdCount = 0;
            foreach (TaskTemplate template in templates)
            {
                using SqlCommand cmd = conn.CreateCommand();
                cmd.Transaction = tx;

                cmd.CommandText = @"
                    INSERT INTO dbo.Tasks (
                        JobNumber, TaskNumber, TaskName, Stage, AssignedTo, Assignee2,
                        DueDate, OffsetFromStartDate, OffsetFromMailDate, Dependencies, Status
                    ) VALUES (
                        @JobNumber, @TaskNumber, @TaskName, @Stage, @AssignedTo, @Assignee2,
                        @DueDate, @OffsetFromStartDate, @OffsetFromMailDate, @Dependencies, @Status
                    );";

                string assignedTo = template.Assignee switch
                {
                    "CSR" => job.Csr ?? "CSR",
                    "DP" => job.DataProcessing ?? "DP",
                    null => "Unassigned",
                    _ => template.Assignee
                };

                string? assignee2 = null;
                if (!string.IsNullOrWhiteSpace(template.Assignee2))
                {
                    assignee2 = template.Assignee2 switch
                    {
                        "CSR" => job.Csr ?? "CSR",
                        "DP" => job.DataProcessing ?? "DP",
                        _ => template.Assignee2
                    };
                }

                DateTime? dueDate = null;
                if (template.OffsetFromStartDate.HasValue && job.StartDate.HasValue)
                {
                    dueDate = job.StartDate.Value.AddDays(template.OffsetFromStartDate.Value);
                }
                else if (template.OffsetFromMailDate.HasValue && job.MailDate.HasValue)
                {
                    dueDate = job.MailDate.Value.AddDays(template.OffsetFromMailDate.Value);
                }

                cmd.Parameters.AddWithValue("@JobNumber", job.JobNumber);
                cmd.Parameters.AddWithValue("@TaskNumber", template.TaskNumber);
                cmd.Parameters.AddWithValue("@TaskName", template.TaskName);
                cmd.Parameters.AddWithValue("@Stage", (object?)template.Stage ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@AssignedTo", assignedTo);
                cmd.Parameters.AddWithValue("@Assignee2", (object?)assignee2 ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DueDate", dueDate.HasValue ? dueDate.Value : (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@OffsetFromStartDate", (object?)template.OffsetFromStartDate ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@OffsetFromMailDate", (object?)template.OffsetFromMailDate ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Dependencies", (object?)template.Dependencies ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Status", "Scheduled");

                cmd.ExecuteNonQuery();
                createdCount++;
            }

            return createdCount;
        }

        private List<TaskTemplate> ReadTaskTemplates(SqlConnection conn, SqlTransaction tx, bool activeOnly)
        {
            var templates = new List<TaskTemplate>();

            using SqlCommand cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = $@"
                SELECT TaskNumber, TaskName, Stage, DefaultAssignee, DaysOffset, Dependencies
                FROM dbo.TaskTemplates
                {(activeOnly ? "WHERE IsActive = 1" : string.Empty)}
                ORDER BY TaskNumber;";

            using SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                templates.Add(new TaskTemplate
                {
                    TaskNumber = reader.GetInt32(0),
                    TaskName = reader.GetString(1),
                    Stage = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Assignee = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Assignee2 = null,
                    OffsetFromStartDate = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4),
                    OffsetFromMailDate = null,
                    Dependencies = reader.IsDBNull(5) ? null : reader.GetString(5),
                    Category = null
                });
            }

            return templates;
        }

        private static bool HasTaskTemplateColumn(SqlConnection conn, SqlTransaction tx, string columnName)
        {
            using SqlCommand cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = 'dbo'
                  AND TABLE_NAME = 'TaskTemplates'
                  AND COLUMN_NAME = @ColumnName;";
            cmd.Parameters.AddWithValue("@ColumnName", columnName);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        private static List<TaskTemplate> GetBuiltInDefaultTemplates()
        {
            return
            [
                new TaskTemplate { TaskNumber = 1, TaskName = "Job Intake", Stage = "Intake", Assignee = "CSR", OffsetFromStartDate = 0, Dependencies = null },
                new TaskTemplate { TaskNumber = 2, TaskName = "Data Received", Stage = "Data", Assignee = "DP", OffsetFromStartDate = 0, Dependencies = "1" },
                new TaskTemplate { TaskNumber = 3, TaskName = "Data Review", Stage = "Data", Assignee = "DP", OffsetFromStartDate = 1, Dependencies = "2" },
                new TaskTemplate { TaskNumber = 4, TaskName = "Counts Approved", Stage = "Data", Assignee = "CSR", OffsetFromStartDate = 2, Dependencies = "3" },
                new TaskTemplate { TaskNumber = 5, TaskName = "Art Proof Ready", Stage = "Prepress", Assignee = "CSR", OffsetFromStartDate = 3, Dependencies = "4" },
                new TaskTemplate { TaskNumber = 6, TaskName = "Signoffs Due", Stage = "Prepress", Assignee = "CSR", OffsetFromStartDate = 5, Dependencies = "5" },
                new TaskTemplate { TaskNumber = 7, TaskName = "Signoffs Approved", Stage = "Prepress", Assignee = "CSR", OffsetFromStartDate = 7, Dependencies = "6" },
                new TaskTemplate { TaskNumber = 8, TaskName = "Merge/Purge", Stage = "Data", Assignee = "DP", OffsetFromStartDate = 8, Dependencies = "4" },
                new TaskTemplate { TaskNumber = 9, TaskName = "Print Files Ready", Stage = "Prepress", Assignee = "DP", OffsetFromStartDate = 9, Dependencies = "8" },
                new TaskTemplate { TaskNumber = 10, TaskName = "Print Production Start", Stage = "Production", Assignee = "CSR", OffsetFromStartDate = 10, Dependencies = "9" },
                new TaskTemplate { TaskNumber = 11, TaskName = "Print Quality Check", Stage = "Production", Assignee = "CSR", OffsetFromStartDate = 11, Dependencies = "10" },
                new TaskTemplate { TaskNumber = 12, TaskName = "Lettershop Setup", Stage = "Production", Assignee = "CSR", OffsetFromStartDate = 12, Dependencies = "11" },
                new TaskTemplate { TaskNumber = 13, TaskName = "Personalization", Stage = "Production", Assignee = "DP", OffsetFromStartDate = 13, Dependencies = "12" },
                new TaskTemplate { TaskNumber = 14, TaskName = "Insert Setup", Stage = "Production", Assignee = "CSR", OffsetFromStartDate = 14, Dependencies = "12" },
                new TaskTemplate { TaskNumber = 15, TaskName = "Match Setup", Stage = "Production", Assignee = "DP", OffsetFromStartDate = 15, Dependencies = "13" },
                new TaskTemplate { TaskNumber = 16, TaskName = "Addressing", Stage = "Production", Assignee = "DP", OffsetFromStartDate = 16, Dependencies = "15" },
                new TaskTemplate { TaskNumber = 17, TaskName = "Postal Prep", Stage = "Postal", Assignee = "CSR", OffsetFromStartDate = 17, Dependencies = "16" },
                new TaskTemplate { TaskNumber = 18, TaskName = "Postal Docs Complete", Stage = "Postal", Assignee = "CSR", OffsetFromStartDate = 18, Dependencies = "17" },
                new TaskTemplate { TaskNumber = 19, TaskName = "Production Out", Stage = "Production", Assignee = "CSR", OffsetFromStartDate = 19, Dependencies = "18" },
                new TaskTemplate { TaskNumber = 20, TaskName = "Final QC", Stage = "Production", Assignee = "CSR", OffsetFromStartDate = 20, Dependencies = "19" },
                new TaskTemplate { TaskNumber = 21, TaskName = "Truck/Style Confirmed", Stage = "Postal", Assignee = "CSR", OffsetFromStartDate = 21, Dependencies = "20" },
                new TaskTemplate { TaskNumber = 22, TaskName = "Mail Date", Stage = "Postal", Assignee = "CSR", OffsetFromStartDate = 22, Dependencies = "21" }
            ];
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var results = new List<ValidationResult>();

            if (Job.TwoWayMatch)
            {
                var count = Job.MatchWayCount ?? 0;
                if (count < 2)
                {
                    results.Add(new ValidationResult(
                        "When Match Job is selected, 'How many way match?' must be at least 2.",
                        new[] { $"{nameof(Job)}.{nameof(Job.MatchWayCount)}" }
                    ));
                }

                if (string.IsNullOrWhiteSpace(Job.MatchComponent1))
                {
                    results.Add(new ValidationResult(
                        "Matching Component 1 is required when Match Job is selected.",
                        new[] { $"{nameof(Job)}.{nameof(Job.MatchComponent1)}" }
                    ));
                }

                if (string.IsNullOrWhiteSpace(Job.MatchComponent2) && count >= 2)
                {
                    results.Add(new ValidationResult(
                        "Matching Component 2 is required when Match Job is selected.",
                        new[] { $"{nameof(Job)}.{nameof(Job.MatchComponent2)}" }
                    ));
                }
            }

            if (Job.Print)
            {
                var pieces = Job.PrintPieceCount ?? 0;
                if (pieces < 1)
                {
                    results.Add(new ValidationResult(
                        "When Print is selected, 'How many pieces printing?' must be at least 1.",
                        new[] { $"{nameof(Job)}.{nameof(Job.PrintPieceCount)}" }
                    ));
                }

                if (string.IsNullOrWhiteSpace(Job.PrintComponent1Name))
                {
                    results.Add(new ValidationResult(
                        "Component 1 Name is required when Print is selected.",
                        new[] { $"{nameof(Job)}.{nameof(Job.PrintComponent1Name)}" }
                    ));
                }

                if (pieces >= 2 && string.IsNullOrWhiteSpace(Job.PrintComponent2Name))
                {
                    results.Add(new ValidationResult(
                        "Component 2 Name is required when printing 2 or more pieces.",
                        new[] { $"{nameof(Job)}.{nameof(Job.PrintComponent2Name)}" }
                    ));
                }
            }

            return results;
        }

        private class TaskTemplate
        {
            public int TaskNumber { get; set; }
            public string TaskName { get; set; } = string.Empty;
            public string? Stage { get; set; }
            public string? Assignee { get; set; }
            public string? Assignee2 { get; set; }
            public int? OffsetFromStartDate { get; set; }
            public int? OffsetFromMailDate { get; set; }
            public string? Dependencies { get; set; }
            public string? Category { get; set; }
        }
    }
}
